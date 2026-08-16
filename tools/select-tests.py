#!/usr/bin/env python3
#
# Dependency-aware test selection.
#
# Answers one question: given a set of changed files, which test assemblies can possibly be
# affected? CI runs those and skips the rest.
#
# Selecting by file path does not work here and is the trap this script exists to avoid. A
# one-line edit to Assets/VoxelEngine/Storage/Api touches only the Storage directory, but
# twenty-one assemblies reference Storage.Api transitively. A path-filtered run would test
# Storage alone and report green while the rest of the engine went unbuilt. Affectedness is a
# property of the .asmdef reference graph, not of directory layout.
#
# So: changed file -> nearest enclosing .asmdef owns it -> every assembly that references that
# one, transitively -> of those, the test assemblies. The ownership and GUID-resolution rules
# are the same ones tools/check-compile.sh uses, deliberately: two different notions of which
# assembly owns a file would be worse than either alone.
#
# Two deliberate biases toward over-running:
#
#   1. A changed file that no .asmdef claims (ProjectSettings, Packages/manifest.json, a scene
#      outside an assembly directory, this script) selects everything. Such files can change
#      behaviour anywhere and there is no graph edge to follow.
#   2. The invariant suites always run. Determinism, boundary and renderer-parity tests assert
#      properties of the whole system, so "nothing they cover changed" is never safe to assume.
#
# Usage:
#   tools/select-tests.py --base origin/master          # diff against a base ref
#   tools/select-tests.py --changed a.cs b.cs           # explicit file list
#   tools/select-tests.py --all                         # every test assembly
#   tools/select-tests.py --base origin/master --format unity-args --platform EditMode
#
# Formats: summary (default, human), names, json, unity-args.
# Exits 0 normally; 3 if the graph could not be read.

import argparse
import json
import os
import re
import subprocess
import sys

# Suites that assert whole-system properties and are never selected out. Matched by exact
# assembly name; unknown entries are ignored so this list can lead the migration.
ALWAYS_RUN = (
    "VoxelEngine.Tests.Parity",
    "VoxelEngine.Tests.Features",
    "VoxelEngine.Tests.Invariants",
)

# Changes anywhere under these select every test assembly: they are outside the graph but can
# change behaviour inside it.
GLOBAL_PATHS = (
    "ProjectSettings/",
    "Packages/manifest.json",
    "Packages/packages-lock.json",
    "tools/",
    ".github/workflows/",
)


def discover(project):
    """Every .asmdef in the project, with the files it owns and who it references."""
    defs = []
    for root in ("Assets", "Packages"):
        base = os.path.join(project, root)
        if not os.path.isdir(base):
            continue
        for dirpath, _, filenames in os.walk(base):
            for f in filenames:
                if f.endswith(".asmdef"):
                    defs.append(os.path.join(dirpath, f))

    by_dir, info, guids = {}, {}, {}
    for path in sorted(defs):
        try:
            with open(path) as fh:
                data = json.load(fh)
        except (OSError, ValueError) as e:
            sys.stderr.write("select-tests: cannot read %s: %s\n" % (path, e))
            raise SystemExit(3)
        name = data["name"]
        d = os.path.dirname(path)
        by_dir[d] = name
        platforms = data.get("includePlatforms") or []
        constraints = data.get("defineConstraints") or []
        refs = data.get("references") or []
        info[name] = {
            "dir": d,
            "refs": refs,
            "editor_only": platforms == ["Editor"],
            "platforms": platforms,
            # Three markers, because the project uses all three. Newer asmdefs carry the
            # UNITY_INCLUDE_TESTS constraint; older ones only set the legacy
            # optionalUnityReferences: ["TestAssemblies"], which is how VoxelEngine.CI.PlayMode
            # declares itself and is easy to miss; a direct nunit/TestRunner reference counts
            # too. Missing a marker silently drops a whole suite from every selection, so err
            # toward recognising one.
            "is_test": "UNITY_INCLUDE_TESTS" in constraints
            or "TestAssemblies" in (data.get("optionalUnityReferences") or [])
            or any("nunit" in r.lower() or "TestRunner" in r for r in refs),
            "asmdef": path,
        }
        meta = path + ".meta"
        if os.path.exists(meta):
            m = re.search(r"^guid:\s*(\w+)", open(meta).read(), re.M)
            if m:
                guids[m.group(1)] = name

    return by_dir, info, guids


def owner_of(path, by_dir, project):
    """The nearest enclosing .asmdef directory owns the file. None if outside every assembly."""
    d = os.path.dirname(os.path.join(project, path))
    root = os.path.abspath(project)
    while True:
        if d in by_dir:
            return by_dir[d]
        parent = os.path.dirname(d)
        if parent == d or len(d) <= len(root):
            return None
        d = parent


def reverse_graph(info, guids):
    """assembly -> set of assemblies that reference it directly."""
    def resolve(ref):
        if ref.startswith("GUID:"):
            return guids.get(ref[5:])
        return ref if ref in info else None

    rev = {n: set() for n in info}
    for name, a in info.items():
        for ref in a["refs"]:
            target = resolve(ref)
            if target:
                rev[target].add(name)
    return rev


def dependents(seeds, rev):
    """Transitive closure over the reverse graph, including the seeds themselves."""
    out, stack = set(seeds), list(seeds)
    while stack:
        cur = stack.pop()
        for child in rev.get(cur, ()):
            if child not in out:
                out.add(child)
                stack.append(child)
    return out


