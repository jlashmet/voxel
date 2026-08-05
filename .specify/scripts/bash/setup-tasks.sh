#!/usr/bin/env bash
# Resolves the active feature directory and reports which design documents exist,
# for the /speckit-tasks workflow. Feature directory comes from .specify/feature.json.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
FEATURE_JSON="$REPO_ROOT/.specify/feature.json"

if [[ ! -f "$FEATURE_JSON" ]]; then
  echo "ERROR: $FEATURE_JSON not found. Run /speckit-specify first." >&2
  exit 1
fi

FEATURE_DIR_REL="$(sed -n 's/.*"feature_directory"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$FEATURE_JSON")"
FEATURE_DIR="$REPO_ROOT/$FEATURE_DIR_REL"
TASKS_TEMPLATE="$REPO_ROOT/.specify/templates/tasks-template.md"

if [[ ! -f "$FEATURE_DIR/plan.md" ]]; then
  echo "ERROR: $FEATURE_DIR/plan.md not found. Run /speckit-plan first." >&2
  exit 1
fi

DOCS=()
for doc in spec.md plan.md research.md data-model.md quickstart.md architecture-notes.md; do
  [[ -f "$FEATURE_DIR/$doc" ]] && DOCS+=("$doc")
done
[[ -d "$FEATURE_DIR/contracts" ]] && DOCS+=("contracts/")
[[ -d "$FEATURE_DIR/checklists" ]] && DOCS+=("checklists/")

[[ -f "$TASKS_TEMPLATE" ]] || TASKS_TEMPLATE=""

if [[ "${1:-}" == "--json" ]]; then
  printf '{"FEATURE_DIR":"%s","TASKS_TEMPLATE":"%s","AVAILABLE_DOCS":[' "$FEATURE_DIR" "$TASKS_TEMPLATE"
  for i in "${!DOCS[@]}"; do
    [[ $i -gt 0 ]] && printf ','
    printf '"%s"' "${DOCS[$i]}"
  done
  printf ']}\n'
else
  echo "FEATURE_DIR=$FEATURE_DIR"
  echo "TASKS_TEMPLATE=$TASKS_TEMPLATE"
  echo "AVAILABLE_DOCS=${DOCS[*]}"
fi
