#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../../.." && pwd)"
cd "$REPO_ROOT"

BLENDER_BIN="${BLENDER_BIN:-/Applications/Blender.app/Contents/MacOS/Blender}"
SOURCE="$REPO_ROOT/tools/character-factory/ci/fixtures/sunlit_cleric_staff.jpg"
OUT="${1:-$REPO_ROOT/Artifacts/SunlitClericStaffProduction}"
UNITY_ASSETS_ROOT="${2:-$REPO_ROOT/Assets/Generated/CharacterFactory}"
FULL_INPUT="$OUT/sunlit_cleric_staff_01.input.png"
ORNAMENT_INPUT="$OUT/sunlit_cleric_staff_01.ornament.png"

for required in "$BLENDER_BIN" "$SOURCE"; do
  test -e "$required" || {
    echo "Required input not found: $required" >&2
    exit 2
  }
done

rm -rf "$OUT"
mkdir -p "$OUT"
export PYTORCH_ENABLE_MPS_FALLBACK="${PYTORCH_ENABLE_MPS_FALLBACK:-1}"
export PYTHONUNBUFFERED=1

echo "[1/1] Produce the composed staff from declared preprocessing + production data"
SPEC="$OUT/sunlit-cleric-staff.json"
cat > "$SPEC" <<JSON
{
  "id": "sunlit_cleric_staff_01",
  "assetType": "weapon",
  "tags": ["sunlit-cleric", "staff", "weapon"],
  "preprocess": [
    {
      "strategy": "python-script",
      "script": "$SCRIPT_DIR/prepare_staff_source.py",
      "arguments": [
        "--input", "$SOURCE",
        "--output", "$FULL_INPUT"
      ],
      "outputs": ["$FULL_INPUT"]
    },
    {
      "strategy": "linear-terminal-detail",
      "input": "$FULL_INPUT",
      "output": "$ORNAMENT_INPUT",
      "axis": "vertical",
      "terminal": "min",
      "background": 128
    }
  ],
  "views": {
    "front": "$FULL_INPUT"
  },
  "references": {
    "details": {
      "ornament": "$ORNAMENT_INPUT"
    }
  },
  "outputDir": "$OUT",
  "generator": {
    "profile": "triposr-smoke-macos",
    "mcResolution": 320,
    "removeBackground": false
  },
  "rigid": {
    "blender": "$BLENDER_BIN",
    "composition": {
      "strategy": "generated-detail-shaft",
      "detailReference": "ornament",
      "totalLength": 1.8,
      "detailLength": 0.38,
      "shaftRadius": 0.024,
      "axis": "auto",
      "attachmentSide": "min",
      "overlap": 0.025
    }
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

python3 tools/character-factory/character_factory.py produce "$SPEC" \
  --unity-assets-root "$UNITY_ASSETS_ROOT"

test -s "$FULL_INPUT"
test -s "$ORNAMENT_INPUT"
test -s "$OUT/sunlit_cleric_staff_01.fbx"
test -s "$OUT/sunlit_cleric_staff_01.preview.png"
test -s "$OUT/sunlit_cleric_staff_01.rigid-contract.json"
test -s "$OUT/manifest.json"

cat <<EOF
Sunlit Cleric staff build complete.
  Weapon FBX: $OUT/sunlit_cleric_staff_01.fbx
  Preview: $OUT/sunlit_cleric_staff_01.preview.png
  Composition: generated-detail-shaft (ornament + procedural shaft)
  Preprocessing: declared asset-local source isolation + generic linear terminal extraction
  Generator profile: triposr-smoke-macos
  Slot: MainHand
  Socket bone: RightHand
  Unity staging root: $UNITY_ASSETS_ROOT
EOF
