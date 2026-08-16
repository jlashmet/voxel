#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
chmod +x "$SCRIPT_DIR/bootstrap_hunyuan_quality_macos.sh" "$SCRIPT_DIR/build.sh"
export HUNYUAN_PY="$("$SCRIPT_DIR/bootstrap_hunyuan_quality_macos.sh" | tail -n 1)"
exec "$SCRIPT_DIR/build.sh" "$@"
