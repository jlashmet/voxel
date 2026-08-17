#!/usr/bin/env bash
# Compatibility wrapper for the old Madeline production entrypoint. Backend
# ownership now lives in the generic hunyuan-quality-macos profile/bootstrap.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../../.." && pwd)"
GENERIC_BOOTSTRAP="$REPO_ROOT/tools/character-factory/ci/bootstrap_hunyuan_quality_macos.sh"

test -f "$GENERIC_BOOTSTRAP"
bash "$GENERIC_BOOTSTRAP"
