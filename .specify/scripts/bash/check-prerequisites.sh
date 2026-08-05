#!/usr/bin/env bash
# Verifies feature prerequisites and reports available design documents.
# Feature directory comes from .specify/feature.json, not the git branch.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
FEATURE_JSON="$REPO_ROOT/.specify/feature.json"

REQUIRE_TASKS=0
INCLUDE_TASKS=0
JSON=0
for arg in "$@"; do
  case "$arg" in
    --require-tasks) REQUIRE_TASKS=1 ;;
    --include-tasks) INCLUDE_TASKS=1 ;;
    --json) JSON=1 ;;
  esac
done

[[ -f "$FEATURE_JSON" ]] || { echo "ERROR: $FEATURE_JSON not found. Run /speckit-specify first." >&2; exit 1; }

FEATURE_DIR_REL="$(sed -n 's/.*"feature_directory"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$FEATURE_JSON")"
FEATURE_DIR="$REPO_ROOT/$FEATURE_DIR_REL"

[[ -f "$FEATURE_DIR/spec.md" ]] || { echo "ERROR: spec.md missing. Run /speckit-specify." >&2; exit 1; }
[[ -f "$FEATURE_DIR/plan.md" ]] || { echo "ERROR: plan.md missing. Run /speckit-plan." >&2; exit 1; }
if [[ $REQUIRE_TASKS -eq 1 && ! -f "$FEATURE_DIR/tasks.md" ]]; then
  echo "ERROR: tasks.md missing. Run /speckit-tasks." >&2; exit 1
fi

DOCS=()
for doc in spec.md plan.md research.md data-model.md quickstart.md architecture-notes.md device-matrix.md; do
  [[ -f "$FEATURE_DIR/$doc" ]] && DOCS+=("$doc")
done
[[ $INCLUDE_TASKS -eq 1 && -f "$FEATURE_DIR/tasks.md" ]] && DOCS+=("tasks.md")
[[ -d "$FEATURE_DIR/contracts" ]] && DOCS+=("contracts/")
[[ -d "$FEATURE_DIR/checklists" ]] && DOCS+=("checklists/")

if [[ $JSON -eq 1 ]]; then
  printf '{"FEATURE_DIR":"%s","AVAILABLE_DOCS":[' "$FEATURE_DIR"
  for i in "${!DOCS[@]}"; do [[ $i -gt 0 ]] && printf ','; printf '"%s"' "${DOCS[$i]}"; done
  printf ']}\n'
else
  echo "FEATURE_DIR=$FEATURE_DIR"
  echo "AVAILABLE_DOCS=${DOCS[*]}"
fi
