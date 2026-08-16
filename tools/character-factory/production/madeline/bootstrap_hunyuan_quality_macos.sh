#!/usr/bin/env bash
# Reuse the existing Character Factory Hunyuan environment, then cache the multiview
# quality checkpoint needed by the Madeline production body build.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../../.." && pwd)"
cd "$REPO_ROOT"

# This creates/pins the same source + Python environment used by Character Factory.
BASE_BOOTSTRAP="$REPO_ROOT/tools/character-factory/ci/bootstrap_hunyuan_macos.sh"
test -x "$BASE_BOOTSTRAP" || chmod +x "$BASE_BOOTSTRAP"
HUNYUAN_PY="$("$BASE_BOOTSTRAP" | tail -n 1)"
test -x "$HUNYUAN_PY"

CACHE_ROOT="${CHARACTER_FACTORY_CACHE_ROOT:-$HOME/Library/Caches/voxel-character-factory}"
MODEL_ROOT="${HY3DGEN_MODELS:-$CACHE_ROOT/models}"
export HY3DGEN_MODELS="$MODEL_ROOT"
export HF_XET_HIGH_PERFORMANCE="${HF_XET_HIGH_PERFORMANCE:-1}"
mkdir -p "$MODEL_ROOT"

# Keep the model in hy3dgen's expected local repository layout. Downloading the
# complete multiview repository avoids depending on undocumented internal VAE file
# names while still remaining a persistent one-time cache on the self-hosted Mac.
"$HUNYUAN_PY" - <<'PY'
from pathlib import Path
import os
from huggingface_hub import snapshot_download

repo_id = "tencent/Hunyuan3D-2mv"
root = Path(os.environ["HY3DGEN_MODELS"]).expanduser()
repo_root = root / repo_id
repo_root.mkdir(parents=True, exist_ok=True)

snapshot_download(
    repo_id=repo_id,
    local_dir=str(repo_root),
)

required = repo_root / "hunyuan3d-dit-v2-mv" / "config.yaml"
if not required.is_file() or required.stat().st_size == 0:
    raise RuntimeError(
        "Hunyuan3D-2mv cache is incomplete; missing " + str(required)
    )

print("Madeline Hunyuan quality cache ready: " + str(repo_root))
PY

printf '%s\n' "$HUNYUAN_PY"
