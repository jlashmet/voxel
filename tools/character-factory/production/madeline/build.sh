#!/usr/bin/env bash
# Production build for Madeline, the reusable base character behind the Sunlit Cleric.
# Clothing and equipment are intentionally excluded from these source views.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../../.." && pwd)"
cd "$REPO_ROOT"

BLENDER_BIN="${BLENDER_BIN:-/Applications/Blender.app/Contents/MacOS/Blender}"
test -x "$BLENDER_BIN"

FRONT="$SCRIPT_DIR/refs/madeline_body_front.png"
BACK="$SCRIPT_DIR/refs/madeline_body_back.png"
LEFT="$SCRIPT_DIR/refs/madeline_body_left.png"
RIGHT="$SCRIPT_DIR/refs/madeline_body_right.png"
FACE="$SCRIPT_DIR/refs/madeline_face_front.png"
for input in "$FRONT" "$BACK" "$LEFT" "$RIGHT" "$FACE"; do
  if [ ! -s "$input" ]; then
    echo "Missing Madeline production reference: $input" >&2
    echo "See $SCRIPT_DIR/README.md. Do not substitute the robe-clad cleric turnaround." >&2
    exit 2
  fi
done

CACHE_ROOT="${CHARACTER_FACTORY_CACHE_ROOT:-$HOME/Library/Caches/voxel-character-factory}"
HUNYUAN_REV="f8db63096c8282cb27354314d896feba5ba6ff8a"
HUNYUAN_PY="${HUNYUAN_PY:-$CACHE_ROOT/hunyuan3d-2-$HUNYUAN_REV-venv/bin/python}"
if [ ! -x "$HUNYUAN_PY" ]; then
  echo "Hunyuan environment not found at $HUNYUAN_PY" >&2
  echo "Bootstrap the existing Character Factory Hunyuan environment first." >&2
  exit 3
fi

OUT="${1:-$REPO_ROOT/Artifacts/MadelineProduction}"
rm -rf "$OUT"
mkdir -p "$OUT"

# The existing canonical fixture supplies the skeleton/weight-transfer donor. It is
# not the visual body source; Hunyuan reconstructs the neutral Madeline references.
CANONICAL="$OUT/canonical_female.glb"
CANONICAL_PREVIEW="$OUT/canonical_female.input.png"
"$BLENDER_BIN" --background \
  --python tools/character-factory/ci/create_canonical_character_fixture.py -- \
  --canonical "$CANONICAL" \
  --input "$CANONICAL_PREVIEW"
test -s "$CANONICAL"

SPEC="$OUT/madeline-body.json"
cat > "$SPEC" <<JSON
{
  "id": "madeline_body_01",
  "assetType": "character",
  "views": {
    "front": "$FRONT",
    "back": "$BACK",
    "left": "$LEFT",
    "right": "$RIGHT"
  },
  "outputDir": "$OUT",
  "generator": {
    "python": "$HUNYUAN_PY",
    "backend": "hunyuan-pytorch",
    "preset": "quality",
    "device": "auto",
    "seed": 31827,
    "removeBackground": true
  },
  "rig": {
    "blender": "$BLENDER_BIN",
    "canonicalBody": "$CANONICAL",
    "bodyObject": "Body",
    "armatureObject": "Armature",
    "maxTransferDistance": 0.38
  }
}
JSON

# First use the existing Character Factory unchanged: multiview shape generation,
# canonical alignment/weight transfer, and skinned FBX export.
python3 tools/character-factory/character_factory.py build "$SPEC"
RAW_FBX="$OUT/madeline_body_01.fbx"
test -s "$RAW_FBX"

"$BLENDER_BIN" --background \
  --python tools/character-factory/ci/verify_skinned_character.py -- \
  --input "$RAW_FBX"

# Face identity is intentionally a separate appearance pass. Hunyuan's shape result is
# not trusted as the authoritative facial texture.
FACE_FBX="$OUT/madeline_body_01.face.fbx"
"$BLENDER_BIN" --background \
  --python tools/character-factory/runtime/blender_project_face_texture.py -- \
  --input "$RAW_FBX" \
  --face "$FACE" \
  --output "$FACE_FBX"
test -s "$FACE_FBX"

# Keep the manifest contract stable: replace the pipeline FBX in place, revalidate it,
# then stage through the normal Character Factory Unity integration.
mv "$FACE_FBX" "$RAW_FBX"
"$BLENDER_BIN" --background \
  --python tools/character-factory/ci/verify_skinned_character.py -- \
  --input "$RAW_FBX"

"$BLENDER_BIN" --background \
  --python tools/character-factory/ci/render_pipeline_artifact.py -- \
  --input "$RAW_FBX" \
  --output "$OUT/madeline_body_01.render.png" \
  --preserve-materials

test -s "$OUT/madeline_body_01.render.png"

python3 tools/character-factory/character_factory.py stage-unity \
  "$OUT/manifest.json" \
  --assets-root Assets/Generated/CharacterFactory

printf '%s\n' \
  "Madeline base body built and staged." \
  "Body: $RAW_FBX" \
  "Preview: $OUT/madeline_body_01.render.png" \
  "Unity: Assets/Generated/CharacterFactory/character/madeline_body_01"
