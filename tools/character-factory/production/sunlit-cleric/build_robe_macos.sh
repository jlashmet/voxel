#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../../.." && pwd)"
cd "$REPO_ROOT"

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

rm -rf "$OUT"
mkdir -p "$OUT"
export PYTORCH_ENABLE_MPS_FALLBACK="${PYTORCH_ENABLE_MPS_FALLBACK:-1}"
export PYTHONUNBUFFERED=1

printf '%s\n' '[1/1] Produce the swappable robe from declared preprocessing + production data'
SPEC="$OUT/sunlit-cleric-robe.json"
cat > "$SPEC" <<JSON
{
  "id": "sunlit_cleric_robe_01",
  "assetType": "clothing",
  "tags": ["sunlit-cleric", "robe", "clothing"],
  "preprocess": [
    {
      "strategy": "tpose-garment-views",
      "inputDirectory": "$VIEWS",
      "outputDirectory": "$ROBE_VIEWS"
    }
  ],
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
    "profile": "canonical-humanoid-macos",
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
for view in front back left right; do
  test -s "$ROBE_VIEWS/$view.png"
done

cat <<EOF
Sunlit Cleric robe production complete.
  Clothing FBX: $OUT/sunlit_cleric_robe_01.fbx
  Base-color atlas: $OUT/sunlit_cleric_robe_01.basecolor.png
  Preview: $OUT/sunlit_cleric_robe_01.preview.png
  Slot: Torso / SkinnedToCharacterSkeleton
  Appearance: garment-multiview
  Preprocessing: declared generic T-pose garment extraction
  Generator profile: hunyuan-quality-macos
  Rig profile: canonical-humanoid-macos
  Unity staging root: $UNITY_ASSETS_ROOT
EOF
