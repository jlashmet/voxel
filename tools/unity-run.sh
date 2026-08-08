#!/usr/bin/env bash
#
# Guarded Unity launcher.
#
# Every headless Unity run in this repo must go through this script. It exists because
# unguarded runs took the developer's machine down repeatedly: a second editor launched
# against a project copy, with a real graphics device and a few hundred megabytes of
# ComputeBuffer, while the developer's own editor was open. On a unified-memory Mac that is
# enough to freeze the whole system, and nothing in Unity stops it.
#
# Three guards, in order:
#
#   1. Refuse to start if another Unity editor is already running. Two editors is the
#      specific thing that caused the freezes.
#   2. Refuse to start if the machine does not have headroom to spare.
#   3. Watch the process tree and kill it if it exceeds a memory ceiling or a time limit.
#
# The watchdog is the load-bearing one: ulimit -v is unreliable on arm64 macOS, so the limit
# has to be enforced from outside by polling and killing.
#
# It watches *system free memory*, not just the process tree's RSS. A run once consumed 200 GB
# while `ps -o rss=` reported under 4 GB for the whole tree: macOS compresses and swaps, and RSS
# does not count either. Per-process accounting can be fooled; the amount of memory left on the
# machine cannot.
#
# The wall-clock limit matters as much as the memory ceiling. A generator that never allocates
# much but pegs a core for twenty minutes makes the machine unusable just as effectively as one
# that exhausts RAM, and the memory ceiling will never fire on it. Six minutes is longer than any
# legitimate run in this project so far.
#
# Usage:  tools/unity-run.sh -projectPath /tmp/foo -batchmode -quit ...
#
# Environment overrides:
#   UNITY_MAX_RSS_MB        memory ceiling for the whole process tree   (default 6144)
#   UNITY_MAX_MINUTES       wall-clock ceiling                          (default 6)
#   UNITY_MIN_FREE_MB       required free memory before starting        (default 4096)
#   UNITY_FLOOR_FREE_MB     kill if system free memory falls below this  (default 8192)
#   UNITY_ALLOW_CONCURRENT  set to 1 to bypass guard 1 — think first
#   UNITY_BIN               path to the Unity binary

set -uo pipefail

MAX_RSS_MB=${UNITY_MAX_RSS_MB:-6144}
MAX_MINUTES=${UNITY_MAX_MINUTES:-6}
MIN_FREE_MB=${UNITY_MIN_FREE_MB:-4096}
FLOOR_FREE_MB=${UNITY_FLOOR_FREE_MB:-8192}
UNITY_BIN=${UNITY_BIN:-/Applications/Unity/Hub/Editor/6000.5.6f1/Unity.app/Contents/MacOS/Unity}

if [[ ! -x "$UNITY_BIN" ]]; then
  echo "unity-run: no Unity binary at $UNITY_BIN" >&2
  exit 2
fi

# -- guard 1: no second editor ------------------------------------------------

existing=$(pgrep -f "Unity.app/Contents/MacOS/Unity" || true)
if [[ -n "$existing" && "${UNITY_ALLOW_CONCURRENT:-0}" != "1" ]]; then
  echo "unity-run: REFUSING — a Unity editor is already running (pids: $(echo "$existing" | tr '\n' ' '))." >&2
  echo "unity-run: two editors on one machine is what caused the freezes. Close it, or set" >&2
  echo "unity-run: UNITY_ALLOW_CONCURRENT=1 if you are certain there is headroom." >&2
  exit 3
fi

# -- guard 2: headroom --------------------------------------------------------

pagesize=$(sysctl -n hw.pagesize)

system_free_mb() {
  local free inactive
  free=$(vm_stat | awk '/Pages free/         {gsub(/\./,"",$3); print $3}')
  inactive=$(vm_stat | awk '/Pages inactive/ {gsub(/\./,"",$3); print $3}')
  echo $(( (free + inactive) * pagesize / 1048576 ))
}

free_mb=$(system_free_mb)

if (( free_mb < MIN_FREE_MB )); then
  echo "unity-run: REFUSING — only ${free_mb} MB free, need ${MIN_FREE_MB} MB." >&2
  exit 4
fi

echo "unity-run: starting (${free_mb} MB free, rss ceiling ${MAX_RSS_MB} MB, free floor ${FLOOR_FREE_MB} MB, limit ${MAX_MINUTES} min)"

# -- run under a watchdog -----------------------------------------------------

"$UNITY_BIN" "$@" &
unity_pid=$!

# Unity spawns helpers (asset import workers, the licensing client, shader compilers), and
# they are where the memory actually goes, so the ceiling applies to the whole tree.
tree_rss_mb() {
  local root=$1
  local pids=("$root")
  local total=0 found=1

  while (( found )); do
    found=0
    for pid in "${pids[@]}"; do
      while read -r child; do
        [[ -z "$child" ]] && continue
        if [[ ! " ${pids[*]} " =~ " ${child} " ]]; then
          pids+=("$child")
          found=1
        fi
      done < <(pgrep -P "$pid" 2>/dev/null || true)
    done
  done

  for pid in "${pids[@]}"; do
    local rss
    rss=$(ps -o rss= -p "$pid" 2>/dev/null | tr -d ' ')
    [[ -n "$rss" ]] && total=$(( total + rss ))
  done

  echo $(( total / 1024 ))
}

start=$(date +%s)
peak=0
status_file="${TMPDIR:-/tmp}/unity-run-status"
: > "$status_file"

while kill -0 "$unity_pid" 2>/dev/null; do
  rss=$(tree_rss_mb "$unity_pid")
  (( rss > peak )) && peak=$rss
  elapsed=$(( $(date +%s) - start ))

  system_free=$(system_free_mb)

  # Written every poll so a run that takes the machine down still leaves evidence.
  echo "elapsed=${elapsed}s rss=${rss}MB peak=${peak}MB systemFree=${system_free}MB" > "$status_file"

  # The guard that actually matters. RSS missed a 200 GB run entirely; free memory did not.
  if (( system_free < FLOOR_FREE_MB )); then
    echo "unity-run: KILLING — system free memory fell to ${system_free} MB (floor ${FLOOR_FREE_MB} MB)" >&2
    pkill -9 -P "$unity_pid" 2>/dev/null
    kill -9 "$unity_pid" 2>/dev/null
    wait "$unity_pid" 2>/dev/null
    exit 7
  fi

  if (( rss > MAX_RSS_MB )); then
    echo "unity-run: KILLING — process tree hit ${rss} MB (ceiling ${MAX_RSS_MB} MB)" >&2
    pkill -9 -P "$unity_pid" 2>/dev/null
    kill -9 "$unity_pid" 2>/dev/null
    wait "$unity_pid" 2>/dev/null
    exit 5
  fi

  if (( elapsed > MAX_MINUTES * 60 )); then
    echo "unity-run: KILLING — ran ${elapsed}s (limit $(( MAX_MINUTES * 60 ))s), peak ${peak} MB" >&2
    pkill -9 -P "$unity_pid" 2>/dev/null
    kill -9 "$unity_pid" 2>/dev/null
    wait "$unity_pid" 2>/dev/null
    exit 6
  fi

  sleep 0.5
done

wait "$unity_pid"
status=$?

echo "unity-run: finished with status ${status}, peak ${peak} MB, $(( $(date +%s) - start ))s"
exit $status
