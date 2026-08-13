#!/usr/bin/env bash
set -euo pipefail

project_root=$(cd "$(dirname "$0")/.." && pwd)
exchange="$project_root/Artifacts/ArchLookdev"
state="$exchange/state.json"
command="$exchange/command.json"
mkdir -p "$exchange"

usage() {
  echo "usage: tools/arch-lookdev.sh state"
  echo "       tools/arch-lookdev.sh capture [name]"
  echo "       tools/arch-lookdev.sh sweep <voussoirs|joint|bevel|moss>"
  echo "       tools/arch-lookdev.sh apply <settings.json>"
  echo "       tools/arch-lookdev.sh save | load"
}

require_state() {
  if [[ ! -f "$state" ]]; then
    echo "arch-lookdev: no running scene state at $state" >&2
    echo "Open Assets/Scenes/ArchLookdev.unity and enter Play Mode." >&2
    exit 2
  fi
}

request_id="cli-$(date +%s)-$$"
action=${1:-}
case "$action" in
  state)
    require_state
    jq . "$state"
    ;;
  capture)
    require_state
    name=${2:-candidate}
    jq -n --arg id "$request_id" --arg name "$name" \
      '{requestId:$id, action:"capture", name:$name}' > "$command"
    echo "arch-lookdev: requested capture '$name' ($request_id)"
    ;;
  sweep)
    require_state
    axis=${2:-}
    case "$axis" in voussoirs|joint|bevel|moss) ;; *) usage; exit 2 ;; esac
    jq -n --arg id "$request_id" --arg axis "$axis" \
      '{requestId:$id, action:"sweep", sweepAxis:$axis}' > "$command"
    echo "arch-lookdev: requested $axis sweep ($request_id)"
    ;;
  apply)
    require_state
    preset=${2:-}
    if [[ ! -f "$preset" ]]; then
      echo "arch-lookdev: settings file not found: $preset" >&2
      exit 2
    fi
    jq -n --arg id "$request_id" --slurpfile input "$preset" \
      '{requestId:$id, action:"apply", settings:($input[0].settings // $input[0])}' > "$command"
    echo "arch-lookdev: requested settings apply ($request_id)"
    ;;
  save|load)
    require_state
    jq -n --arg id "$request_id" --arg action "$action" \
      '{requestId:$id, action:$action}' > "$command"
    echo "arch-lookdev: requested $action ($request_id)"
    ;;
  *)
    usage
    exit 2
    ;;
esac
