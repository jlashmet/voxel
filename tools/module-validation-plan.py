#!/usr/bin/env python3
"""Diff-driven validation planning from repository structure.

A module is any Assets subtree that owns Tests/EditMode and/or Tests/PlayMode
assemblies. Production ownership is the longest matching module root. Focused
Unity tests are every asmdef below the owning module's test directories.
Player-visible validation is opt-in by placing paired scene/scenario files under
<module>/Validation. KentridgePlayableSlice is the canonical production-change
integration gate.
"""
from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

KENTRIDGE_SCENE = "Assets/Scenes/KentridgePlayableSlice.unity"
KENTRIDGE_SCENARIO = "Assets/Scenes/Validation/kentridge.player-scenario.json"


class ConventionError(ValueError):
    pass


def _rel(path: Path, root: Path) -> str:
    return path.relative_to(root).as_posix()


def _load_asmdef(path: Path) -> dict:
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise ConventionError(f"{path}: {exc}") from exc
    name = data.get("name") if isinstance(data, dict) else None
    references = data.get("references", []) if isinstance(data, dict) else []
    if not isinstance(name, str) or not name.strip():
        raise ConventionError(f"{path}: asmdef requires a non-empty name")
    if not isinstance(references, list) or any(not isinstance(v, str) for v in references):
        raise ConventionError(f"{path}: asmdef references must be an array of strings")
    return {"name": name, "references": references}


def _asmdef_guid(path: Path) -> str | None:
    meta = Path(str(path) + ".meta")
    if not meta.is_file():
        return None
    match = re.search(r"(?m)^guid:\s*([0-9a-fA-F]+)\s*$", meta.read_text(encoding="utf-8"))
    return match.group(1).lower() if match else None


def _module_roots(root: Path) -> list[Path]:
    roots: set[Path] = set()
    for platform in ("EditMode", "PlayMode"):
        for asmdef in root.glob(f"Assets/**/Tests/{platform}/**/*.asmdef"):
            parts = asmdef.relative_to(root).parts
            try:
                tests_index = parts.index("Tests")
            except ValueError:
                continue
            if tests_index == 0:
                continue
            roots.add(root.joinpath(*parts[:tests_index]))
    return sorted(roots, key=lambda p: p.as_posix())


def _module_for_path(path: str, modules: list[dict]) -> dict | None:
    candidates = [m for m in modules if path == m["root"] or path.startswith(m["root"] + "/")]
    return max(candidates, key=lambda m: len(m["root"]), default=None)


def _discover_player_targets(module_root: Path, root: Path, module_name: str) -> list[dict]:
    validation_root = module_root / "Validation"
    if not validation_root.is_dir():
        return []
    scenes = sorted(validation_root.rglob("*.unity"))
    scenarios = sorted(validation_root.rglob("*.player-scenario.json"))
    targets = []
    expected_scenarios = set()
    for scene in scenes:
        scenario = scene.with_suffix(".player-scenario.json")
        expected_scenarios.add(scenario)
        if not scenario.is_file():
            raise ConventionError(f"validation scene is missing paired scenario: {_rel(scene, root)}")
        targets.append({
            "module": module_name,
            "scene": _rel(scene, root),
            "scenario": _rel(scenario, root),
        })
    orphaned = [p for p in scenarios if p not in expected_scenarios]
    if orphaned:
        raise ConventionError(f"validation scenario is missing paired scene: {_rel(orphaned[0], root)}")
    return targets


