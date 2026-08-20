#!/usr/bin/env bash
#
# Build a showcase scene as a standalone player and measure it.
#
# Frame cost is only meaningful in a player. The batchmode diagnostic renders to a RenderTexture
# and never presents; the editor measures its own render loop alongside the game's. See
# ShowcasePlayerBuild and ShowcasePlayerHarness.
#
# Produces, under Artifacts/Measure/<scene>/:
#   fps.txt         one line per second: p50/p95/p99/max frame time
#   rings.txt       per-LOD residency and the live ring bands, once a second
#   Screenshots/    phase-tagged PNGs
#   player.log      the full player log
#
# Usage:
#   tools/showcase-measure.sh SmallVoxelShowcase [run-seconds] [autowalk-after]
#   tools/showcase-measure.sh VoxelShowcase 200 90

set -uo pipefail

SCENE="${1:?usage: showcase-measure.sh <SceneName> [run-seconds] [autowalk-after]}"
RUN_SECONDS="${2:-150}"
AUTOWALK_AFTER="${3:-60}"

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="$ROOT/Artifacts/Measure/$SCENE"
SHOTS="$OUT/Screenshots"
LOG="$OUT/player.log"

rm -rf "$OUT"
mkdir -p "$SHOTS"

echo "== building $SCENE"
UNITY_MAX_RSS_MB=${UNITY_MAX_RSS_MB:-14336} UNITY_MAX_MINUTES=${UNITY_MAX_MINUTES:-45} \
"$ROOT/tools/unity-run.sh" -batchmode -nographics -quit \
  -projectPath "$ROOT" \
  -executeMethod VoxelEngine.Showcase.Editor.ShowcasePlayerBuild.Build \
  -voxelScene "Assets/Scenes/$SCENE.unity" \
  -voxelBuildOutput "$ROOT/Artifacts/Player" \
  -logFile "$OUT/build.log" >/dev/null 2>&1
build_status=$?

if (( build_status != 0 )); then
  echo "build failed ($build_status); see $OUT/build.log" >&2
  grep -E "error CS|ShowcasePlayerBuild|Exception" "$OUT/build.log" | tail -20 >&2
  exit "$build_status"
fi
grep -E "ShowcasePlayerBuild (Succeeded|Failed)" "$OUT/build.log" | tail -1

APP="$ROOT/Artifacts/Player/$SCENE.app/Contents/MacOS"
BIN="$APP/$(ls "$APP" | head -1)"

echo "== running $SCENE for ${RUN_SECONDS}s, walking from ${AUTOWALK_AFTER}s"
"$BIN" -logFile "$LOG" \
  -screen-width 1600 -screen-height 900 -screen-fullscreen 0 \
  -voxel-uncapped -voxel-fps-log \
  -voxel-autowalk-after "$AUTOWALK_AFTER" \
  -voxel-run-seconds "$RUN_SECONDS" \
  -voxel-screenshot-dir "$SHOTS" \
  -voxel-screenshot-every 15 &
PID=$!

# The harness quits itself; this only covers a player that hangs before it can.
( sleep $(( RUN_SECONDS + 120 )); kill -9 $PID 2>/dev/null ) &
WATCHDOG=$!
wait $PID
run_status=$?
kill $WATCHDOG 2>/dev/null

grep 'FPSLOG' "$LOG" > "$OUT/fps.txt"
grep '^RINGS' "$LOG" > "$OUT/rings.txt"

echo "== $SCENE"
echo "-- ring residency (last)"
tail -1 "$OUT/rings.txt"
echo "-- frame cost (settled tail)"
tail -8 "$OUT/fps.txt"
echo "-- screenshots: $(find "$SHOTS" -name '*.png' -size +1k | wc -l | tr -d ' ')"
echo "-- player exit: $run_status"
exit "$run_status"
