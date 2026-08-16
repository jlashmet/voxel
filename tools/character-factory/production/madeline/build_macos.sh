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
SOURCE_VIEWS="$SCRIPT_DIR/views"
OUT="${1:-$REPO_ROOT/Artifacts/MadelineBaseProduction}"
UNITY_ASSETS_ROOT="${2:-$REPO_ROOT/Assets/Generated/CharacterFactory}"
RAW_VIEWS="$OUT/reference/raw"
BODY_VIEWS="$OUT/reference/body-only"

for view in front back left right; do
  test -s "$SOURCE_VIEWS/$view.jpg" || {
    echo "Missing approved Madeline reference: $SOURCE_VIEWS/$view.jpg" >&2
    exit 2
  }
done

test -x "$BLENDER_BIN" || {
  echo "Blender is required at $BLENDER_BIN" >&2
  exit 2
}

mkdir -p "$OUT" "$RAW_VIEWS" "$BODY_VIEWS"
export PYTORCH_ENABLE_MPS_FALLBACK="${PYTORCH_ENABLE_MPS_FALLBACK:-1}"
export PYTHONUNBUFFERED=1
export HY3DGEN_MODELS="$MODEL_ROOT"
export HF_XET_HIGH_PERFORMANCE="${HF_XET_HIGH_PERFORMANCE:-1}"
# Preserve the approved Madeline proportions during canonical skeleton alignment.
# The historical global default (0.78) strongly morphs meshes to mannequin bounds;
# this low blend keeps most generated body shape while still nudging joints/bounds
# toward the donor for reliable weight transfer.
export CHARACTER_FACTORY_ALIGNMENT_BLEND="${CHARACTER_FACTORY_ALIGNMENT_BLEND:-0.15}"

echo "[1/8] Copy the approved Madeline turnaround into the build audit trail"
for view in front back left right; do
  cp "$SOURCE_VIEWS/$view.jpg" "$RAW_VIEWS/$view.jpg"
done

echo "[2/8] Prepare body-only geometry/texture references"
python3 "$SCRIPT_DIR/prepare_body_texture_views.py" \
  --input-dir "$RAW_VIEWS" \
  --output-dir "$BODY_VIEWS" \
  --report "$OUT/body-only-reference-report.json"

echo "[3/8] Bootstrap cached Hunyuan3D environment"
chmod +x tools/character-factory/ci/bootstrap_hunyuan_macos.sh
tools/character-factory/ci/bootstrap_hunyuan_macos.sh
test -x "$HUNYUAN_PY"

echo "[4/8] Ensure multiview turbo weights are cached"
"$HUNYUAN_PY" - <<'PY'
from pathlib import Path
import os
from huggingface_hub import snapshot_download

repo_id = "tencent/Hunyuan3D-2mv"
root = Path(os.environ["HY3DGEN_MODELS"]).expanduser()
repo_root = root / repo_id
repo_root.mkdir(parents=True, exist_ok=True)
dit = repo_root / "hunyuan3d-dit-v2-mv-turbo"
required = (dit / "config.yaml", dit / "model.fp16.safetensors")
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

echo "[5/8] Create canonical animation skeleton"
CANONICAL="$OUT/canonical_female.glb"
DONOR_RENDER="$OUT/canonical_female.input.png"
"$BLENDER_BIN" --background --python-exit-code 1 \
  --python tools/character-factory/ci/create_canonical_character_fixture.py -- \
  --canonical "$CANONICAL" \
  --input "$DONOR_RENDER"
test -s "$CANONICAL"

echo "[6/8] Reconstruct clothing-free Madeline body shape from the approved turnaround"
SPEC="$OUT/madeline-base.json"
cat > "$SPEC" <<JSON
{
  "id": "madeline_base_01",
  "assetType": "character",
  "views": {
    "front": "$BODY_VIEWS/front.jpg",
    "back": "$BODY_VIEWS/back.jpg",
    "left": "$BODY_VIEWS/left.jpg",
    "right": "$BODY_VIEWS/right.jpg"
  },
  "outputDir": "$OUT",
  "generator": {
    "python": "$HUNYUAN_PY",
    "backend": "hunyuan-pytorch",
    "preset": "quality",
    "model": "tencent/Hunyuan3D-2mv",
    "subfolder": "hunyuan3d-dit-v2-mv-turbo",
    "device": "auto",
    "seed": 28471,
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
BASE_FBX="$OUT/madeline_base_01.fbx"
test -s "$BASE_FBX"

echo "[7/8] Project face/hair/body source color onto the clothing-free mesh"
TEXTURED_FBX="$OUT/madeline_base_01.textured.fbx"
ATLAS="$OUT/madeline_base_01.body_basecolor.png"
"$BLENDER_BIN" --background --python-exit-code 1 \
  --python tools/character-factory/runtime/blender_texture_rigged_character.py -- \
  --input "$BASE_FBX" \
  --output "$TEXTURED_FBX" \
  --front "$BODY_VIEWS/front.jpg" \
  --back "$BODY_VIEWS/back.jpg" \
  --left "$BODY_VIEWS/left.jpg" \
  --right "$BODY_VIEWS/right.jpg" \
  --atlas "$ATLAS"
test -s "$TEXTURED_FBX"
test -s "$ATLAS"

mv "$BASE_FBX" "$OUT/madeline_base_01.geometry_only.fbx"
mv "$TEXTURED_FBX" "$BASE_FBX"

"$BLENDER_BIN" --background --python-exit-code 1 \
  --python tools/character-factory/ci/verify_skinned_character.py -- \
  --input "$BASE_FBX"
"$BLENDER_BIN" --background --python-exit-code 1 \
  --python tools/character-factory/ci/verify_character_animations.py -- \
  --input "$BASE_FBX"

"$BLENDER_BIN" --background --python-exit-code 1 \
  --python tools/character-factory/ci/render_pipeline_artifact.py -- \
  --input "$BASE_FBX" \
  --output "$OUT/madeline_base_01.preview.png" \
  --preserve-materials
"$BLENDER_BIN" --background --python-exit-code 1 \
  --python tools/character-factory/ci/render_character_clip.py -- \
  --input "$BASE_FBX" \
  --output "$OUT/madeline_base_01.idle.png" \
  --clip Idle --frame 30

echo "[8/8] Stage the verified base character for Unity automatic import"
python3 tools/character-factory/character_factory.py stage-unity \
  "$OUT/manifest.json" \
  --assets-root "$UNITY_ASSETS_ROOT"

cat <<EOF
Madeline base-body build complete.
  Animated FBX: $BASE_FBX
  Body-only atlas: $ATLAS
  Preview: $OUT/madeline_base_01.preview.png
  Reference audit: $OUT/body-only-reference-report.json
  Alignment blend: $CHARACTER_FACTORY_ALIGNMENT_BLEND
  Unity staging root: $UNITY_ASSETS_ROOT
  Embedded clips: Idle, Walk, Run, Cast, StaffAttack
EOF
