#!/usr/bin/env bash
#
# Bake the worldbuilding gallery, then build and measure it as a standalone player.
#
# The gallery cannot be measured without baking first: its startup path deliberately refuses to
# author the district during play, so a player built against a missing or stale bake throws on the
# first frame instead of producing a number.
#
# Both steps need a raised memory ceiling. The scene asks for the PC-tier brick pool, which is
# reserved up front — around 5 GB before Unity's own footprint — and the wrapper's 6 GB default
# would kill a perfectly healthy bake partway through.
#
# Usage:
#   tools/gallery-measure.sh [run-seconds] [autowalk-after]
#   tools/gallery-measure.sh 180 90

set -uo pipefail

RUN_SECONDS="${1:-150}"
AUTOWALK_AFTER="${2:-60}"

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SCENE="WorldbuildingGalleryShowcase"
BAKE_LOG="$ROOT/Artifacts/Measure/$SCENE-bake.log"

mkdir -p "$(dirname "$BAKE_LOG")"

echo "== baking $SCENE world"
UNITY_MAX_RSS_MB=${UNITY_MAX_RSS_MB:-20480} UNITY_MAX_MINUTES=${UNITY_MAX_MINUTES:-45} \
"$ROOT/tools/unity-run.sh" -batchmode -nographics -quit \
  -projectPath "$ROOT" \
  -executeMethod VoxelEngine.Showcase.Editor.ShowcaseWorldBaker.BakeWorldbuildingGalleryWorld \
  -logFile "$BAKE_LOG" >/dev/null 2>&1
bake_status=$?

if (( bake_status != 0 )); then
  echo "bake failed ($bake_status); see $BAKE_LOG" >&2
  grep -E "error CS|Exception|Baked|refusing" "$BAKE_LOG" | tail -20 >&2
  exit "$bake_status"
fi
grep -E "Baked Worldbuilding Gallery" "$BAKE_LOG" | tail -1

UNITY_MAX_RSS_MB=${UNITY_MAX_RSS_MB:-20480} \
exec "$ROOT/tools/showcase-measure.sh" "$SCENE" "$RUN_SECONDS" "$AUTOWALK_AFTER"
