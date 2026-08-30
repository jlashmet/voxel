#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Usage:
  bash tools/showcase-player-capture.sh --unity <Unity binary> --output <artifact dir> \
    [--scene <scene.unity> | --scene-issue <SceneIssues/.../issue.json>] [options]

Generic standalone-player build/capture mechanism. Feature/module policy belongs in declarative
scenario metadata or SceneIssue capture metadata, never in scene/test-name branches here.

Options:
  --unity PATH
  --output DIR
  --scene PATH
  --scene-issue PATH
  --run-seconds N
  --width N
  --height N
  --screenshot-every N
  --minimum-frames N
  --auto-dialogue N
  --autowalk-after N
  --converging-builds N
  --survey-after N
  --survey-height N
  --survey-spin N
  --stationary-sample N
  --require-log-pattern TEXT
  --forbid-log-pattern TEXT
EOF
}

UNITY_PATH=""
OUTPUT_ROOT=""
SCENE=""
RUN_SECONDS=""
AUTO_DIALOGUE=""
AUTOWALK_AFTER=""
CONVERGING_BUILDS=""
SURVEY_AFTER=""
SURVEY_HEIGHT=""
SURVEY_SPIN=""
STATIONARY_SAMPLE=""
SCENE_ISSUE=""
ISSUE_CAPTURE_COUNT=0
PLAYER_WIDTH=1600
PLAYER_HEIGHT=900
SCREENSHOT_EVERY=10
MINIMUM_FRAMES=2
REQUIRED_LOG_PATTERNS_FILE=""
FORBIDDEN_LOG_PATTERNS_FILE=""

append_pattern() {
  local kind="$1"
  local value="$2"
  local file
  if [[ "$kind" == required ]]; then
    file="${REQUIRED_LOG_PATTERNS_FILE:-${TMPDIR:-/tmp}/voxel-required-log-patterns-$$}"
    REQUIRED_LOG_PATTERNS_FILE="$file"
  else
    file="${FORBIDDEN_LOG_PATTERNS_FILE:-${TMPDIR:-/tmp}/voxel-forbidden-log-patterns-$$}"
    FORBIDDEN_LOG_PATTERNS_FILE="$file"
  fi
  printf '%s\n' "$value" >> "$file"
}

while (( $# > 0 )); do
  case "$1" in
    --unity) UNITY_PATH="$2"; shift 2 ;;
    --output) OUTPUT_ROOT="$2"; shift 2 ;;
    --scene) SCENE="$2"; shift 2 ;;
    --scene-issue) SCENE_ISSUE="$2"; shift 2 ;;
    --run-seconds) RUN_SECONDS="$2"; shift 2 ;;
    --width) PLAYER_WIDTH="$2"; shift 2 ;;
    --height) PLAYER_HEIGHT="$2"; shift 2 ;;
    --screenshot-every) SCREENSHOT_EVERY="$2"; shift 2 ;;
    --minimum-frames) MINIMUM_FRAMES="$2"; shift 2 ;;
    --auto-dialogue) AUTO_DIALOGUE="$2"; shift 2 ;;
    --autowalk-after) AUTOWALK_AFTER="$2"; shift 2 ;;
    --converging-builds) CONVERGING_BUILDS="$2"; shift 2 ;;
    --survey-after) SURVEY_AFTER="$2"; shift 2 ;;
    --survey-height) SURVEY_HEIGHT="$2"; shift 2 ;;
    --survey-spin) SURVEY_SPIN="$2"; shift 2 ;;
    --stationary-sample) STATIONARY_SAMPLE="$2"; shift 2 ;;
    --require-log-pattern) append_pattern required "$2"; shift 2 ;;
    --forbid-log-pattern) append_pattern forbidden "$2"; shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "ERROR: unknown argument '$1'" >&2; usage >&2; exit 2 ;;
  esac
done

cleanup_patterns() {
  [[ -z "$REQUIRED_LOG_PATTERNS_FILE" ]] || rm -f "$REQUIRED_LOG_PATTERNS_FILE"
  [[ -z "$FORBIDDEN_LOG_PATTERNS_FILE" ]] || rm -f "$FORBIDDEN_LOG_PATTERNS_FILE"
}
trap cleanup_patterns EXIT

