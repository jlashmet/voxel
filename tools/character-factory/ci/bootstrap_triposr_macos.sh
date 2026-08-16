#!/usr/bin/env bash
# Fast smoke backend for Apple Silicon. TripoSR is a feed-forward image-to-3D
# model and this pinned fork explicitly runs it on MPS.
set -euo pipefail

TRIPOSR_REV="24e6763a8b20d07b4b9f796f44aed45e412f2dcd"
CACHE_ROOT="${CHARACTER_FACTORY_CACHE_ROOT:-$HOME/Library/Caches/voxel-character-factory}"
SOURCE_DIR="$CACHE_ROOT/TripoSR-$TRIPOSR_REV"
VENV_DIR="$CACHE_ROOT/triposr-$TRIPOSR_REV-py312-venv"
MODEL_DIR="$CACHE_ROOT/models/triposr"
DINO_DIR="$CACHE_ROOT/models/dino-vitb16"
STAMP="$VENV_DIR/.voxel-ready-v6"

resolve_python312() {
  if [ -n "${PYTHON_BIN:-}" ]; then
    "$PYTHON_BIN" -c 'import sys; assert sys.version_info[:2] == (3, 12)' >/dev/null
    printf '%s\n' "$PYTHON_BIN"
    return
  fi

  if command -v python3.12 >/dev/null 2>&1; then
    command -v python3.12
    return
  fi

  if [ -x /opt/homebrew/bin/python3.12 ]; then
    printf '%s\n' /opt/homebrew/bin/python3.12
    return
  fi

  if ! command -v brew >/dev/null 2>&1; then
    echo "TripoSR smoke backend requires Python 3.12; Homebrew is unavailable" >&2
    exit 1
  fi

  brew install python@3.12
  local brew_python
  brew_python="$(brew --prefix python@3.12)/bin/python3.12"
  test -x "$brew_python"
  printf '%s\n' "$brew_python"
}

PYTHON_BIN="$(resolve_python312)"
"$PYTHON_BIN" -c 'import sys; print(f"TripoSR bootstrap Python: {sys.version.split()[0]}"); assert sys.version_info[:2] == (3, 12)'

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

"$VENV_DIR/bin/python" -c 'import sys; assert sys.version_info[:2] == (3, 12), sys.version'

if [ ! -f "$STAMP" ]; then
  "$VENV_DIR/bin/python" -m pip install --upgrade pip setuptools wheel
  "$VENV_DIR/bin/python" -m pip install torch torchvision

  # Upstream pins UI-only Gradio, an old xatlas release that no longer builds
  # cleanly on this Apple-Silicon runner, and trimesh 4.0.5. The old trimesh
  # GLB exporter calls ndarray.ptp(), which NumPy 2 removed. The smoke path
  # needs neither Gradio nor texture baking, so install the runtime set and use
  # current compatible binary/exporter releases instead of downgrading NumPy.
  SMOKE_REQUIREMENTS="$VENV_DIR/triposr-smoke-requirements.txt"
  grep -Ev '^[[:space:]]*(gradio([[:space:]]|$)|xatlas==|trimesh==)' "$SOURCE_DIR/requirements.txt" > "$SMOKE_REQUIREMENTS"
  "$VENV_DIR/bin/python" -m pip install 'xatlas==0.0.11' 'trimesh==4.12.2'
  "$VENV_DIR/bin/python" -m pip install -r "$SMOKE_REQUIREMENTS"

  # rembg is imported by TripoSR's run.py even when --no-remove-bg is used.
  # The package intentionally leaves the execution provider optional.
  "$VENV_DIR/bin/python" -m pip install onnxruntime

  # Reassert the exporter version in case a transitive dependency resolver
  # changed it while installing the upstream runtime requirements.
  "$VENV_DIR/bin/python" -m pip install 'trimesh==4.12.2'

  "$VENV_DIR/bin/python" - <<'PY'
import importlib
import numpy
import trimesh
for module in ("torch", "torchvision", "omegaconf", "PIL", "einops", "transformers", "trimesh", "onnxruntime", "rembg", "xatlas", "moderngl"):
    importlib.import_module(module)
assert trimesh.__version__ == "4.12.2", trimesh.__version__
print(f"TripoSR smoke runtime imports ready; numpy={numpy.__version__} trimesh={trimesh.__version__}")
PY
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
    print("downloading TripoSR checkpoint", flush=True)
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
import sys
import numpy
import torch
import trimesh
print(f"python={sys.version.split()[0]} torch={torch.__version__} numpy={numpy.__version__} trimesh={trimesh.__version__} mps={torch.backends.mps.is_available()}")
if not torch.backends.mps.is_available():
    raise SystemExit("Apple MPS is unavailable")
PY
