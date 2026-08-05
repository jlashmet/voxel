#!/usr/bin/env bash
# Resolves the active feature directory and emits the paths the /speckit-plan
# workflow needs. Feature directory comes from .specify/feature.json (written by
# /speckit-specify), NOT from the git branch name.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
FEATURE_JSON="$REPO_ROOT/.specify/feature.json"

if [[ ! -f "$FEATURE_JSON" ]]; then
  echo "ERROR: $FEATURE_JSON not found. Run /speckit-specify first." >&2
  exit 1
fi

FEATURE_DIR_REL="$(sed -n 's/.*"feature_directory"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$FEATURE_JSON")"
if [[ -z "$FEATURE_DIR_REL" ]]; then
  echo "ERROR: feature_directory missing from $FEATURE_JSON" >&2
  exit 1
fi

SPECS_DIR="$REPO_ROOT/$FEATURE_DIR_REL"
FEATURE_SPEC="$SPECS_DIR/spec.md"
IMPL_PLAN="$SPECS_DIR/plan.md"
TEMPLATE="$REPO_ROOT/.specify/templates/plan-template.md"

if [[ ! -f "$FEATURE_SPEC" ]]; then
  echo "ERROR: $FEATURE_SPEC not found." >&2
  exit 1
fi

if [[ ! -f "$IMPL_PLAN" && -f "$TEMPLATE" ]]; then
  cp "$TEMPLATE" "$IMPL_PLAN"
fi

if git -C "$REPO_ROOT" rev-parse --git-dir >/dev/null 2>&1; then
  BRANCH="$(git -C "$REPO_ROOT" rev-parse --abbrev-ref HEAD)"
else
  BRANCH="(not a git repository)"
fi

if [[ "${1:-}" == "--json" ]]; then
  printf '{"FEATURE_SPEC":"%s","IMPL_PLAN":"%s","SPECS_DIR":"%s","BRANCH":"%s"}\n' \
    "$FEATURE_SPEC" "$IMPL_PLAN" "$SPECS_DIR" "$BRANCH"
else
  echo "FEATURE_SPEC=$FEATURE_SPEC"
  echo "IMPL_PLAN=$IMPL_PLAN"
  echo "SPECS_DIR=$SPECS_DIR"
  echo "BRANCH=$BRANCH"
fi
