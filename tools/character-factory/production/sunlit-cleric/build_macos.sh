#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../../.." && pwd)"
cd "$REPO_ROOT"

HUNYUAN_REV="f8db63096c8282cb27354314d896feba5ba6ff8a"
CACHE_ROOT="${CHARACTER_FACTORY_CACHE_ROOT:-$HOME/Library/Caches/voxel-character-factory}"
MODEL_ROOT="${HY3DGEN_MODELS:-$CACHE_ROOT/models}"
HUNYUAN_PY="$CACHE_ROOT/hunyuan3d-2-$HUNYUAN_REV-venv/bin/python"
BLENDER_BIN="${BLENDER_BIN:-/Applications/Blender.app/Contents/MacOS/Blender}"
VIEWS="$SCRIPT_DIR/views"
OUT="${1:-$REPO_ROOT/Artifacts/SunlitClericProduction}"
UNITY_ASSETS_ROOT="${2:-$REPO_ROOT/Assets/Generated/CharacterFactory}"

for view in front back left right; do
  test -s "$VIEWS/$view.jpg" || {
    echo "Missing Cleric turnaround view: $VIEWS/$view.jpg" >&2
    exit 2
  }
done

test -x "$BLENDER_BIN" || {
  echo "Blender is required at $BLENDER_BIN" >&2
  exit 2
}

mkdir -p "$OUT"
export PYTORCH_ENABLE_MPS_FALLBACK="${PYTORCH_ENABLE_MPS_FALLBACK:-1}"
export PYTHONUNBUFFERED=1
export HY3DGEN_MODELS="$MODEL_ROOT"
export HF_XET_HIGH_PERFORMANCE="${HF_XET_HIGH_PERFORMANCE:-1}"

echo "[1/7] Bootstrap cached Hunyuan3D environment"
chmod +x tools/character-factory/ci/bootstrap_hunyuan_macos.sh
tools/character-factory/ci/bootstrap_hunyuan_macos.sh
test -x "$HUNYUAN_PY"

echo "[2/7] Ensure multiview turbo weights are cached"
"$HUNYUAN_PY" - <<'PY'
from pathlib import Path
import os
from huggingface_hub import snapshot_download

repo_id = "tencent/Hunyuan3D-2mv"
root = Path(os.environ["HY3DGEN_MODELS"]).expanduser()
repo_root = root / repo_id
repo_root.mkdir(parents=True, exist_ok=True)
dit = repo_root / "hunyuan3d-dit-v2-mv-turbo"
required = (
    dit / "config.yaml",
    dit / "model.fp16.safetensors",
)
if not all(path.is_file() and path.stat().st_size > 0 for path in required):
    snapshot_download(
        repo_id=repo_id,
        allow_patterns=[
            "hunyuan3d-dit-v2-mv-turbo/**",
            "hunyuan3d-vae-v2-mv/**",
            "hunyuan3d-vae-v2/**",
        ],
        local_dir=str(repo_root),
    )
missing = [str(path) for path in required if not path.is_file() or path.stat().st_size == 0]
if missing:
    raise RuntimeError("Multiview Hunyuan cache incomplete: " + ", ".join(missing))
print(dit)
PY

echo "[3/7] Create canonical animation skeleton"
CANONICAL="$OUT/canonical_female.glb"
DONOR_RENDER="$OUT/canonical_female.input.png"
"$BLENDER_BIN" --background --python-exit-code 1 \
  --python tools/character-factory/ci/create_canonical_character_fixture.py -- \
  --canonical "$CANONICAL" \
  --input "$DONOR_RENDER"
test -s "$CANONICAL"

