#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../../.." && pwd)"
cd "$REPO_ROOT"

BLENDER_BIN="${BLENDER_BIN:-/Applications/Blender.app/Contents/MacOS/Blender}"
OUT="${1:-$REPO_ROOT/Artifacts/SunlitClericRobeProduction}"
UNITY_ASSETS_ROOT="${2:-$REPO_ROOT/Assets/Generated/CharacterFactory}"
VIEWS="$SCRIPT_DIR/views"
ROBE_VIEWS="$OUT/references/geometry"

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

rm -rf "$OUT"
mkdir -p "$ROBE_VIEWS"
export PYTORCH_ENABLE_MPS_FALLBACK="${PYTORCH_ENABLE_MPS_FALLBACK:-1}"
export PYTHONUNBUFFERED=1

printf '%s\n' '[1/4] Resolve the managed Hunyuan production environment'
HUNYUAN_PY="$(
  python3 tools/character-factory/character_factory.py \
    bootstrap-profile hunyuan-quality-macos | tail -n 1
)"
test -x "$HUNYUAN_PY"

printf '%s\n' '[2/4] Derive clothing-only four-view references with the generic T-pose garment preprocessor'
"$HUNYUAN_PY" tools/character-factory/ci/prepare_tpose_garment_views.py \
  --views "$VIEWS" \
  --output "$ROBE_VIEWS"
for view in front back left right; do
  test -s "$ROBE_VIEWS/$view.png"
done

printf '%s\n' '[3/4] Create the canonical skeleton and garment weight donor'
CANONICAL="$OUT/canonical_female_with_robe.glb"
CANONICAL_BODY_PREVIEW="$OUT/canonical_female.input.png"
CANONICAL_ROBE_PREVIEW="$OUT/canonical_robe.input.png"
"$BLENDER_BIN" --background --python-exit-code 1 \
  --python tools/character-factory/ci/create_canonical_character_fixture.py -- \
  --canonical "$CANONICAL" \
  --input "$CANONICAL_BODY_PREVIEW" \
  --garment-input "$CANONICAL_ROBE_PREVIEW"
test -s "$CANONICAL"

printf '%s\n' '[4/4] Produce, verify, render, and stage the swappable robe'
SPEC="$OUT/sunlit-cleric-robe.json"
cat > "$SPEC" <<JSON
{
  "id": "sunlit_cleric_robe_01",
  "assetType": "clothing",
  "tags": ["sunlit-cleric", "robe", "clothing"],
  "references": {
    "geometry": {
      "directory": "$ROBE_VIEWS"
    },
    "appearance": {
      "directory": "$ROBE_VIEWS"
    }
  },
  "appearance": {
    "strategy": "garment-multiview"
  },
  "outputDir": "$OUT",
  "generator": {
    "profile": "hunyuan-quality-macos",
    "seed": 12345,
    "steps": 5,
    "octreeResolution": 256,
    "numChunks": 16000,
    "removeBackground": false
  },
  "rig": {
    "blender": "$BLENDER_BIN",
    "canonicalBody": "$CANONICAL",
    "bodyObject": "GarmentDonor",
    "armatureObject": "Armature",
    "maxTransferDistance": 0.45
  },
  "runtimePart": {
    "slot": "Torso",
    "socketBoneName": null,
    "socketLocalPosition": [0, 0, 0],
    "socketLocalEulerAngles": [0, 0, 0],
    "socketLocalScale": [1, 1, 1]
  }
}
JSON

python3 tools/character-factory/character_factory.py produce "$SPEC" \
  --unity-assets-root "$UNITY_ASSETS_ROOT"

test -s "$OUT/sunlit_cleric_robe_01.fbx"
test -s "$OUT/sunlit_cleric_robe_01.basecolor.png"
test -s "$OUT/sunlit_cleric_robe_01.preview.png"
test -s "$OUT/reference-audit.json"

cat <<EOF
Sunlit Cleric robe production complete.
  Clothing FBX: $OUT/sunlit_cleric_robe_01.fbx
  Base-color atlas: $OUT/sunlit_cleric_robe_01.basecolor.png
  Preview: $OUT/sunlit_cleric_robe_01.preview.png
  Slot: Torso / SkinnedToCharacterSkeleton
  Appearance: garment-multiview
  Preprocessing: generic T-pose garment extraction
  Unity staging root: $UNITY_ASSETS_ROOT
EOF
