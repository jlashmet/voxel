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
# Every invocation also owns a fresh process session. If Unity crashes on its own (for example
# inside Burst/LLVM), helpers can otherwise be reparented and poison the next isolated test run.
# Session cleanup makes a natural editor crash obey the same lifecycle boundary as a watchdog kill.
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
#   UNITY_MAX_SWAP_GROWTH_MB kill if swap grows this much during a run    (default 512)
#   UNITY_ALLOW_CONCURRENT  set to 1 to bypass guard 1 — think first
#   UNITY_BIN               path to the Unity binary

set -uo pipefail

MAX_RSS_MB=${UNITY_MAX_RSS_MB:-6144}
MAX_MINUTES=${UNITY_MAX_MINUTES:-6}
MIN_FREE_MB=${UNITY_MIN_FREE_MB:-4096}
FLOOR_FREE_MB=${UNITY_FLOOR_FREE_MB:-8192}
MAX_SWAP_GROWTH_MB=${UNITY_MAX_SWAP_GROWTH_MB:-512}
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

swap_used_mb() {
  local value
  value=$(sysctl vm.swapusage 2>/dev/null | awk '{for(i=1;i<=NF;i++) if($i=="used") {gsub(/M/,"",$(i+2)); print int($(i+2));}}')
  echo "${value:-0}"
}

initial_swap_mb=$(swap_used_mb)

if (( free_mb < MIN_FREE_MB )); then
  echo "unity-run: REFUSING — only ${free_mb} MB free, need ${MIN_FREE_MB} MB." >&2
  exit 4
fi

echo "unity-run: starting (${free_mb} MB free, rss ceiling ${MAX_RSS_MB} MB, free floor ${FLOOR_FREE_MB} MB, swap-growth ceiling ${MAX_SWAP_GROWTH_MB} MB, limit ${MAX_MINUTES} min)"

# -- run under a watchdog -----------------------------------------------------

# Non-interactive bash does not give background jobs their own process group. Start Unity in a
# fresh session explicitly so helpers remain attributable to this invocation even if the editor
# itself crashes and they are reparented before the watchdog can walk the old parent tree.
python3 - "$UNITY_BIN" "$@" <<'PY' &
import os
import sys

os.setsid()
os.execv(sys.argv[1], sys.argv[1:])
PY
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

# Kill every descendant, deepest-first, before killing Unity itself. A direct `pkill -P`
# reaches only one generation. Shader/import/licensing helpers can have their own children;
# if Unity dies first those descendants may be reparented and continue consuming memory after
# the safety guard reports success.
kill_tree() {
  local root=$1
  local pids=("$root")
  local found=1

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

  for (( i=${#pids[@]}-1; i>0; i-- )); do
    kill -9 "${pids[$i]}" 2>/dev/null || true
  done
  kill -9 "$root" 2>/dev/null || true
}

# The session id/process-group id is the original launcher pid because the Python wrapper calls
# setsid() before exec'ing Unity. This catches helpers that survived a natural Unity crash and
# were reparented, which kill_tree can no longer discover once the root is gone.
kill_session() {
  local root=$1
  kill -9 -- "-$root" 2>/dev/null || true
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
  swap_now=$(swap_used_mb)
  swap_growth=$(( swap_now - initial_swap_mb ))

  # Written every poll so a run that takes the machine down still leaves evidence.
  echo "elapsed=${elapsed}s rss=${rss}MB peak=${peak}MB systemFree=${system_free}MB swapGrowth=${swap_growth}MB" > "$status_file"

  if (( swap_growth > MAX_SWAP_GROWTH_MB )); then
    echo "unity-run: KILLING — swap grew ${swap_growth} MB (ceiling ${MAX_SWAP_GROWTH_MB} MB)" >&2
    kill_tree "$unity_pid"
    kill_session "$unity_pid"
    wait "$unity_pid" 2>/dev/null
    exit 8
  fi

  # The guard that actually matters. RSS missed a 200 GB run entirely; free memory did not.
  if (( system_free < FLOOR_FREE_MB )); then
    echo "unity-run: KILLING — system free memory fell to ${system_free} MB (floor ${FLOOR_FREE_MB} MB)" >&2
    kill_tree "$unity_pid"
    kill_session "$unity_pid"
    wait "$unity_pid" 2>/dev/null
    exit 7
  fi

  if (( rss > MAX_RSS_MB )); then
    echo "unity-run: KILLING — process tree hit ${rss} MB (ceiling ${MAX_RSS_MB} MB)" >&2
    kill_tree "$unity_pid"
    kill_session "$unity_pid"
    wait "$unity_pid" 2>/dev/null
    exit 5
  fi

  if (( elapsed > MAX_MINUTES * 60 )); then
    echo "unity-run: KILLING — ran ${elapsed}s (limit $(( MAX_MINUTES * 60 ))s), peak ${peak} MB" >&2
    kill_tree "$unity_pid"
    kill_session "$unity_pid"
    wait "$unity_pid" 2>/dev/null
    exit 6
  fi

  sleep 0.1
done

wait "$unity_pid"
status=$?
# Natural crashes bypass the watchdog branches above. Reap any helpers still owned by this
# invocation before returning so the next isolated Unity run does not see a phantom editor.
kill_session "$unity_pid"

echo "unity-run: finished with status ${status}, peak ${peak} MB, $(( $(date +%s) - start ))s"
exit $status