def discover(root: Path) -> dict:
    root = root.resolve()
    obsolete = sorted(root.glob("Assets/**/*.module-validation.json"))
    if obsolete:
        raise ConventionError("obsolete *.module-validation.json registration is not supported: " + _rel(obsolete[0], root))
    top_level = sorted((root / "Assets" / "Tests" / "EditMode").glob("**/*.asmdef")) if (root / "Assets" / "Tests" / "EditMode").exists() else []
    if top_level:
        raise ConventionError("repository-wide Assets/Tests/EditMode assembly is not allowed: " + _rel(top_level[0], root))

    modules = []
    for module_root in _module_roots(root):
        module_name = _rel(module_root, root)
        tests = []
        for platform in ("EditMode", "PlayMode"):
            test_root = module_root / "Tests" / platform
            if not test_root.is_dir():
                continue
            for asmdef_path in sorted(test_root.rglob("*.asmdef")):
                asmdef = _load_asmdef(asmdef_path)
                tests.append({"module": module_name, "platform": platform, "assembly": asmdef["name"]})
        if not tests:
            continue
        runtime_assemblies = []
        for asmdef_path in sorted(module_root.rglob("*.asmdef")):
            rel = _rel(asmdef_path, root)
            if "/Tests/" in rel:
                continue
            asmdef = _load_asmdef(asmdef_path)
            runtime_assemblies.append({
                "name": asmdef["name"],
                "guid": _asmdef_guid(asmdef_path),
                "references": asmdef["references"],
            })
        modules.append({
            "name": module_name,
            "root": module_name,
            "tests": tests,
            "players": _discover_player_targets(module_root, root, module_name),
            "runtimeAssemblies": runtime_assemblies,
        })
    if not modules:
        raise ConventionError("no module-owned test assemblies discovered under Assets/**/Tests/{EditMode,PlayMode}")

    assembly_owner: dict[str, str] = {}
    for module in modules:
        for asmdef in module["runtimeAssemblies"]:
            tokens = [asmdef["name"]]
            if asmdef["guid"]:
                tokens.append("GUID:" + asmdef["guid"])
            for token in tokens:
                previous = assembly_owner.get(token)
                if previous is not None and previous != module["name"]:
                    raise ConventionError(f"runtime assembly token has multiple module owners: {token}")
                assembly_owner[token] = module["name"]

    dependencies: dict[str, set[str]] = {m["name"]: set() for m in modules}
    for module in modules:
        for asmdef in module["runtimeAssemblies"]:
            for reference in asmdef["references"]:
                token = reference
                if token.startswith("GUID:"):
                    token = "GUID:" + token[5:].lower()
                owner = assembly_owner.get(token)
                if owner and owner != module["name"]:
                    dependencies[module["name"]].add(owner)

    for required in (root / KENTRIDGE_SCENE, root / KENTRIDGE_SCENARIO):
        if not required.is_file():
            raise ConventionError(f"required Kentridge validation file does not exist: {_rel(required, root)}")
    return {"modules": modules, "dependencies": dependencies}


def is_production(path: str) -> bool:
    path = path.replace("\\", "/")
    if not path.startswith("Assets/") or path.endswith(".meta"):
        return False
    if "/Tests/" in path or path.startswith("Assets/Tests/"):
        return False
    if "/Validation/" in path or path.endswith(".player-scenario.json"):
        return False
    return True


def _expand_dependents(selected: set[str], dependencies: dict[str, set[str]]) -> set[str]:
    expanded = set(selected)
    changed = True
    while changed:
        changed = False
        for module, deps in dependencies.items():
            if module not in expanded and deps.intersection(expanded):
                expanded.add(module)
                changed = True
    return expanded


def plan(changed_paths: list[str], discovered: dict) -> dict:
    changed = sorted({p.strip().replace("\\", "/") for p in changed_paths if p.strip()})
    modules = discovered["modules"]
    by_name = {m["name"]: m for m in modules}
    selected: set[str] = set()
    direct_production: set[str] = set()
    fallback_paths = []
    production = [p for p in changed if is_production(p)]

    for path in changed:
        owner = _module_for_path(path, modules)
        if owner:
            selected.add(owner["name"])
            if is_production(path):
                direct_production.add(owner["name"])
        elif is_production(path):
            # A production path outside a discoverable module boundary is fail-safe broad:
            # run all known module contracts plus the Kentridge integration gate.
            selected.update(by_name)
            fallback_paths.append(path)

    if direct_production:
        selected.update(_expand_dependents(direct_production, discovered["dependencies"]))

    tests = []
    players = []
    for module_name in sorted(selected):
        module = by_name[module_name]
        tests.extend(module["tests"])
        players.extend(module["players"])
    if production:
        players.append({
            "module": "game-integration",
            "scene": KENTRIDGE_SCENE,
            "scenario": KENTRIDGE_SCENARIO,
        })
    return {
        "changedPaths": changed,
        "modules": sorted(selected),
        "tests": tests,
        "playerValidations": players,
        "hasProductionChanges": bool(production),
        "hasValidationWork": bool(tests or players),
        "fallbackPaths": sorted(fallback_paths),
    }


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--changed-file", action="append", default=[])
    ap.add_argument("--changed-file-list", action="append", default=[])
    ap.add_argument("--output")
    ns = ap.parse_args(argv)
    changed = list(ns.changed_file)
    for name in ns.changed_file_list:
        changed.extend(Path(name).read_text(encoding="utf-8").splitlines())
    try:
        result = plan(changed, discover(Path(ns.root)))
    except ConventionError as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 2
    if ns.output:
        Path(ns.output).write_text(json.dumps(result, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(json.dumps(result, sort_keys=True, separators=(",", ":")))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
