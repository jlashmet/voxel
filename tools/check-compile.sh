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
EDITOR_CORE="$(find "$UNITY_ROOT/Unity.app/Contents" -type f -name 'UnityEditor.CoreModule.dll' 2>/dev/null | head -1)"

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

# Assemblies built from source here, discovered from the .asmdef files themselves rather than
# listed by hand: the layout is split into Api/Runtime pairs that move around, and a hand-kept
# list silently rots into nonsense — a stale entry globs two assemblies' sources into one and
# every shared type then reports CS0433 against its own prebuilt DLL.
#
# Each assembly owns the .cs files under its directory that no nearer .asmdef claims, and they
# are emitted in dependency order so each one compiles against outputs built earlier this run.
# Their prebuilt copies in Library/ScriptAssemblies are excluded from the reference set so a
# stale DLL can never shadow the sources under test.
MANIFEST="$OUT/assemblies.tsv"
python3 - "$PROJECT" "$MANIFEST" <<'PY' || exit 2
import json, os, re, sys

project, manifest = sys.argv[1], sys.argv[2]

defs = []
for root in ("Assets", "Packages"):
    for dirpath, _, filenames in os.walk(os.path.join(project, root)):
        for f in filenames:
            if f.endswith(".asmdef"):
                defs.append(os.path.join(dirpath, f))

by_dir, info, guids = {}, {}, {}
for path in sorted(defs):
    with open(path) as fh:
        data = json.load(fh)
    name = data["name"]
    d = os.path.dirname(path)
    by_dir[d] = name
    info[name] = {
        "dir": d,
        "refs": data.get("references") or [],
        "editor": "Editor" in (data.get("includePlatforms") or []),
        "sources": [],
    }
    meta = path + ".meta"
    if os.path.exists(meta):
        m = re.search(r"^guid:\s*(\w+)", open(meta).read(), re.M)
        if m:
            guids[m.group(1)] = name

# Nearest enclosing .asmdef owns the file.
for name in info:
    for dirpath, _, filenames in os.walk(info[name]["dir"]):
        owner = dirpath
        while owner not in by_dir:
            owner = os.path.dirname(owner)
        if by_dir[owner] != name:
            continue
        for f in filenames:
            if f.endswith(".cs"):
                info[name]["sources"].append(os.path.join(dirpath, f))

def resolve(ref):
    if ref.startswith("GUID:"):
        return guids.get(ref[5:])
    return ref if ref in info else None

order, state = [], {}
def visit(name):
    if state.get(name) == "done":
        return
    if state.get(name) == "active":   # a reference cycle Unity would reject anyway
        return
    state[name] = "active"
    for ref in info[name]["refs"]:
        target = resolve(ref)
        if target:
            visit(target)
    state[name] = "done"
    order.append(name)

for name in sorted(info):
    visit(name)

with open(manifest, "w") as out:
    for name in order:
        a = info[name]
        out.write("\t".join([name, "editor" if a["editor"] else "runtime",
                             os.path.relpath(a["dir"], project),
                             *sorted(a["sources"])]) + "\n")
PY

# Only the assemblies this run actually rebuilds are shadowed. A filtered run leans on the
# prebuilt DLLs of everything it skipped, which is the point of filtering — but those DLLs are
# whatever the editor last managed to emit, so an unfiltered run is the only trustworthy verdict.
rebuilt_names() { cut -f1 "$MANIFEST" | { [ -n "$FILTER" ] && grep -F "$FILTER" || cat; }; }

failed=0
while IFS=$'\t' read -r -a fields; do
    name="${fields[0]}"
    kind="${fields[1]}"
    src="${fields[2]}"
    sources=("${fields[@]:3}")
    [ -n "$FILTER" ] && [[ "$name" != *"$FILTER"* ]] && continue
    [ ${#sources[@]} -eq 0 ] && { echo "### $name  SKIPPED (no sources under $src)"; continue; }

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
        for dll in "$MANAGED"/*.dll; do echo "-r:$dll"; done
        # Editor-only assemblies need the modular UnityEditor reference. Do not also reference
        # the legacy monolithic UnityEditor.dll; doing both duplicates editor types.
        if [ "$kind" = editor ] && [ -n "$EDITOR_CORE" ]; then
            echo "-r:$EDITOR_CORE"
        fi
        [ -n "$NUNIT" ] && echo "-r:$NUNIT"

        for dll in Library/ScriptAssemblies/*.dll; do
            [ -f "$dll" ] || continue
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

        printf '%s\n' "${sources[@]}"
    } > "$rsp"

    errors="$("$DOTNET" "$CSC" "@$rsp" 2>&1 | grep -E "error CS" | head -25)"
    if [ -n "$errors" ]; then
        echo "### $name"
        echo "$errors"
        failed=1
    else
        echo "### $name  OK"
    fi
done < "$MANIFEST"

exit $failed
