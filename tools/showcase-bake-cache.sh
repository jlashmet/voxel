#!/usr/bin/env bash
set -euo pipefail

usage() {
  echo "Usage: tools/showcase-bake-cache.sh fingerprint | restore|store <ShowcaseWorld.bytes>" >&2
  exit 2
}

[[ $# -ge 1 && $# -le 2 ]] || usage
ACTION=$1
OUTPUT=${2:-}

# Request commits, tests, and presentation code do not affect the deterministic semantic world.
# The manifest is deliberately explicit and versioned so an authoritative dependency cannot drift
# into or out of the key invisibly.
INPUT_MANIFEST=${VOXEL_SHOWCASE_BAKE_INPUTS:-.github/showcase-bake-inputs.txt}
[[ -s "$INPUT_MANIFEST" ]] || {
  echo "ERROR: showcase bake input manifest is missing: $INPUT_MANIFEST" >&2
  exit 2
}

inputs=()
while IFS= read -r input || [[ -n "$input" ]]; do
  [[ -z "$input" || "$input" == \#* ]] && continue
  inputs+=("$input")
done < "$INPUT_MANIFEST"
(( ${#inputs[@]} > 0 )) || {
  echo "ERROR: showcase bake input manifest is empty: $INPUT_MANIFEST" >&2
  exit 2
}

fingerprint="$({
  printf 'voxel-showcase-semantic-bake-v2\n'
  shasum -a 256 "$INPUT_MANIFEST"
  git ls-files -s -- "${inputs[@]}"
} | shasum -a 256 | awk '{print $1}')"

[[ -n "$fingerprint" ]] || { echo "ERROR: could not fingerprint showcase bake inputs" >&2; exit 2; }
CACHE_BASE=${VOXEL_SHOWCASE_BAKE_CACHE:-${RUNNER_TOOL_CACHE:-${HOME}/.cache}/voxel-showcase-bakes}
CACHE_FILE="$CACHE_BASE/$fingerprint.bytes"

case "$ACTION" in
  fingerprint)
    [[ -z "$OUTPUT" ]] || usage
    echo "$fingerprint"
    ;;
  restore)
    [[ -n "$OUTPUT" ]] || usage
    if [[ ! -s "$CACHE_FILE" ]]; then
      echo "Showcase bake cache miss: $fingerprint"
      exit 1
    fi
    mkdir -p "$(dirname "$OUTPUT")"
    cp "$CACHE_FILE" "$OUTPUT"
    echo "Restored ShowcaseWorld.bytes from cache: $fingerprint"
    ;;
  store)
    [[ -n "$OUTPUT" ]] || usage
    [[ -s "$OUTPUT" ]] || { echo "ERROR: showcase bake output is missing: $OUTPUT" >&2; exit 2; }
    mkdir -p "$CACHE_BASE"
    temporary="$CACHE_FILE.tmp.$$"
    cp "$OUTPUT" "$temporary"
    mv "$temporary" "$CACHE_FILE"
    echo "Stored ShowcaseWorld.bytes in cache: $fingerprint"
    ;;
  *) usage ;;
esac
