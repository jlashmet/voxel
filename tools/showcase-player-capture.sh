#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Usage:
  bash tools/showcase-player-capture.sh --unity <Unity binary> --output <artifact dir> \
    [--scene <scene.unity> | --test-filter <Unity test filter>] [options]

Builds the selected Unity scene as a real macOS player, launches that player, and captures
actual presented frames every 10 seconds. Visual Unity tests can be supplied by test filter;
known screenshot suites are mapped to their production scene automatically.

Options:
  --unity PATH             Unity executable (required)
  --output DIR             Artifact root (required)
  --scene PATH             Scene to build
  --test-filter FILTER     Resolve a known screenshot test to its real scene
  --if-configured          Exit successfully when FILTER is not a screenshot test
  --run-seconds N          Player run duration (default: profile-specific or 30)
  --autowalk-after N       Enable the showcase scripted walk after N seconds
  --survey-after N         Enable showcase survey camera after N seconds
  --survey-height N        Survey camera height
  --survey-spin N          Survey spin degrees/second
EOF
}

UNITY_PATH=""
OUTPUT_ROOT=""
SCENE=""
TEST_FILTER=""
RUN_SECONDS=""
AUTOWALK_AFTER=""
SURVEY_AFTER=""
SURVEY_HEIGHT=""
SURVEY_SPIN=""
IF_CONFIGURED=0

while (( $# > 0 )); do
  case "$1" in
    --unity) UNITY_PATH="$2"; shift 2 ;;
    --output) OUTPUT_ROOT="$2"; shift 2 ;;
    --scene) SCENE="$2"; shift 2 ;;
    --test-filter) TEST_FILTER="$2"; shift 2 ;;
    --if-configured) IF_CONFIGURED=1; shift ;;
    --run-seconds) RUN_SECONDS="$2"; shift 2 ;;
    --autowalk-after) AUTOWALK_AFTER="$2"; shift 2 ;;
    --survey-after) SURVEY_AFTER="$2"; shift 2 ;;
    --survey-height) SURVEY_HEIGHT="$2"; shift 2 ;;
    --survey-spin) SURVEY_SPIN="$2"; shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "ERROR: unknown argument '$1'" >&2; usage >&2; exit 2 ;;
  esac
done

if [[ -n "$TEST_FILTER" ]]; then
  if [[ -n "$SCENE" ]]; then
    echo "ERROR: use either --scene or --test-filter, not both." >&2
    exit 2
  fi

  case "$TEST_FILTER" in
    VoxelEngine.Tests.PlayMode.ArchLookdevSceneTests|VoxelEngine.Tests.PlayMode.ArchLookdevSceneTests.*)
      SCENE="Assets/Scenes/ArchLookdev.unity"
      : "${RUN_SECONDS:=30}"
      ;;
    VoxelEngine.Tests.PlayMode.TerrainLookdevScreenshotTests|VoxelEngine.Tests.PlayMode.TerrainLookdevScreenshotTests.*)
      SCENE="Assets/Scenes/TerrainLookdev.unity"
      : "${RUN_SECONDS:=30}"
      ;;
    VoxelEngine.Tests.PlayMode.KentridgePlayableScenePlayTests|VoxelEngine.Tests.PlayMode.KentridgePlayableScenePlayTests.*)
      # Kentridge is an integration scene, so its visual proof must come from the same standalone
      # player path as the showcase benchmarks rather than a PlayMode RenderTexture. Let the real
      # opening render first, then switch to the scene's own survey driver so later ten-second
      # frames prove Kentridge, Hightown, corridor life, near terrain and far terrain together.
      SCENE="Assets/Scenes/KentridgePlayableSlice.unity"
      : "${RUN_SECONDS:=60}"
      : "${SURVEY_AFTER:=10}"
      : "${SURVEY_HEIGHT:=55}"
      : "${SURVEY_SPIN:=30}"
      ;;
    VoxelEngine.Tests.PlayMode.CastleScreenshotTests|VoxelEngine.Tests.PlayMode.CastleScreenshotTests.*|\
    VoxelEngine.Tests.PlayMode.CastleExteriorLookdevTests|VoxelEngine.Tests.PlayMode.CastleExteriorLookdevTests.*)
      SCENE="Assets/Scenes/VoxelShowcase.unity"
      : "${RUN_SECONDS:=60}"
      : "${SURVEY_AFTER:=10}"
      : "${SURVEY_HEIGHT:=55}"
      : "${SURVEY_SPIN:=30}"
      ;;
    *)
      if (( IF_CONFIGURED )); then
        echo "No real-player screenshot profile for '$TEST_FILTER'; skipping capture."
        exit 0
      fi
      echo "ERROR: no real-player screenshot profile for '$TEST_FILTER'." >&2
      exit 2
      ;;
  esac
fi

[[ -n "$UNITY_PATH" ]] || { echo "ERROR: --unity is required." >&2; exit 2; }
[[ -x "$UNITY_PATH" ]] || { echo "ERROR: Unity is not executable: $UNITY_PATH" >&2; exit 2; }
[[ -n "$OUTPUT_ROOT" ]] || { echo "ERROR: --output is required." >&2; exit 2; }
[[ -n "$SCENE" ]] || { echo "ERROR: --scene or --test-filter is required." >&2; exit 2; }
[[ -f "$SCENE" ]] || { echo "ERROR: scene does not exist: $SCENE" >&2; exit 2; }
: "${RUN_SECONDS:=30}"

