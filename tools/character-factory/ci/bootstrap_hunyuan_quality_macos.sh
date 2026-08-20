#!/usr/bin/env bash
# Bootstrap the pinned Hunyuan runtime plus the multiview turbo checkpoint used
# by production character/clothing geometry generation on Apple Silicon.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
cd "$REPO_ROOT"

BASE_BOOTSTRAP="$SCRIPT_DIR/bootstrap_hunyuan_macos.sh"
test -x "$BASE_BOOTSTRAP" || chmod +x "$BASE_BOOTSTRAP"
HUNYUAN_PY="$("$BASE_BOOTSTRAP" | tail -n 1)"
test -x "$HUNYUAN_PY"

CACHE_ROOT="${CHARACTER_FACTORY_CACHE_ROOT:-$HOME/Library/Caches/voxel-character-factory}"
MODEL_ROOT="${HY3DGEN_MODELS:-$CACHE_ROOT/models}"
export HY3DGEN_MODELS="$MODEL_ROOT"
export HF_XET_HIGH_PERFORMANCE="${HF_XET_HIGH_PERFORMANCE:-1}"
mkdir -p "$MODEL_ROOT"

"$HUNYUAN_PY" - <<'PY'
from pathlib import Path
import os
from huggingface_hub import snapshot_download

repo_id = "tencent/Hunyuan3D-2mv"
subfolder = "hunyuan3d-dit-v2-mv-turbo"
root = Path(os.environ["HY3DGEN_MODELS"]).expanduser()
repo_root = root / repo_id
repo_root.mkdir(parents=True, exist_ok=True)
required_relpaths = [
    f"{subfolder}/config.yaml",
    f"{subfolder}/model.fp16.safetensors",
]

required = [repo_root / relpath for relpath in required_relpaths]
if not all(path.is_file() and path.stat().st_size > 0 for path in required):
    snapshot_download(
        repo_id=repo_id,
        local_dir=str(repo_root),
        allow_patterns=required_relpaths,
    )

missing = [str(path) for path in required if not path.is_file() or path.stat().st_size == 0]
if missing:
    raise RuntimeError("Hunyuan multiview quality cache is incomplete: " + ", ".join(missing))

print("Hunyuan multiview quality cache ready: " + str(repo_root / subfolder))
PY

printf '%s\n' "$HUNYUAN_PY"
