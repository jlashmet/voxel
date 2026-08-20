#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
cd "$REPO_ROOT"

if [ "$#" -lt 2 ]; then
  cat >&2 <<'EOF'
usage: tools/character-factory/new_asset_macos.sh <character|clothing|weapon|accessory> <id> [init_asset options]

examples:
  tools/character-factory/new_asset_macos.sh character steven_01 --tag main-cast
  tools/character-factory/new_asset_macos.sh clothing guard_tunic_01 --tag castle --tag guard
  tools/character-factory/new_asset_macos.sh weapon guard_sword_01 --tag castle --tag guard
EOF
  exit 2
fi

ASSET_TYPE="$1"
ASSET_ID="$2"
shift 2

case "$ASSET_TYPE" in
  character|clothing|weapon|accessory)
    # Character/clothing specs now carry the named canonical rig profile. The
    # shared donor is materialized only if production misses prepared geometry,
    # so creating an asset no longer needs Blender or any canonical file path.
    exec python3 tools/character-factory/init_asset.py \
      "$ASSET_TYPE" "$ASSET_ID" \
      "$@"
    ;;
  *)
    echo "unknown asset type: $ASSET_TYPE" >&2
    exit 2
    ;;
esac