echo "[4/7] Reconstruct four-view Cleric and transfer canonical skin weights"
SPEC="$OUT/sunlit-cleric-multiview.json"
cat > "$SPEC" <<JSON
{
  "id": "sunlit_cleric_mv_01",
  "assetType": "character",
  "views": {
    "front": "$VIEWS/front.jpg",
    "back": "$VIEWS/back.jpg",
    "left": "$VIEWS/left.jpg",
    "right": "$VIEWS/right.jpg"
  },
  "outputDir": "$OUT",
  "generator": {
    "python": "$HUNYUAN_PY",
    "backend": "hunyuan-pytorch",
    "preset": "quality",
    "model": "tencent/Hunyuan3D-2mv",
    "subfolder": "hunyuan3d-dit-v2-mv-turbo",
    "device": "auto",
    "seed": 12345,
    "steps": 5,
    "octreeResolution": 256,
    "numChunks": 16000,
    "removeBackground": false,
    "enableFlashVdm": false
  },
  "rig": {
    "blender": "$BLENDER_BIN",
    "canonicalBody": "$CANONICAL",
    "bodyObject": "Body",
    "armatureObject": "Armature",
    "maxTransferDistance": 0.45
  }
}
JSON
python3 tools/character-factory/character_factory.py build "$SPEC"
BASE_FBX="$OUT/sunlit_cleric_mv_01.fbx"
test -s "$BASE_FBX"

echo "[5/7] Project front/back/left/right source colors onto the aligned mesh"
TEXTURED_FBX="$OUT/sunlit_cleric_mv_01.textured.fbx"
ATLAS="$OUT/sunlit_cleric_mv_01.multiview_basecolor.png"
"$BLENDER_BIN" --background --python-exit-code 1 \
  --python tools/character-factory/runtime/blender_texture_rigged_character.py -- \
  --input "$BASE_FBX" \
  --output "$TEXTURED_FBX" \
  --front "$VIEWS/front.jpg" \
  --back "$VIEWS/back.jpg" \
  --left "$VIEWS/left.jpg" \
  --right "$VIEWS/right.jpg" \
  --atlas "$ATLAS"
test -s "$TEXTURED_FBX"
test -s "$ATLAS"

# Keep the Character Factory manifest contract intact: its output FBX path is
# sunlit_cleric_mv_01.fbx. Preserve the geometry-only file for lookdev, then put
# the textured/animated FBX at the manifest path before Unity staging.
mv "$BASE_FBX" "$OUT/sunlit_cleric_mv_01.geometry_only.fbx"
mv "$TEXTURED_FBX" "$BASE_FBX"

echo "[6/7] Verify skin deformation and embedded gameplay clips"
"$BLENDER_BIN" --background --python-exit-code 1 \
  --python tools/character-factory/ci/verify_skinned_character.py -- \
  --input "$BASE_FBX"
"$BLENDER_BIN" --background --python-exit-code 1 \
  --python tools/character-factory/ci/verify_character_animations.py -- \
  --input "$BASE_FBX"

"$BLENDER_BIN" --background --python-exit-code 1 \
  --python tools/character-factory/ci/render_pipeline_artifact.py -- \
  --input "$BASE_FBX" \
  --output "$OUT/sunlit_cleric_mv_01.preview.png" \
  --preserve-materials

"$BLENDER_BIN" --background --python-exit-code 1 \
  --python tools/character-factory/ci/render_character_clip.py -- \
  --input "$BASE_FBX" \
  --output "$OUT/sunlit_cleric_mv_01.idle.png" \
  --clip Idle --frame 30

echo "[7/7] Stage the verified character for Unity automatic import"
python3 tools/character-factory/character_factory.py stage-unity \
  "$OUT/manifest.json" \
  --assets-root "$UNITY_ASSETS_ROOT"

cat <<EOF
Sunlit Cleric build complete.
  Animated FBX: $BASE_FBX
  Base-color atlas: $ATLAS
  Preview: $OUT/sunlit_cleric_mv_01.preview.png
  Unity staging root: $UNITY_ASSETS_ROOT
  Embedded clips: Idle, Walk, Run, Cast, StaffAttack
EOF
