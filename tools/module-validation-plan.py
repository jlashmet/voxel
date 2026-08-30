#!/usr/bin/env python3
"""Diff-driven module validation planner.

Module policy lives in *.module-validation.json files. The planner only understands
the schema and path matching; it contains no module, scene, material, or test names.
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

def load_manifest(path: Path) -> dict:
    try:
        data=json.loads(path.read_text(encoding="utf-8"))
    except (OSError,json.JSONDecodeError) as exc:
        raise ManifestError(f"{path}: {exc}") from exc
    if not isinstance(data,dict) or data.get("schemaVersion") != SCHEMA_VERSION:
        raise ManifestError(f"{path}: schemaVersion must be {SCHEMA_VERSION}")
    module=data.get("module")
    if not isinstance(module,str) or not module.strip():
        raise ManifestError(f"{path}: module must be a non-empty string")
    _strings(data.get("productionPaths"), f"{path}: productionPaths")
    shared=data.get("sharedPaths",[])
    _strings(shared, f"{path}: sharedPaths", allow_empty=True)
    tests=data.get("tests")
    if not isinstance(tests,list) or not tests:
        raise ManifestError(f"{path}: tests must be a non-empty array")
    normalized_tests=[]
    for i,test in enumerate(tests):
        if not isinstance(test,dict):
            raise ManifestError(f"{path}: tests[{i}] must be an object")
        platform=test.get("platform")
        test_filter=test.get("filter")
        if platform not in ("EditMode","PlayMode") or not isinstance(test_filter,str) or not test_filter:
            raise ManifestError(f"{path}: tests[{i}] requires platform EditMode|PlayMode and non-empty filter")
        normalized_tests.append({"platform":platform,"filter":test_filter})
    player=data.get("playerValidation")
    if player is not None:
        if not isinstance(player,dict):
            raise ManifestError(f"{path}: playerValidation must be an object")
        scene=player.get("scene")
        scenario=player.get("scenario")
        if not isinstance(scene,str) or not scene.endswith(".unity") or not scene.startswith("Assets/"):
            raise ManifestError(f"{path}: playerValidation.scene must be an Assets/... .unity path")
        if not isinstance(scenario,str) or not scenario.endswith(".player-scenario.json"):
            raise ManifestError(f"{path}: playerValidation.scenario must be a *.player-scenario.json path")
    return {
        "module":module,
        "manifest":path.as_posix(),
        "productionPaths":data["productionPaths"],
        "sharedPaths":shared,
        "tests":normalized_tests,
        "playerValidation":player,
    }

def matches(path: str, pattern: str) -> bool:
    prefix=pattern[:-3] if pattern.endswith("/**") else None
    return fnmatch.fnmatchcase(path,pattern) or (prefix is not None and (path==prefix or path.startswith(prefix+"/")))

def discover(root: Path) -> list[dict]:
    result=[]
    for path in sorted(root.glob("Assets/**/*.module-validation.json")):
        m=load_manifest(path)
        raw=json.loads(path.read_text(encoding="utf-8"))
        gate=raw.get("integrationGate",False)
        if not isinstance(gate,bool):
            raise ManifestError(f"{path}: integrationGate must be boolean")
        m["integrationGate"]=gate
        m["productionPaths"]=list(m["productionPaths"])+[path.as_posix()]
        result.append(m)
    if not result:
        raise ManifestError("no module validation manifests discovered under Assets/")
    names=[m["module"] for m in result]
    if len(names)!=len(set(names)):
        raise ManifestError("module names must be unique")
    gates=[m for m in result if m["integrationGate"]]
    if len(gates)!=1 or not gates[0].get("playerValidation"):
        raise ManifestError("exactly one module manifest must declare a player integrationGate")
    return result

def plan(changed_paths: list[str], manifests: list[dict], *, require_ownership=True) -> dict:
    changed=sorted({p.strip().replace("\\","/") for p in changed_paths if p.strip()})
    production=[p for p in changed if p.startswith("Assets/") and not p.endswith(".meta")
                and not p.endswith(".module-validation.json")
                and not p.endswith(".player-scenario.json")
                and "/Tests/" not in p and not p.startswith("Assets/Tests/")]
    selected={}
    unresolved=[]
    for p in changed:
        owners=[m for m in manifests if any(matches(p,pat) for pat in m["productionPaths"])]
        shared=[m for m in manifests if any(matches(p,pat) for pat in m["sharedPaths"])]
        for m in owners+shared:
            selected[m["module"]]=m
    for p in production:
        if not any(any(matches(p,pat) for pat in (m["productionPaths"]+m["sharedPaths"])) for m in manifests):
            unresolved.append(p)
    if unresolved and require_ownership:
        raise ManifestError("unowned production path(s): "+", ".join(unresolved))
    tests=[]
    players=[]
    for module in sorted(selected):
        m=selected[module]
        for test in m["tests"]:
            item={"module":module,**test}
            if item not in tests: tests.append(item)
        if m["playerValidation"]:
            players.append({"module":module,**m["playerValidation"]})
    if selected:
        for m in manifests:
            if m["integrationGate"] and m.get("playerValidation"):
                item={"module":m["module"],**m["playerValidation"]}
                if item not in players: players.append(item)
    return {"changedPaths":changed,"modules":sorted(selected),"tests":tests,"playerValidations":players,
            "hasProductionChanges":bool(production),"unresolvedProductionPaths":unresolved}

def main(argv=None):
    ap=argparse.ArgumentParser()
    ap.add_argument("--root",default=".")
    ap.add_argument("--changed-file",action="append",default=[])
    ap.add_argument("--changed-file-list",action="append",default=[])
    ap.add_argument("--output")
    ap.add_argument("--allow-unowned-production",action="store_true")
    ns=ap.parse_args(argv)
    changed=list(ns.changed_file)
    for name in ns.changed_file_list:
        changed.extend(Path(name).read_text(encoding="utf-8").splitlines())
    try:
        result=plan(changed,discover(Path(ns.root)),require_ownership=not ns.allow_unowned_production)
    except ManifestError as exc:
        print(f"ERROR: {exc}",file=sys.stderr)
        return 2
    encoded=json.dumps(result,sort_keys=True,separators=(",",":"))
    if ns.output:
        Path(ns.output).write_text(json.dumps(result,indent=2,sort_keys=True)+"\n",encoding="utf-8")
    print(encoded)
    return 0
if __name__=="__main__":
    raise SystemExit(main())