def changed_files(args, project):
    if args.changed:
        return list(args.changed)
    base = args.base
    merge_base = subprocess.run(
        ["git", "merge-base", base, "HEAD"], cwd=project,
        capture_output=True, text=True)
    ref = merge_base.stdout.strip() or base
    r = subprocess.run(["git", "diff", "--name-only", ref, "--"], cwd=project,
                       capture_output=True, text=True)
    if r.returncode != 0:
        sys.stderr.write("select-tests: git diff failed: %s\n" % r.stderr.strip())
        raise SystemExit(3)
    return [p for p in r.stdout.splitlines() if p.strip()]


def main():
    ap = argparse.ArgumentParser(add_help=True)
    g = ap.add_mutually_exclusive_group()
    g.add_argument("--base", default="origin/master", help="ref to diff against")
    g.add_argument("--changed", nargs="*", help="explicit changed file list")
    g.add_argument("--all", action="store_true", help="select every test assembly")
    ap.add_argument("--format", default="summary",
                    choices=("summary", "names", "json", "unity-args"))
    ap.add_argument("--platform", default="both", choices=("EditMode", "PlayMode", "both"))
    ap.add_argument("--project", default=os.path.join(os.path.dirname(__file__), ".."))
    args = ap.parse_args()

    project = os.path.abspath(args.project)
    by_dir, info, guids = discover(project)
    rev = reverse_graph(info, guids)
    tests = {n for n, a in info.items() if a["is_test"]}

    reasons, unowned = {}, []
    if args.all:
        selected = set(tests)
        for t in selected:
            reasons[t] = "--all"
        files = []
        global_hit = None
    else:
        files = changed_files(args, project)
        global_hit = next((f for f in files
                           if any(f.startswith(p) for p in GLOBAL_PATHS)), None)
        seeds = set()
        for f in files:
            if global_hit and f == global_hit:
                continue
            o = owner_of(f, by_dir, project)
            if o:
                seeds.add(o)
            elif f.startswith("Assets/") or f.startswith("Packages/"):
                unowned.append(f)

        if global_hit or unowned:
            selected = set(tests)
            why = ("out-of-graph change: %s" % (global_hit or unowned[0]))
            for t in selected:
                reasons[t] = why
        else:
            affected = dependents(seeds, rev)
            selected = affected & tests
            # Name the seeds that actually reach each test, so a surprising selection can be
            # traced back to the edit that caused it rather than taken on faith.
            for seed in sorted(seeds):
                for t in dependents({seed}, rev) & selected:
                    reasons.setdefault(t, "depends on " + seed)

    for n in ALWAYS_RUN:
        if n in info and info[n]["is_test"]:
            selected.add(n)
            reasons.setdefault(n, "always-run invariant suite")

    def platforms_for(n):
        # includePlatforms decides the run mode, and it is the *only* thing that decides it —
        # the assembly's name does not. An asmdef restricted to ["Editor"] appears in EditMode
        # runs and is invisible to -testPlatform PlayMode; an unrestricted one is the reverse.
        # Verified against this project: a full EditMode run reports exactly two assemblies,
        # and the four unrestricted suites contribute nothing to it.
        #
        # The trap is an assembly named ...Tests.PlayMode that carries includePlatforms
        # ["Editor"]. It runs in the wrong mode, where its [UnityTest] cases fail on absent
        # cameras, and it never runs in the mode it was written for.
        return ["EditMode"] if info[n]["editor_only"] else ["PlayMode"]

    chosen = sorted(selected)
    if args.platform != "both":
        chosen = [n for n in chosen if args.platform in platforms_for(n)]

    if args.format == "names":
        for n in chosen:
            print(n)
    elif args.format == "unity-args":
        if not chosen:
            return 0
        print("-assemblyNames " + ";".join(chosen))
    elif args.format == "json":
        print(json.dumps({
            "changed_files": len(files),
            "selected": chosen,
            "skipped": sorted(tests - set(chosen)),
            "platforms": {n: platforms_for(n) for n in chosen},
            "reasons": {n: reasons.get(n, "") for n in chosen},
        }, indent=2))
    else:
        print("changed files: %d" % len(files))
        if global_hit:
            print("full run: out-of-graph change (%s)" % global_hit)
        elif unowned:
            print("full run: %d changed file(s) owned by no assembly, e.g. %s"
                  % (len(unowned), unowned[0]))
        print("test assemblies: %d of %d selected" % (len(chosen), len(tests)))
        for n in chosen:
            print("   + %-46s %-10s %s"
                  % (n, "/".join(platforms_for(n)), reasons.get(n, "")))
        for n in sorted(tests - set(chosen)):
            print("   - %s" % n)

        # A suite that runs in the opposite mode to the one its name claims is almost always a
        # mistake, and a quiet one: it fails for the wrong reason in the mode it does run in,
        # and is silently absent from the mode CI targets for it.
        for n in sorted(tests):
            mode = platforms_for(n)[0]
            if "PlayMode" in n and mode == "EditMode":
                print("warning: %s is includePlatforms [\"Editor\"], so it runs in EditMode "
                      "and never in a -testPlatform PlayMode run" % n)
            elif "EditMode" in n and mode == "PlayMode":
                print("warning: %s is unrestricted, so it runs in PlayMode, not EditMode" % n)
    return 0


if __name__ == "__main__":
    sys.exit(main())
