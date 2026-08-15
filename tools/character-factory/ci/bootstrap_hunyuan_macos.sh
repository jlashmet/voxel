#!/usr/bin/env bash
# Bootstraps a pinned local Hunyuan3D-2 shape-generation environment on the
# self-hosted macOS runner. Source, Python env, and model weights live outside
# the repository so subsequent CI runs reuse them.
set -euo pipefail

HUNYUAN_REV="f8db63096c8282cb27354314d896feba5ba6ff8a"
CACHE_ROOT="${CHARACTER_FACTORY_CACHE_ROOT:-$HOME/Library/Caches/voxel-character-factory}"
SOURCE_DIR="$CACHE_ROOT/Hunyuan3D-2-$HUNYUAN_REV"
VENV_DIR="$CACHE_ROOT/hunyuan3d-2-$HUNYUAN_REV-venv"
STAMP="$VENV_DIR/.voxel-ready-$HUNYUAN_REV"
MODEL_ROOT="${HY3DGEN_MODELS:-$CACHE_ROOT/models}"
PYTHON_BIN="${PYTHON_BIN:-$(command -v python3)}"

mkdir -p "$CACHE_ROOT" "$MODEL_ROOT"

if [ ! -d "$SOURCE_DIR/.git" ]; then
  rm -rf "$SOURCE_DIR"
  git clone --filter=blob:none https://github.com/Tencent-Hunyuan/Hunyuan3D-2.git "$SOURCE_DIR"
fi

git -C "$SOURCE_DIR" fetch --depth 1 origin "$HUNYUAN_REV"
git -C "$SOURCE_DIR" checkout --detach "$HUNYUAN_REV"

if [ ! -x "$VENV_DIR/bin/python" ]; then
  rm -rf "$VENV_DIR"
  "$PYTHON_BIN" -m venv "$VENV_DIR"
fi

if [ ! -f "$STAMP" ]; then
  "$VENV_DIR/bin/python" -m pip install --upgrade pip setuptools wheel
  # Shape generation only. We intentionally do not compile the CUDA-oriented
  # texture rasterizers on Apple Silicon.
  "$VENV_DIR/bin/python" -m pip install -r "$SOURCE_DIR/requirements.txt"
  "$VENV_DIR/bin/python" -m pip install -e "$SOURCE_DIR"
  touch "$STAMP"
fi

# hy3dgen does not use HF_HOME as its primary local-model lookup. Its
# smart_load_model() first checks HY3DGEN_MODELS/<repo>/<subfolder>. Populate
# that exact persistent directory before the timed inference step so CI never
# spends its smoke-test timeout downloading weights.
export HY3DGEN_MODELS="$MODEL_ROOT"
"$VENV_DIR/bin/python" - <<'PY'
from pathlib import Path
import os
from huggingface_hub import snapshot_download

repo_id = "tencent/Hunyuan3D-2mini"
subfolders = (
    "hunyuan3d-dit-v2-mini-turbo",
    "hunyuan3d-vae-v2-mini-turbo",
)
root = Path(os.environ["HY3DGEN_MODELS"]).expanduser()
repo_root = root / repo_id
repo_root.mkdir(parents=True, exist_ok=True)

for subfolder in subfolders:
    target = repo_root / subfolder
    required = (
        target / "config.yaml",
        target / "model.fp16.safetensors",
    )
    if all(path.is_file() and path.stat().st_size > 0 for path in required):
        print(f"hunyuan model cache ready: {target}")
        continue

    print(f"prefetching {repo_id}/{subfolder} into {repo_root}", flush=True)
    snapshot_download(
        repo_id=repo_id,
        allow_patterns=[
            f"{subfolder}/config.yaml",
            f"{subfolder}/model.fp16.safetensors",
        ],
        local_dir=str(repo_root),
    )

    missing = [str(path) for path in required if not path.is_file() or path.stat().st_size == 0]
    if missing:
        raise RuntimeError("Hunyuan model prefetch incomplete: " + ", ".join(missing))
PY

"$VENV_DIR/bin/python" - <<'PY'
import torch
import hy3dgen
print(f"torch={torch.__version__} mps={getattr(torch.backends, 'mps', None) is not None and torch.backends.mps.is_available()}")
print(f"hy3dgen={hy3dgen.__file__}")
PY

printf '%s\n' "$VENV_DIR/bin/python"
