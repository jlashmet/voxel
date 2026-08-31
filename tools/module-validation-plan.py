#!/usr/bin/env python3
"""Diff-driven module validation planner.

All module/scene/test policy lives in *.module-validation.json metadata. This planner
knows only the schema, ownership matching, conservative fallback, and integration-gate rules.
"""
from __future__ import annotations
import argparse, fnmatch, json, sys
from pathlib import Path

SCHEMA_VERSION = 1

class ManifestError(ValueError):
    pass

def _strings(value, field, allow_empty=False):
    if not isinstance(value, list) or (not allow_empty and not value):
        raise ManifestError(f"{field} must be a {'possibly empty ' if allow_empty else 'non-empty '}array")
    if any(not isinstance(v, str) or not v.strip() for v in value):
        raise ManifestError(f"{field} entries must be non-empty strings")
    return value

def load_manifest(path: Path, root: Path) -> dict:
    try:
        data=json.loads(path.read_text(encoding="utf-8"))
    except (OSError,json.JSONDecodeError) as exc:
        raise ManifestError(f"{path}: {exc}") from exc
    if not isinstance(data,dict) or data.get("schemaVersion") != SCHEMA_VERSION:
        raise ManifestError(f"{path}: schemaVersion must be {SCHEMA_VERSION}")
    module=data.get("module")
    if not isinstance(module,str) or not module.strip():
        raise ManifestError(f"{path}: module must be a non-empty string")
    production=_strings(data.get("productionPaths"), f"{path}: productionPaths")
    shared=_strings(data.get("sharedPaths",[]), f"{path}: sharedPaths", allow_empty=True)
    integration=data.get("integrationGate",False)
    fallback=data.get("fallback",False)
    if not isinstance(integration,bool) or not isinstance(fallback,bool):
        raise ManifestError(f"{path}: integrationGate/fallback must be boolean")
    tests=data.get("tests",[])
    if not isinstance(tests,list) or (not tests and not integration):
        raise ManifestError(f"{path}: tests must be a non-empty array for owning modules")
    normalized_tests=[]
    for i,test in enumerate(tests):
        if not isinstance(test,dict):
            raise ManifestError(f"{path}: tests[{i}] must be an object")
        platform=test.get("platform")
        test_filter=test.get("filter")
        if platform not in ("EditMode","PlayMode") or not isinstance(test_filter,str) or not test_filter.strip():
            raise ManifestError(f"{path}: tests[{i}] requires platform EditMode|PlayMode and non-empty filter")
        normalized_tests.append({"platform":platform,"filter":test_filter})
    player=data.get("playerValidation")
    if player is not None:
        if not isinstance(player,dict):
            raise ManifestError(f"{path}: playerValidation must be an object")
        scene=player.get("scene")
        scenario=player.get("scenario")
        if not isinstance(scene,str) or not scene.startswith("Assets/") or not scene.endswith(".unity"):
            raise ManifestError(f"{path}: playerValidation.scene must be an Assets/... .unity path")
        if not isinstance(scenario,str) or not scenario.startswith("Assets/") or not scenario.endswith(".player-scenario.json"):
            raise ManifestError(f"{path}: playerValidation.scenario must be an Assets/... *.player-scenario.json path")
        for field,target in (("scene",scene),("scenario",scenario)):
            if not (root/target).is_file():
                raise ManifestError(f"{path}: playerValidation.{field} does not exist: {target}")
    return {"module":module,"manifest":path.relative_to(root).as_posix(),
            "productionPaths":list(production)+[path.relative_to(root).as_posix()],
            "sharedPaths":shared,"tests":normalized_tests,"playerValidation":player,
            "integrationGate":integration,"fallback":fallback}

def matches(path: str, pattern: str) -> bool:
    prefix=pattern[:-3] if pattern.endswith("/**") else None
    return fnmatch.fnmatchcase(path,pattern) or (prefix is not None and (path==prefix or path.startswith(prefix+"/")))

def discover(root: Path) -> list[dict]:
    manifests=[load_manifest(path,root) for path in sorted(root.glob("Assets/**/*.module-validation.json"))]
    if not manifests:
        raise ManifestError("no module validation manifests discovered under Assets/")
    names=[m["module"] for m in manifests]
    if len(names)!=len(set(names)):
        raise ManifestError("module names must be unique")
    gates=[m for m in manifests if m["integrationGate"]]
    if len(gates)!=1 or not gates[0].get("playerValidation"):
        raise ManifestError("exactly one manifest with playerValidation must declare integrationGate")
    if not any(m["fallback"] for m in manifests):
        raise ManifestError("at least one manifest must declare fallback=true")
    return manifests

def is_production(path: str) -> bool:
    return (path.startswith("Assets/") and not path.endswith(".meta")
            and not path.endswith(".player-scenario.json") and "/Tests/" not in path
            and not path.startswith("Assets/Tests/"))

def plan(changed_paths: list[str], manifests: list[dict]) -> dict:
    changed=sorted({p.strip().replace("\\","/") for p in changed_paths if p.strip()})
    production=[p for p in changed if is_production(p)]
    selected={}
    fallback_used=[]
    for p in changed:
        direct=[m for m in manifests if not m["fallback"] and any(matches(p,pat) for pat in m["productionPaths"])]
        dependents=[m for m in manifests if not m["fallback"] and any(matches(p,pat) for pat in m["sharedPaths"])]
        matches_now=direct+dependents
        if not matches_now and is_production(p):
            matches_now=[m for m in manifests if m["fallback"] and
                         any(matches(p,pat) for pat in (m["productionPaths"]+m["sharedPaths"]))]
            if matches_now:
                fallback_used.append(p)
        if is_production(p) and not matches_now:
            raise ManifestError("unowned production path without fallback: "+p)
        for m in matches_now:
            selected[m["module"]]=m
    tests=[]
    players=[]
    for module in sorted(selected):
        m=selected[module]
        for test in m["tests"]:
            item={"module":module,**test}
            if item not in tests: tests.append(item)
        if m.get("playerValidation"):
            players.append({"module":module,**m["playerValidation"]})
    if production:
        gate=next(m for m in manifests if m["integrationGate"])
        item={"module":gate["module"],**gate["playerValidation"]}
        if item not in players: players.append(item)
    return {"changedPaths":changed,"modules":sorted(selected),"tests":tests,"playerValidations":players,
            "hasProductionChanges":bool(production),"fallbackPaths":sorted(fallback_used)}

def main(argv=None):
    ap=argparse.ArgumentParser()
    ap.add_argument("--root",default=".")
    ap.add_argument("--changed-file",action="append",default=[])
    ap.add_argument("--changed-file-list",action="append",default=[])
    ap.add_argument("--output")
    ns=ap.parse_args(argv)
    changed=list(ns.changed_file)
    for name in ns.changed_file_list:
        changed.extend(Path(name).read_text(encoding="utf-8").splitlines())
    try:
        result=plan(changed,discover(Path(ns.root)))
    except ManifestError as exc:
        print(f"ERROR: {exc}",file=sys.stderr)
        return 2
    if ns.output:
        Path(ns.output).write_text(json.dumps(result,indent=2,sort_keys=True)+"\n",encoding="utf-8")
    print(json.dumps(result,sort_keys=True,separators=(",",":")))
    return 0

if __name__=="__main__":
    raise SystemExit(main())