BUILD_DIR="$OUTPUT_ROOT/Player"
SHOTS_DIR="$OUTPUT_ROOT/Screenshots"
BUILD_LOG="$OUTPUT_ROOT/player-build.log"
PLAYER_LOG="$OUTPUT_ROOT/player-run.log"
FPS_LOG="$OUTPUT_ROOT/fps.txt"

cleanup() {
  # The .app is an execution intermediate, not a useful CI artifact. In particular the single-test
  # workflow uploads its artifact root recursively, so retaining the bundle would turn a handful of
  # screenshots into a hundreds-of-megabytes artifact.
  rm -rf "$BUILD_DIR"
}
trap cleanup EXIT

wait_for_unity_quiet() {
  local deadline=$((SECONDS + 900))
  while pgrep -f '/Unity.app/Contents/MacOS/Unity' >/dev/null 2>&1; do
    if (( SECONDS >= deadline )); then
      echo "ERROR: Unity did not become idle before real-player build." >&2
      pgrep -alf '/Unity.app/Contents/MacOS/Unity' >&2 || true
      return 1
    fi
    sleep 5
  done
}

rm -rf "$BUILD_DIR" "$SHOTS_DIR"
mkdir -p "$OUTPUT_ROOT" "$BUILD_DIR" "$SHOTS_DIR"

# A preceding bake or PlayMode run can return before the macOS Unity process disappears from the
# process table. tools/unity-run.sh intentionally refuses concurrent editors, so make the shared
# capture utility sequencing-safe instead of racing the previous Unity invocation.
wait_for_unity_quiet

echo "Building real player for $SCENE"
UNITY_MAX_RSS_MB="${UNITY_MAX_RSS_MB:-12288}" \
UNITY_MAX_MINUTES="${UNITY_MAX_MINUTES:-25}" \
UNITY_BIN="$UNITY_PATH" tools/unity-run.sh \
  -batchmode -nographics -quit \
  -projectPath "$PWD" \
  -executeMethod VoxelEngine.Showcase.Editor.ShowcasePlayerBuild.Build \
  -voxelScene "$SCENE" \
  -voxelBuildOutput "$BUILD_DIR" \
  -logFile "$BUILD_LOG"

APP="$(find "$BUILD_DIR" -maxdepth 1 -type d -name '*.app' -print -quit)"
[[ -n "$APP" && -d "$APP" ]] || {
  echo "ERROR: player build produced no .app under $BUILD_DIR" >&2
  exit 1
}

APP_BIN_DIR="$APP/Contents/MacOS"
BIN="$(find "$APP_BIN_DIR" -maxdepth 1 -type f -perm -111 -print -quit)"
[[ -n "$BIN" && -x "$BIN" ]] || {
  echo "ERROR: no executable found in $APP_BIN_DIR" >&2
  exit 1
}

PLAYER_ARGS=(
  -logFile "$PLAYER_LOG"
  -screen-width 1600 -screen-height 900 -screen-fullscreen 0
  -voxel-uncapped -voxel-fps-log
  -voxel-run-seconds "$RUN_SECONDS"
  -voxel-screenshot-dir "$SHOTS_DIR"
  -voxel-screenshot-every 10
)

if [[ -n "$AUTOWALK_AFTER" ]]; then
  PLAYER_ARGS+=( -voxel-autowalk-after "$AUTOWALK_AFTER" )
fi
if [[ -n "$SURVEY_AFTER" ]]; then
  PLAYER_ARGS+=( -voxel-survey-after "$SURVEY_AFTER" )
fi
if [[ -n "$SURVEY_HEIGHT" ]]; then
  PLAYER_ARGS+=( -voxel-survey-height "$SURVEY_HEIGHT" )
fi
if [[ -n "$SURVEY_SPIN" ]]; then
  PLAYER_ARGS+=( -voxel-survey-spin "$SURVEY_SPIN" )
fi

echo "Running real player for ${RUN_SECONDS}s; screenshots every 10s"
"$BIN" "${PLAYER_ARGS[@]}" &
PID=$!

( sleep $(( ${RUN_SECONDS%.*} + 120 )); kill -9 "$PID" 2>/dev/null || true ) &
WATCHDOG=$!
status=0
wait "$PID" || status=$?
kill "$WATCHDOG" 2>/dev/null || true
wait "$WATCHDOG" 2>/dev/null || true

echo "player exit status: $status"
if (( status != 0 )); then
  exit "$status"
fi

if [[ -s "$PLAYER_LOG" ]]; then
  grep 'FPSLOG' "$PLAYER_LOG" > "$FPS_LOG" || true
fi

shots="$(find "$SHOTS_DIR" -name '*.png' -size +1k | wc -l | tr -d ' ')"
echo "real-player screenshots captured: $shots"
if (( shots < 2 )); then
  echo "ERROR: expected at least 2 real-player screenshots, found $shots." >&2
  echo "A runner without a logged-in window server can launch the player but render no screenshots." >&2
  exit 1
fi
