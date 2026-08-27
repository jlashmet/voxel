#!/usr/bin/env bash
set -euo pipefail

usage() {
  echo "Usage: tools/showcase-player-cache.sh fingerprint <scene> <mode> [bake] | restore|store <fingerprint> <build-dir>" >&2
  exit 2
}

[[ $# -ge 1 ]] || usage
ACTION=$1
shift
CACHE_BASE=${VOXEL_SHOWCASE_PLAYER_CACHE:-${RUNNER_TOOL_CACHE:-${HOME}/.cache}/voxel-showcase-players}

case "$ACTION" in
  fingerprint)
    [[ $# -ge 2 && $# -le 3 ]] || usage
    scene=$1
    mode=$2
    bake=${3:-}
    [[ -f "$scene" ]] || { echo "ERROR: player scene is missing: $scene" >&2; exit 2; }
    case "$mode" in development|development-frame-timing|release|frame-timing) ;; *) usage ;; esac

    {
      printf 'voxel-showcase-player-v1\nscene=%s\nmode=%s\n' "$scene" "$mode"
      git ls-files -s -- Assets Packages ProjectSettings \
        | grep -v '^.*[[:space:]]Assets/Tests/' \
        | grep -v '[[:space:]]Assets/Resources/VoxelShowcase/ShowcaseWorld.bytes$'
      if [[ -n "$bake" && -s "$bake" ]]; then
        shasum -a 256 "$bake"
      else
        printf 'no-showcase-bake\n'
      fi
    } | shasum -a 256 | awk '{print $1}'
    ;;
  restore|store)
    [[ $# == 2 ]] || usage
    fingerprint=$1
    build_dir=$2
    [[ "$fingerprint" =~ ^[0-9a-f]{64}$ ]] || {
      echo "ERROR: invalid player cache fingerprint" >&2
      exit 2
    }
    cache_dir="$CACHE_BASE/$fingerprint"

    if [[ "$ACTION" == restore ]]; then
      app="$(find "$cache_dir" -maxdepth 1 -type d -name '*.app' -print -quit 2>/dev/null || true)"
      executable=""
      if [[ -n "$app" ]]; then
        executable="$(find "$app/Contents/MacOS" -maxdepth 1 -type f -perm -111 -print -quit 2>/dev/null || true)"
      fi
      if [[ -z "$app" || -z "$executable" ]]; then
        echo "Showcase player cache miss: $fingerprint"
        exit 1
      fi
      mkdir -p "$build_dir"
      cp -R "$cache_dir/." "$build_dir/"
      echo "Restored showcase player from cache: $fingerprint"
      exit 0
    fi

    app="$(find "$build_dir" -maxdepth 1 -type d -name '*.app' -print -quit 2>/dev/null || true)"
    [[ -n "$app" ]] || { echo "ERROR: player build contains no .app: $build_dir" >&2; exit 2; }
    mkdir -p "$CACHE_BASE"
    if [[ -d "$cache_dir" ]]; then
      echo "Showcase player already cached: $fingerprint"
      exit 0
    fi
    temporary="$CACHE_BASE/.tmp-$fingerprint-$$"
    mkdir "$temporary"
    cp -R "$build_dir/." "$temporary/"
    mv "$temporary" "$cache_dir"
    echo "Stored showcase player in cache: $fingerprint"
    ;;
  *) usage ;;
esac
