#!/usr/bin/env bash
# Bootstraps a pinned local Hunyuan3D-2 shape-generation environment on the
# self-hosted macOS runner. The source checkout, Python venv, and Hugging Face
# model cache live outside the repository so subsequent CI runs reuse them.
set -euo pipefail

HUNYUAN_REV="f8db63096c8282cb27354314d896feba5ba6ff8a"
CACHE_ROOT="${CHARACTER_FACTORY_CACHE_ROOT:-$HOME/Library/Caches/voxel-character-factory}"
SOURCE_DIR="$CACHE_ROOT/Hunyuan3D-2-$HUNYUAN_REV"
VENV_DIR="$CACHE_ROOT/hunyuan3d-2-$HUNYUAN_REV-venv"
STAMP="$VENV_DIR/.voxel-ready-$HUNYUAN_REV"
PYTHON_BIN="${PYTHON_BIN:-$(command -v python3)}"

mkdir -p "$CACHE_ROOT"

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

"$VENV_DIR/bin/python" - <<'PY'
import torch
import hy3dgen
print(f"torch={torch.__version__} mps={getattr(torch.backends, 'mps', None) is not None and torch.backends.mps.is_available()}")
print(f"hy3dgen={hy3dgen.__file__}")
PY

printf '%s\n' "$VENV_DIR/bin/python"
