#!/usr/bin/env bash
set -euo pipefail

usage() {
  echo "Usage: tools/showcase-bake-cache.sh restore|store <ShowcaseWorld.bytes>" >&2
  exit 2
}

[[ $# == 2 ]] || usage
ACTION=$1
OUTPUT=$2
MANIFEST="${OUTPUT%.bytes}.manifest.txt"

# Request commits and SceneIssue evidence do not affect the generated semantic world. Hash the
# tracked source inputs broadly enough to prefer a harmless cache miss over a stale bake hit.
fingerprint="$({
  git ls-files -s -- \
    ProjectSettings/ProjectVersion.txt \
    Packages/manifest.json \
    Packages/packages-lock.json \
    Packages/com.mountingforce.worldgen \
    Assets/Scenes/VoxelShowcase.unity \
    Assets/Scenes/Showcase \
    Assets/Game/Composition \
    Assets/Game/WorldBuilder \
    Assets/VoxelEngine
} | grep -v 'Assets/Resources/VoxelShowcase/ShowcaseWorld.bytes$' | shasum -a 256 | awk '{print $1}')"

[[ -n "$fingerprint" ]] || { echo "ERROR: could not fingerprint showcase bake inputs" >&2; exit 2; }
CACHE_BASE=${VOXEL_SHOWCASE_BAKE_CACHE:-${RUNNER_TOOL_CACHE:-${HOME}/.cache}/voxel-showcase-bakes}
CACHE_FILE="$CACHE_BASE/$fingerprint.bytes"
CACHE_MANIFEST="$CACHE_BASE/$fingerprint.manifest.txt"

case "$ACTION" in
  restore)
    if [[ ! -s "$CACHE_FILE" || ! -s "$CACHE_MANIFEST" ]]; then
      echo "Showcase bake cache miss: $fingerprint"
      exit 1
    fi
    mkdir -p "$(dirname "$OUTPUT")"
    cp "$CACHE_FILE" "$OUTPUT"
    cp "$CACHE_MANIFEST" "$MANIFEST"
    echo "Restored ShowcaseWorld.bytes + provenance manifest from cache: $fingerprint"
    ;;
  store)
    [[ -s "$OUTPUT" ]] || { echo "ERROR: showcase bake output is missing: $OUTPUT" >&2; exit 2; }
    [[ -s "$MANIFEST" ]] || { echo "ERROR: showcase bake manifest is missing: $MANIFEST" >&2; exit 2; }
    mkdir -p "$CACHE_BASE"
    temporary="$CACHE_FILE.tmp.$$"
    temporary_manifest="$CACHE_MANIFEST.tmp.$$"
    cp "$OUTPUT" "$temporary"
    cp "$MANIFEST" "$temporary_manifest"
    mv "$temporary" "$CACHE_FILE"
    mv "$temporary_manifest" "$CACHE_MANIFEST"
    echo "Stored ShowcaseWorld.bytes + provenance manifest in cache: $fingerprint"
    ;;
  *) usage ;;
esac