if [[ -n "$SCENE_ISSUE" ]]; then
  if [[ -n "$SCENE" ]]; then
    echo "ERROR: use either --scene or --scene-issue, not both." >&2
    exit 2
  fi
  case "$SCENE_ISSUE" in
    SceneIssues/open/*/issue.json|SceneIssues/pending/*/issue.json|SceneIssues/closed/*/issue.json|/*) ;;
    *) echo "ERROR: invalid --scene-issue path." >&2; exit 2 ;;
  esac
  if [[ "$SCENE_ISSUE" != /* ]]; then SCENE_ISSUE="$PWD/$SCENE_ISSUE"; fi
  [[ -f "$SCENE_ISSUE" ]] || { echo "ERROR: scene issue does not exist: $SCENE_ISSUE" >&2; exit 2; }
  ISSUE_METADATA="$(python3 - "$SCENE_ISSUE" <<'PY'
import json, sys
from pathlib import Path
path=Path(sys.argv[1])
value=json.loads(path.read_text(encoding='utf-8'))
scene=value.get('scenePath') or ''
if not isinstance(scene,str) or not scene.startswith('Assets/') or not scene.endswith('.unity'):
    raise SystemExit('ERROR: scene issue has no valid scenePath')
frames=value.get('captures') or []
if not isinstance(frames,list):
    raise SystemExit('ERROR: scene issue captures must be an array')
if frames:
    first=frames[0]
    width=first.get('screenWidth') or value.get('screenWidth') or 0
    height=first.get('screenHeight') or value.get('screenHeight') or 0
else:
    width=value.get('screenWidth') or 1600
    height=value.get('screenHeight') or 900
if not isinstance(width,int) or not isinstance(height,int) or width <= 0 or height <= 0:
    raise SystemExit('ERROR: scene issue has invalid screen dimensions')
print(f'{scene}\t{width}\t{height}\t{len(frames)}')
PY
)"
  IFS=$'\t' read -r SCENE PLAYER_WIDTH PLAYER_HEIGHT ISSUE_CAPTURE_COUNT <<< "$ISSUE_METADATA"
fi

[[ -n "$UNITY_PATH" && -x "$UNITY_PATH" ]] || { echo "ERROR: --unity must be an executable." >&2; exit 2; }
[[ -n "$OUTPUT_ROOT" ]] || { echo "ERROR: --output is required." >&2; exit 2; }
[[ -n "$SCENE" && -f "$SCENE" && "$SCENE" == *.unity ]] || { echo "ERROR: a valid --scene or --scene-issue is required." >&2; exit 2; }

validate_positive_int() {
  local value="$1" name="$2"
  [[ "$value" =~ ^[0-9]+$ && "$value" -gt 0 ]] || { echo "ERROR: $name must be a positive integer." >&2; exit 2; }
}
validate_positive_int "$PLAYER_WIDTH" width
validate_positive_int "$PLAYER_HEIGHT" height
validate_positive_int "$SCREENSHOT_EVERY" screenshot-every
validate_positive_int "$MINIMUM_FRAMES" minimum-frames
: "${RUN_SECONDS:=30}"
if [[ ! "$RUN_SECONDS" =~ ^[0-9]+([.][0-9]+)?$ ]]; then echo "ERROR: run-seconds must be numeric." >&2; exit 2; fi
if [[ -n "$STATIONARY_SAMPLE" && ( -n "$AUTOWALK_AFTER" || -n "$SURVEY_AFTER" ) ]]; then
  echo "ERROR: stationary sampling cannot be combined with movement." >&2
  exit 2
fi

BUILD_DIR="$OUTPUT_ROOT/Player"
SHOTS_DIR="$OUTPUT_ROOT/Screenshots"
BUILD_LOG="$OUTPUT_ROOT/player-build.log"
PLAYER_LOG="$OUTPUT_ROOT/player-run.log"
FPS_LOG="$OUTPUT_ROOT/fps.txt"
STATIONARY_LOG="$OUTPUT_ROOT/stationary.txt"

cleanup_build() { rm -rf "$BUILD_DIR"; }
trap 'cleanup_build; cleanup_patterns' EXIT

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
wait_for_unity_quiet

BUILD_ARGS=(-batchmode -nographics -quit)
if [[ -n "$STATIONARY_SAMPLE" ]]; then BUILD_ARGS+=(-voxelFrameTimingStats); fi

echo "Building real player for $SCENE"
UNITY_MAX_RSS_MB="${UNITY_MAX_RSS_MB:-12288}" UNITY_MAX_MINUTES="${UNITY_MAX_MINUTES:-25}" \
UNITY_BIN="$UNITY_PATH" tools/unity-run.sh \
  "${BUILD_ARGS[@]}" -projectPath "$PWD" \
  -executeMethod VoxelEngine.Showcase.Editor.ShowcasePlayerBuild.Build \
  -voxelScene "$SCENE" -voxelBuildOutput "$BUILD_DIR" -logFile "$BUILD_LOG"

APP="$(find "$BUILD_DIR" -maxdepth 1 -type d -name '*.app' -print -quit)"
[[ -n "$APP" && -d "$APP" ]] || { echo "ERROR: player build produced no .app." >&2; exit 1; }
BIN="$(find "$APP/Contents/MacOS" -maxdepth 1 -type f -perm -111 -print -quit)"
[[ -n "$BIN" && -x "$BIN" ]] || { echo "ERROR: player build produced no executable." >&2; exit 1; }

PLAYER_ARGS=(-logFile "$PLAYER_LOG" -screen-width "$PLAYER_WIDTH" -screen-height "$PLAYER_HEIGHT" -screen-fullscreen 0 -voxel-uncapped)
if [[ -n "$SCENE_ISSUE" ]]; then PLAYER_ARGS+=( -voxel-scene-issue "$SCENE_ISSUE" ); fi
if [[ -n "$STATIONARY_SAMPLE" ]]; then
  PLAYER_ARGS+=( -voxel-stationary-sample-seconds "$STATIONARY_SAMPLE" -voxel-stationary-timeout-seconds "$RUN_SECONDS" -voxel-stationary-screenshot-dir "$SHOTS_DIR" )
else
  PLAYER_ARGS+=( -voxel-fps-log -voxel-run-seconds "$RUN_SECONDS" -voxel-screenshot-dir "$SHOTS_DIR" -voxel-screenshot-every "$SCREENSHOT_EVERY" )
  [[ -z "$AUTO_DIALOGUE" ]] || PLAYER_ARGS+=( -voxel-auto-dialogue "$AUTO_DIALOGUE" )
  [[ -z "$AUTOWALK_AFTER" ]] || PLAYER_ARGS+=( -voxel-autowalk-after "$AUTOWALK_AFTER" )
  [[ -z "$CONVERGING_BUILDS" ]] || PLAYER_ARGS+=( -voxel-converging-builds "$CONVERGING_BUILDS" )
  [[ -z "$SURVEY_AFTER" ]] || PLAYER_ARGS+=( -voxel-survey-after "$SURVEY_AFTER" )
  [[ -z "$SURVEY_HEIGHT" ]] || PLAYER_ARGS+=( -voxel-survey-height "$SURVEY_HEIGHT" )
  [[ -z "$SURVEY_SPIN" ]] || PLAYER_ARGS+=( -voxel-survey-spin "$SURVEY_SPIN" )
fi

echo "Running real player for ${RUN_SECONDS}s"
"$BIN" "${PLAYER_ARGS[@]}" &
PID=$!
( sleep $(( ${RUN_SECONDS%.*} + 120 )); kill -9 "$PID" 2>/dev/null || true ) &
WATCHDOG=$!
status=0
wait "$PID" || status=$?
kill "$WATCHDOG" 2>/dev/null || true
wait "$WATCHDOG" 2>/dev/null || true
(( status == 0 )) || exit "$status"

if [[ -n "$STATIONARY_SAMPLE" ]]; then
  grep 'STATIONARY result=' "$PLAYER_LOG" > "$STATIONARY_LOG" || true
  grep -q 'STATIONARY result=PASS' "$STATIONARY_LOG" || { echo "ERROR: stationary benchmark did not pass." >&2; exit 1; }
  [[ -s "$SHOTS_DIR/stationary-final.png" ]] || { echo "ERROR: stationary benchmark produced no screenshot." >&2; exit 1; }
else
  grep 'FPSLOG' "$PLAYER_LOG" > "$FPS_LOG" 2>/dev/null || true
fi

if [[ -n "$REQUIRED_LOG_PATTERNS_FILE" ]]; then
  while IFS= read -r pattern; do
    grep -Fq -- "$pattern" "$PLAYER_LOG" || { echo "ERROR: required player-log pattern missing: $pattern" >&2; exit 1; }
  done < "$REQUIRED_LOG_PATTERNS_FILE"
fi
if [[ -n "$FORBIDDEN_LOG_PATTERNS_FILE" ]]; then
  while IFS= read -r pattern; do
    if grep -Fq -- "$pattern" "$PLAYER_LOG"; then echo "ERROR: forbidden player-log pattern found: $pattern" >&2; exit 1; fi
  done < "$FORBIDDEN_LOG_PATTERNS_FILE"
fi

shots="$(find "$SHOTS_DIR" -type f -name '*.png' -size +1k | wc -l | tr -d ' ')"
echo "real-player screenshots captured: $shots"
if (( shots < MINIMUM_FRAMES )); then
  echo "ERROR: expected at least $MINIMUM_FRAMES real-player screenshot(s), found $shots." >&2
  exit 1
fi

if [[ -n "$SCENE_ISSUE" ]]; then
  if (( ISSUE_CAPTURE_COUNT > 0 )) && ! grep -q 'SCENEISSUE camera pinned' "$PLAYER_LOG" 2>/dev/null; then
    echo "ERROR: scene-issue player never confirmed the recorded camera was pinned." >&2
    exit 1
  fi
  FINAL_SHOT="$(find "$SHOTS_DIR" -type f -name '*.png' -size +1k | sort | tail -1)"
  SHOT_WIDTH="$(sips -g pixelWidth "$FINAL_SHOT" 2>/dev/null | awk '/pixelWidth:/ {print $2}')"
  SHOT_HEIGHT="$(sips -g pixelHeight "$FINAL_SHOT" 2>/dev/null | awk '/pixelHeight:/ {print $2}')"
  if [[ -z "$SHOT_WIDTH" || -z "$SHOT_HEIGHT" ]] || (( SHOT_WIDTH < PLAYER_WIDTH || SHOT_HEIGHT < PLAYER_HEIGHT )); then
    echo "ERROR: scene-issue verification frame is smaller than requested capture dimensions." >&2
    exit 1
  fi
  cp "$FINAL_SHOT" "$OUTPUT_ROOT/verification-final.png"
fi
