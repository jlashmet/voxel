#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Usage:
  bash tools/showcase-player-capture.sh --unity <Unity binary> --output <artifact dir> \
    [--scene <scene.unity> | --test-filter <Unity test filter>] [options]

Builds the selected Unity scene as a real macOS player. Normal visual capture records actual
presented frames every 10 seconds. Stationary benchmark mode instead holds the production scene
still after convergence, measures without screenshots, then captures one presented frame afterward.

Options:
  --unity PATH             Unity executable (required)
  --output DIR             Artifact root (required)
  --scene PATH             Scene to build
  --test-filter FILTER     Resolve a known screenshot/benchmark test to its real scene
  --if-configured          Exit successfully when FILTER is not a configured real-player test
  --run-seconds N          Player run duration / stationary timeout
  --autowalk-after N       Enable the showcase scripted walk after N seconds
  --converging-builds N    Override only visible-convergence voxel build concurrency
  --survey-after N         Enable showcase survey camera after N seconds
  --survey-height N        Survey camera height
  --survey-spin N          Survey spin degrees/second
  --stationary-sample N    Measure N settled seconds with no motion or screenshots
EOF
}

UNITY_PATH=""
OUTPUT_ROOT=""
SCENE=""
TEST_FILTER=""
RUN_SECONDS=""
AUTOWALK_AFTER=""
CONVERGING_BUILDS=""
SURVEY_AFTER=""
SURVEY_HEIGHT=""
SURVEY_SPIN=""
STATIONARY_SAMPLE=""
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
    --converging-builds) CONVERGING_BUILDS="$2"; shift 2 ;;
    --survey-after) SURVEY_AFTER="$2"; shift 2 ;;
    --survey-height) SURVEY_HEIGHT="$2"; shift 2 ;;
    --survey-spin) SURVEY_SPIN="$2"; shift 2 ;;
    --stationary-sample) STATIONARY_SAMPLE="$2"; shift 2 ;;
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
    VoxelEngine.Tests.PlayMode.StationaryRenderBenchmarkTests.SmallVoxelShowcaseMovingBuild12)
      SCENE="Assets/Scenes/SmallVoxelShowcase.unity"
      : "${RUN_SECONDS:=90}"
      : "${AUTOWALK_AFTER:=20}"
      : "${CONVERGING_BUILDS:=12}"
      ;;
    VoxelEngine.Tests.PlayMode.StationaryRenderBenchmarkTests.SmallVoxelShowcaseMovingBuild8)
      SCENE="Assets/Scenes/SmallVoxelShowcase.unity"
      : "${RUN_SECONDS:=90}"
      : "${AUTOWALK_AFTER:=20}"
      : "${CONVERGING_BUILDS:=8}"
      ;;
    VoxelEngine.Tests.PlayMode.StationaryRenderBenchmarkTests|VoxelEngine.Tests.PlayMode.StationaryRenderBenchmarkTests.*)
      SCENE="Assets/Scenes/VoxelShowcase.unity"
      : "${RUN_SECONDS:=120}"
      : "${STATIONARY_SAMPLE:=10}"
      ;;
    VoxelEngine.Tests.PlayMode.ShowcaseTraversalPerformanceTests.ContinuousPlayerTraversalNeverStuttersOrOpensNearFarGap)
      # The PlayMode version is valuable for coverage/blocking assertions and now gets a bounded
      # multi-worker pool in tests-single.yml so it can reach movement. Frame timing still belongs
      # to the production VoxelShowcase real player rather than the Editor test loop.
      SCENE="Assets/Scenes/VoxelShowcase.unity"
      : "${RUN_SECONDS:=150}"
      : "${AUTOWALK_AFTER:=60}"
      # Diagnostic A/B for the measured job-pool starvation hypothesis. This changes only the
      # visible-convergence ceiling (12 -> 8); the production converged ceiling remains zero.
      : "${CONVERGING_BUILDS:=8}"
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
        echo "No real-player screenshot/benchmark profile for '$TEST_FILTER'; skipping capture."
        exit 0
      fi
      echo "ERROR: no real-player screenshot/benchmark profile for '$TEST_FILTER'." >&2
      exit 2
      ;;
  esac
fi

[[ -n "$UNITY_PATH" ]] || { echo "ERROR: --unity is required." >&2; exit 2; }
[[ -x "$UNITY_PATH" ]] || { echo "ERROR: Unity is not executable: $UNITY_PATH" >&2; exit 2; }
[[ -n "$OUTPUT_ROOT" ]] || { echo "ERROR: --output is required." >&2; exit 2; }
[[ -n "$SCENE" ]] || { echo "ERROR: --scene or --test-filter is required." >&2; exit 2; }
[[ -f "$SCENE" ]] || { echo "ERROR: scene does not exist: $SCENE" >&2; exit 2; }
if [[ -n "$STATIONARY_SAMPLE" ]]; then
  : "${RUN_SECONDS:=120}"
  if [[ -n "$AUTOWALK_AFTER" || -n "$SURVEY_AFTER" ]]; then
    echo "ERROR: stationary sampling cannot be combined with autowalk or survey motion." >&2
    exit 2
  fi
