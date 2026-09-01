#!/usr/bin/env python3
"""Execute a convention-derived module validation plan."""
from __future__ import annotations
import argparse, json, os, subprocess, time, xml.etree.ElementTree as ET
from pathlib import Path


def run_test(unity: str, item: dict, root: Path) -> float:
    module = item["module"]
    platform = item["platform"]
    assembly = item["assembly"]
    safe = "".join(c if c.isalnum() or c in "-_" else "_" for c in module + "-" + platform + "-" + assembly)
    out = root / "Tests" / safe
    out.mkdir(parents=True, exist_ok=True)
    xml = out / "results.xml"
    log = out / "unity.log"
    args = ["tools/unity-run.sh", "-batchmode", "-job-worker-count", "1"]
    if platform == "EditMode":
        args.append("-nographics")
    args += ["-projectPath", str(Path.cwd()), "-runTests", "-testPlatform", platform,
             "-assemblyNames", assembly, "-testResults", str(xml), "-logFile", str(log)]
    env = os.environ.copy()
    env.update({"UNITY_BIN": unity, "UNITY_MAX_RSS_MB": "14336", "UNITY_MAX_MINUTES": "4"})
    started = time.monotonic()
    subprocess.run(args, check=True, env=env)
    if not xml.is_file():
        raise SystemExit(f"ERROR: required module test assembly produced no results: {module} {assembly}")
    cases = ET.parse(xml).getroot().findall(".//test-case")
    if not cases:
        raise SystemExit(f"ERROR: required module test assembly executed zero tests: {module} {assembly}")
    failed = [c for c in cases if c.get("result") not in ("Passed", "Success")]
    if failed:
        raise SystemExit(f"ERROR: required module test assembly failed: {module} {assembly} ({len(failed)} failures)")
    return time.monotonic() - started


def main(argv=None) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--unity", required=True)
    ap.add_argument("--plan", required=True)
    ap.add_argument("--output", required=True)
    ns = ap.parse_args(argv)
    plan = json.loads(Path(ns.plan).read_text(encoding="utf-8"))
    root = Path(ns.output)
    root.mkdir(parents=True, exist_ok=True)
    summary = {"tests": [], "players": [], "totalSeconds": 0.0}
    started_all = time.monotonic()
    for item in plan.get("tests", []):
        seconds = run_test(ns.unity, item, root)
        summary["tests"].append({**item, "seconds": round(seconds, 2)})
    for item in plan.get("playerValidations", []):
        module = item["module"]
        safe_module = "".join(c if c.isalnum() or c in "-_" else "_" for c in module)
        out = root / "Players" / safe_module
        started = time.monotonic()
        subprocess.run(["python3", "tools/player-validation.py", "--unity", ns.unity,
                        "--scene", item["scene"], "--scenario", item["scenario"],
                        "--output", str(out)], check=True)
        summary["players"].append({**item, "seconds": round(time.monotonic() - started, 2)})
    summary["totalSeconds"] = round(time.monotonic() - started_all, 2)
    (root / "module-validation-summary.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(summary, sort_keys=True))
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
