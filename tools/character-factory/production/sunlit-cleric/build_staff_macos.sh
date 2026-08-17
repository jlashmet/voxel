#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../../.." && pwd)"
cd "$REPO_ROOT"

BLENDER_BIN="${BLENDER_BIN:-/Applications/Blender.app/Contents/MacOS/Blender}"
SOURCE="$REPO_ROOT/tools/character-factory/ci/fixtures/sunlit_cleric_staff.jpg"
OUT="${1:-$REPO_ROOT/Artifacts/SunlitClericStaffProduction}"
UNITY_ASSETS_ROOT="${2:-$REPO_ROOT/Assets/Generated/CharacterFactory}"
HEAD_OUT="$OUT/head-work"

for required in "$BLENDER_BIN" "$SOURCE"; do
  test -e "$required" || {
    echo "Required input not found: $required" >&2
    exit 2
  }
done

rm -rf "$OUT"
mkdir -p "$OUT" "$HEAD_OUT"
export PYTORCH_ENABLE_MPS_FALLBACK="${PYTORCH_ENABLE_MPS_FALLBACK:-1}"
export PYTHONUNBUFFERED=1

echo "[1/6] Bootstrap the pinned TripoSR profile"
TRIPOSR_PY="$(python3 tools/character-factory/character_factory.py \
  bootstrap-profile triposr-smoke-macos | tail -n 1)"
test -x "$TRIPOSR_PY"

echo "[2/6] Isolate the staff and its ornate sun head"
FULL_INPUT="$OUT/sunlit_cleric_staff_01.input.png"
HEAD_INPUT="$HEAD_OUT/sunlit_cleric_staff_head_work.input.png"
"$TRIPOSR_PY" tools/character-factory/ci/prepare_staff_fixture.py \
  --input "$SOURCE" \
  --output "$FULL_INPUT"
"$TRIPOSR_PY" tools/character-factory/ci/prepare_staff_head_crop.py \
  --input "$FULL_INPUT" \
  --output "$HEAD_INPUT"
test -s "$FULL_INPUT"
test -s "$HEAD_INPUT"

echo "[3/6] Build the runtime weapon product and detailed ornament work mesh"
FULL_SPEC="$OUT/sunlit-cleric-staff.json"
cat > "$FULL_SPEC" <<JSON
{
  "id": "sunlit_cleric_staff_01",
  "assetType": "weapon",
  "views": {
    "front": "$FULL_INPUT"
  },
  "outputDir": "$OUT",
  "generator": {
    "profile": "triposr-smoke-macos",
    "mcResolution": 320,
    "removeBackground": false
  },
  "rigid": {
    "blender": "$BLENDER_BIN"
  },
  "runtimePart": {
    "slot": "MainHand",
    "socketBoneName": "RightHand",
    "socketLocalPosition": [0, 0, 0],
    "socketLocalEulerAngles": [0, 0, 0],
    "socketLocalScale": [1, 1, 1]
  }
}
JSON
python3 tools/character-factory/character_factory.py build "$FULL_SPEC"
test -s "$OUT/sunlit_cleric_staff_01.fbx"
test -s "$OUT/manifest.json"

HEAD_SPEC="$HEAD_OUT/sunlit-cleric-staff-head-work.json"
cat > "$HEAD_SPEC" <<JSON
{
  "id": "sunlit_cleric_staff_head_work",
  "assetType": "weapon",
  "views": {
    "front": "$HEAD_INPUT"
  },
  "outputDir": "$HEAD_OUT",
  "generator": {
    "profile": "triposr-smoke-macos",
    "mcResolution": 320,
    "removeBackground": false
  },
  "rigid": {
    "blender": "$BLENDER_BIN"
  },
  "runtimePart": {
    "slot": "MainHand",
    "socketBoneName": "RightHand",
    "socketLocalPosition": [0, 0, 0],
    "socketLocalEulerAngles": [0, 0, 0],
    "socketLocalScale": [1, 1, 1]
  }
}
JSON
python3 tools/character-factory/character_factory.py build "$HEAD_SPEC"
test -s "$HEAD_OUT/sunlit_cleric_staff_head_work.raw.glb"

echo "[4/6] Assemble the detailed generated ornament with a clean procedural shaft"
COMPOSITE="$OUT/sunlit_cleric_staff_01.composite.fbx"
"$BLENDER_BIN" --background --python-exit-code 1 \
  --python tools/character-factory/runtime/blender_assemble_ornamented_staff.py -- \
  --input-head "$HEAD_OUT/sunlit_cleric_staff_head_work.raw.glb" \
  --output "$COMPOSITE" \
  --total-length 1.8 \
  --head-length 0.38 \
  --shaft-radius 0.024 \
  --axis auto \
  --attachment-side min
test -s "$COMPOSITE"

BASE_FBX="$OUT/sunlit_cleric_staff_01.fbx"
mv "$BASE_FBX" "$OUT/sunlit_cleric_staff_01.generator_only.fbx"
mv "$COMPOSITE" "$BASE_FBX"

echo "[5/6] Render the final separate weapon for visual review"
"$BLENDER_BIN" --background --python-exit-code 1 \
  --python tools/character-factory/ci/render_pipeline_artifact.py -- \
  --input "$BASE_FBX" \
  --output "$OUT/sunlit_cleric_staff_01.preview.png" \
  --preserve-materials
test -s "$OUT/sunlit_cleric_staff_01.preview.png"

echo "[6/6] Stage the staff as a swappable Unity CharacterPartAsset"
python3 tools/character-factory/character_factory.py stage-unity \
  "$OUT/manifest.json" \
  --assets-root "$UNITY_ASSETS_ROOT"

cat <<EOF
Sunlit Cleric staff build complete.
  Weapon FBX: $BASE_FBX
  Preview: $OUT/sunlit_cleric_staff_01.preview.png
  Generator profile: triposr-smoke-macos
  Slot: MainHand
  Socket bone: RightHand
  Unity staging root: $UNITY_ASSETS_ROOT
EOF
