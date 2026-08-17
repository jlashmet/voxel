#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../../.." && pwd)"
cd "$REPO_ROOT"

VIEWS="$SCRIPT_DIR/views"
OUT="${1:-$REPO_ROOT/Artifacts/SunlitClericProduction}"
UNITY_ASSETS_ROOT="${2:-$REPO_ROOT/Assets/Generated/CharacterFactory}"

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

echo "[1/1] Produce, verify, render, and stage the Sunlit Cleric"
SPEC="$OUT/sunlit-cleric.asset.json"
cat > "$SPEC" <<JSON
{
  "id": "sunlit_cleric_mv_01",
  "assetType": "character",
  "tags": ["sunlit-cleric", "character"],
  "references": {
    "geometry": { "directory": "$VIEWS" }
  },
  "outputDir": "$OUT",
  "generator": {
    "profile": "hunyuan-quality-macos",
    "seed": 12345,
    "removeBackground": false
  },
  "rig": {
    "profile": "canonical-humanoid-macos",
    "maxTransferDistance": 0.45
  }
}
JSON

python3 tools/character-factory/character_factory.py produce "$SPEC" \
  --unity-assets-root "$UNITY_ASSETS_ROOT"

BASE_FBX="$OUT/sunlit_cleric_mv_01.fbx"
ATLAS="$OUT/sunlit_cleric_mv_01.basecolor.png"
PREVIEW="$OUT/sunlit_cleric_mv_01.preview.png"
IDLE="$OUT/sunlit_cleric_mv_01.idle.png"
for required in "$BASE_FBX" "$ATLAS" "$PREVIEW" "$IDLE" "$OUT/reference-audit.json"; do
  test -s "$required"
done

cat <<EOF
Sunlit Cleric build complete through Character Factory produce.
  Animated FBX: $BASE_FBX
  Base-color atlas: $ATLAS
  Preview: $PREVIEW
  Idle: $IDLE
  Generator profile: hunyuan-quality-macos
  Rig profile: canonical-humanoid-macos
  Unity staging root: $UNITY_ASSETS_ROOT
EOF
