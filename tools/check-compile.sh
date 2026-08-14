#!/usr/bin/env bash
#
# Offline compile check.
#
# Type-checks every project assembly with the Roslyn compiler Unity ships, against the
# reference assemblies already on disk. It never launches the editor, so it is safe to run
# while the developer has one open — which matters here, because tools/unity-run.sh must
# refuse in that case and a full test run is otherwise the only way to learn that a change
# does not compile.
#
# This is a type check, not a test run. It cannot execute anything: NativeArray and the
# Allocator family need Unity's native runtime, so the tests themselves still require
# tools/unity-run.sh. What this catches is the class of error that otherwise costs a whole
# Unity round trip to discover — missing usings, signature drift, bad references.
#
# Usage:  tools/check-compile.sh [assembly-name-filter]
#
# Exits non-zero if any assembly fails to compile.

set -uo pipefail

PROJECT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$PROJECT"

UNITY_ROOT="${UNITY_ROOT:-/Applications/Unity/Hub/Editor/6000.5.6f1}"
BCL="$UNITY_ROOT/Unity.app/Contents/Resources/Scripting/MonoBleedingEdge/lib/mono/unityaot-macos"
MANAGED="$UNITY_ROOT/Unity.app/Contents/Resources/Scripting/Managed/UnityEngine"
DOTNET="$UNITY_ROOT/Unity.app/Contents/Resources/Scripting/DotNetSdk/dotnet"
CSC="$(ls "$UNITY_ROOT"/Unity.app/Contents/Resources/Scripting/DotNetSdk/sdk/*/Roslyn/bincore/csc.dll 2>/dev/null | head -1)"
NUNIT="$(ls "$PROJECT"/Library/PackageCache/com.unity.ext.nunit@*/net472/unity-custom/nunit.framework.dll 2>/dev/null | head -1)"

for required in "$DOTNET" "$CSC" "$MANAGED" "$BCL"; do
    if [ ! -e "$required" ]; then
        echo "check-compile: missing $required" >&2
        echo "check-compile: set UNITY_ROOT if your editor lives elsewhere" >&2
        exit 2
    fi
done

OUT="$(mktemp -d)"
trap 'rm -rf "$OUT"' EXIT
FILTER="${1:-}"

# Assemblies built from source here. Their prebuilt copies in Library/ScriptAssemblies are
# excluded from the reference set so a stale DLL can never shadow the sources under test.
ASSEMBLIES=(
    "VoxelEngine.Core:Assets/VoxelEngine/Core"
    "VoxelEngine.Vegetation:Assets/VoxelEngine/Vegetation"
    "VoxelEngine.Tiering:Assets/VoxelEngine/Tiering"
    "VoxelEngine.Collision:Assets/VoxelEngine/Collision"
    "VoxelEngine.Structures:Assets/VoxelEngine/Structures"
    "VoxelEngine.Streaming:Assets/VoxelEngine/Streaming"
    "VoxelEngine.Rendering:Assets/VoxelEngine/Rendering"
    "VoxelEngine.Net:Assets/VoxelEngine/Net"
    "VoxelEngine.Showcase:Assets/Scenes/Showcase"
    "VoxelEngine.CI.Editor:Assets/VoxelEngine/CI/Editor"
    "VoxelEngine.CI.PlayMode:Assets/VoxelEngine/CI/PlayMode"
    "VoxelEngine.Tests.EditMode:Assets/Tests/EditMode"
    "VoxelEngine.Tests.PlayMode:Assets/Tests/PlayMode"
)

rebuilt_names() { for entry in "${ASSEMBLIES[@]}"; do echo "${entry%%:*}"; done; }

failed=0
for entry in "${ASSEMBLIES[@]}"; do
    name="${entry%%:*}"
    src="${entry##*:}"
    [ -n "$FILTER" ] && [[ "$name" != *"$FILTER"* ]] && continue
    [ -d "$src" ] || { echo "### $name  SKIPPED (no $src)"; continue; }

    rsp="$OUT/$name.rsp"
    {
        echo "-nologo"; echo "-target:library"; echo "-langversion:9"
        echo "-unsafe"; echo "-noconfig"; echo "-nostdlib+"
        echo "-nowarn:CS0169,CS0414,CS0649,CS0436"
        echo "-out:$OUT/$name.dll"

        for dll in "$BCL"/mscorlib.dll "$BCL"/System.dll "$BCL"/System.Core.dll \
                   "$BCL"/Facades/netstandard.dll; do
            echo "-r:$dll"
        done
        # UnityEngine and UnityEditor modules. Referencing the monolithic UnityEditor.dll
        # alongside these duplicates every editor type, so it is deliberately left out.
        for dll in "$MANAGED"/*.dll; do echo "-r:$dll"; done
        [ -n "$NUNIT" ] && echo "-r:$NUNIT"

        for dll in Library/ScriptAssemblies/*.dll; do
            base="$(basename "$dll" .dll)"
            rebuilt_names | grep -qx "$base" && continue
            echo "-r:$PROJECT/$dll"
        done
        # Assemblies already rebuilt this run, excluding this one: referencing your own
        # output duplicates every type in it.
        for dll in "$OUT"/*.dll; do
            [ -f "$dll" ] || continue
            [ "$(basename "$dll" .dll)" = "$name" ] && continue
            echo "-r:$dll"
        done

        find "$src" -name "*.cs" | sed "s|^|$PROJECT/|"
    } > "$rsp"

    errors="$("$DOTNET" "$CSC" "@$rsp" 2>&1 | grep -E "error CS" | head -25)"
    if [ -n "$errors" ]; then
        echo "### $name"
        echo "$errors"
        failed=1
    else
        echo "### $name  OK"
    fi
done

exit $failed
