#!/usr/bin/env bash
# Fast smoke backend for Apple Silicon. TripoSR is a feed-forward image-to-3D
# model and this pinned fork explicitly runs it on MPS.
set -euo pipefail

TRIPOSR_REV="24e6763a8b20d07b4b9f796f44aed45e412f2dcd"
CACHE_ROOT="${CHARACTER_FACTORY_CACHE_ROOT:-$HOME/Library/Caches/voxel-character-factory}"
SOURCE_DIR="$CACHE_ROOT/TripoSR-$TRIPOSR_REV"
VENV_DIR="$CACHE_ROOT/triposr-$TRIPOSR_REV-venv"
MODEL_DIR="$CACHE_ROOT/models/triposr"
DINO_DIR="$CACHE_ROOT/models/dino-vitb16"
STAMP="$VENV_DIR/.voxel-ready-v1"
PYTHON_BIN="${PYTHON_BIN:-$(command -v python3)}"

mkdir -p "$CACHE_ROOT" "$MODEL_DIR" "$DINO_DIR"

if [ ! -d "$SOURCE_DIR/.git" ]; then
  rm -rf "$SOURCE_DIR"
  git clone --filter=blob:none https://github.com/StarxSky/TRIPOSR.git "$SOURCE_DIR"
fi
git -C "$SOURCE_DIR" fetch --depth 1 origin "$TRIPOSR_REV"
git -C "$SOURCE_DIR" checkout --detach "$TRIPOSR_REV"

if [ ! -x "$VENV_DIR/bin/python" ]; then
  rm -rf "$VENV_DIR"
  "$PYTHON_BIN" -m venv "$VENV_DIR"
fi

if [ ! -f "$STAMP" ]; then
  "$VENV_DIR/bin/python" -m pip install --upgrade pip setuptools wheel
  "$VENV_DIR/bin/python" -m pip install torch torchvision
  "$VENV_DIR/bin/python" -m pip install -r "$SOURCE_DIR/requirements.txt"
  touch "$STAMP"
fi

# Standard HTTP downloads have been more predictable than the multi-GB Xet
# transfer on this runner. Both model caches are persistent and resume across
# CI attempts.
export HF_HUB_DISABLE_XET=1
"$VENV_DIR/bin/python" - <<'PY'
from pathlib import Path
import os
from huggingface_hub import snapshot_download

cache_root = Path(os.environ.get("CHARACTER_FACTORY_CACHE_ROOT", Path.home() / "Library/Caches/voxel-character-factory"))
model_dir = cache_root / "models/triposr"
dino_dir = cache_root / "models/dino-vitb16"

if not (model_dir / "model.ckpt").is_file():
    print("downloading TripoSR 1.68 GB checkpoint", flush=True)
    snapshot_download(
        repo_id="stabilityai/TripoSR",
        allow_patterns=["config.yaml", "model.ckpt"],
        local_dir=str(model_dir),
    )

if not (dino_dir / "pytorch_model.bin").is_file():
    print("downloading DINO ViT-B/16 image encoder", flush=True)
    snapshot_download(
        repo_id="facebook/dino-vitb16",
        allow_patterns=["config.json", "preprocessor_config.json", "pytorch_model.bin"],
        local_dir=str(dino_dir),
    )

config = model_dir / "config.yaml"
text = config.read_text(encoding="utf-8")
text = text.replace('pretrained_model_name_or_path: "facebook/dino-vitb16"', f'pretrained_model_name_or_path: "{dino_dir}"')
config.write_text(text, encoding="utf-8")

for required in (model_dir / "model.ckpt", model_dir / "config.yaml", dino_dir / "pytorch_model.bin"):
    if not required.is_file() or required.stat().st_size == 0:
        raise RuntimeError(f"missing TripoSR smoke dependency: {required}")
print("TripoSR smoke cache ready", flush=True)
PY

"$VENV_DIR/bin/python" - <<'PY'
import torch
print(f"torch={torch.__version__} mps={torch.backends.mps.is_available()}")
if not torch.backends.mps.is_available():
    raise SystemExit("Apple MPS is unavailable")
PY