else
  : "${RUN_SECONDS:=30}"
fi

BUILD_DIR="$OUTPUT_ROOT/Player"
SHOTS_DIR="$OUTPUT_ROOT/Screenshots"
BUILD_LOG="$OUTPUT_ROOT/player-build.log"
PLAYER_LOG="$OUTPUT_ROOT/player-run.log"
FPS_LOG="$OUTPUT_ROOT/fps.txt"
STATIONARY_LOG="$OUTPUT_ROOT/stationary.txt"

cleanup() {
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
mkdir -p "$OUTPUT_ROOT" "$BUILD_DIR"
if [[ -z "$STATIONARY_SAMPLE" ]]; then mkdir -p "$SHOTS_DIR"; fi

wait_for_unity_quiet

BUILD_ARGS=(-batchmode -nographics -quit)
if [[ -n "$STATIONARY_SAMPLE" ]]; then BUILD_ARGS+=(-voxelFrameTimingStats); fi

echo "Building real player for $SCENE"
UNITY_MAX_RSS_MB="${UNITY_MAX_RSS_MB:-12288}" \
UNITY_MAX_MINUTES="${UNITY_MAX_MINUTES:-25}" \
UNITY_BIN="$UNITY_PATH" tools/unity-run.sh \
  "${BUILD_ARGS[@]}" \
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
  -voxel-uncapped
)

if [[ -n "$STATIONARY_SAMPLE" ]]; then
  PLAYER_ARGS+=(
    -voxel-stationary-sample-seconds "$STATIONARY_SAMPLE"
    -voxel-stationary-timeout-seconds "$RUN_SECONDS"
    -voxel-stationary-screenshot-dir "$SHOTS_DIR"
  )
  echo "Running stationary benchmark for ${STATIONARY_SAMPLE}s after convergence; screenshot after measurement"
else
  PLAYER_ARGS+=(
    -voxel-fps-log
    -voxel-run-seconds "$RUN_SECONDS"
    -voxel-screenshot-dir "$SHOTS_DIR"
    -voxel-screenshot-every 10
  )

  if [[ -n "$AUTOWALK_AFTER" ]]; then
    PLAYER_ARGS+=( -voxel-autowalk-after "$AUTOWALK_AFTER" )
  fi
  if [[ -n "$CONVERGING_BUILDS" ]]; then
    PLAYER_ARGS+=( -voxel-converging-builds "$CONVERGING_BUILDS" )
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
  if [[ -n "$CONVERGING_BUILDS" ]]; then
    echo "Real-player converging build ceiling override: $CONVERGING_BUILDS (converged remains 0)"
  fi
fi

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

if [[ -n "$STATIONARY_SAMPLE" ]]; then
  if [[ -s "$PLAYER_LOG" ]]; then
    grep 'STATIONARY result=' "$PLAYER_LOG" > "$STATIONARY_LOG" || true
  fi
  if ! grep -q 'STATIONARY result=PASS' "$STATIONARY_LOG" 2>/dev/null; then
    echo "ERROR: stationary benchmark did not publish a passing result." >&2
    tail -80 "$PLAYER_LOG" >&2 || true
    exit 1
  fi
  if [[ ! -s "$SHOTS_DIR/stationary-final.png" ]]; then
    echo "ERROR: stationary benchmark produced no post-measurement screenshot." >&2
    tail -80 "$PLAYER_LOG" >&2 || true
    exit 1
  fi
  cat "$STATIONARY_LOG"
  echo "stationary post-measurement screenshot: $SHOTS_DIR/stationary-final.png"
  exit 0
fi

if [[ -s "$PLAYER_LOG" ]]; then
  grep 'FPSLOG' "$PLAYER_LOG" > "$FPS_LOG" || true
fi

if [[ -s "$FPS_LOG" ]]; then
  echo "=== REAL PLAYER FPS TAIL ==="
  tail -20 "$FPS_LOG"
fi

if [[ -s "$PLAYER_LOG" ]] && grep -q 'PREPARESECTIONS' "$PLAYER_LOG"; then
  echo "=== REAL PLAYER PREPARE SECTIONS ==="
  grep 'PREPARESECTIONS' "$PLAYER_LOG" | tail -30
fi

if [[ -s "$PLAYER_LOG" ]] && grep -q 'SURFACE t=' "$PLAYER_LOG"; then
  echo "=== REAL PLAYER SURFACE TAIL ==="
  grep 'SURFACE t=' "$PLAYER_LOG" | tail -30
fi
if [[ -s "$PLAYER_LOG" ]] && grep -q 'RINGS ' "$PLAYER_LOG"; then
  echo "=== REAL PLAYER RINGS TAIL ==="
  grep 'RINGS ' "$PLAYER_LOG" | tail -30
fi

shots="$(find "$SHOTS_DIR" -name '*.png' -size +1k | wc -l | tr -d ' ')"
echo "real-player screenshots captured: $shots"
if (( shots < 2 )); then
  echo "ERROR: expected at least 2 real-player screenshots, found $shots." >&2
  echo "A runner without a logged-in window server can launch the player but render no screenshots." >&2
  exit 1
fi
