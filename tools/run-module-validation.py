#!/usr/bin/env python3
"""Execute a convention-derived module validation plan."""
from __future__ import annotations
import argparse, json, os, subprocess, time, xml.etree.ElementTree as ET
from pathlib import Path

# This suite intentionally remains process-isolated. The full master workflow
# shards it into fresh editors because repeated scene/rendering loads retain
# native allocations across runs.
PROCESS_ISOLATED_ASSEMBLIES = {"VoxelEngine.Tests.PlayMode"}


def run_test(unity: str, item: dict, root: Path) -> float:
    module = item["module"]
    platform = item["platform"]
    assembly = item["assembly"]
    safe = "".join(c if c.isalnum() or c in "-_" else "_" for c in module + "-" + platform + "-" + assembly)
    out = root / "Tests" / safe
    out.mkdir(parents=True, exist_ok=True)
    xml = out / "results.xml"
    log = out / "unity.log"
    # Required module suites may contain graphics/compute regressions even when they are EditMode.
    # Keep the graphics device available so those tests execute rather than silently becoming
    # unavailable under -nographics; skipped required cases are intentionally treated as failures.
    args = ["tools/unity-run.sh", "-batchmode", "-job-worker-count", "1",
            "-projectPath", str(Path.cwd()), "-runTests", "-testPlatform", platform,
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


def _parse_summary(path: Path) -> dict[str, str]:
    if not path.is_file():
        raise SystemExit(f"ERROR: persistent module validation produced no summary: {path}")
    values = {}
    for line in path.read_text(encoding="utf-8").splitlines():
        if "=" in line:
            key, value = line.split("=", 1)
            values[key] = value
    return values


def _phase_assemblies(items: list[dict], platform: str) -> list[str]:
    return list(dict.fromkeys(
        item["assembly"] for item in items if item["platform"] == platform
    ))


def run_persistent_tests(unity: str, items: list[dict], root: Path) -> float:
    edit_assemblies = _phase_assemblies(items, "EditMode")
    play_assemblies = _phase_assemblies(items, "PlayMode")
    unknown = sorted({
        item["platform"] for item in items
        if item["platform"] not in ("EditMode", "PlayMode")
    })
    if unknown:
        raise SystemExit("ERROR: unsupported module test platform(s): " + ", ".join(unknown))

    out = root / "Tests" / "Persistent"
    out.mkdir(parents=True, exist_ok=True)
    env = os.environ.copy()
    env.update({
        "UNITY_BIN": unity,
        "UNITY_MAX_RSS_MB": "14336",
        "UNITY_MAX_MINUTES": "8",
        "VOXEL_CI_EDITMODE_ASSEMBLIES": ";".join(edit_assemblies),
        "VOXEL_CI_PLAYMODE_ASSEMBLIES": ";".join(play_assemblies),
        "VOXEL_CI_RESULTS_ROOT": str(out.resolve()),
        "VOXEL_CI_BAKE_SHOWCASE": "0",
    })
    args = ["tools/unity-run.sh", "-batchmode", "-job-worker-count", "1",
            "-projectPath", str(Path.cwd()),
            "-executeMethod", "VoxelCiPersistentTestRunner.Run",
            "-logFile", str(out / "persistent.log")]
    started = time.monotonic()
    subprocess.run(args, check=True, env=env)
    seconds = time.monotonic() - started

    final = _parse_summary(out / "persistent-summary.txt")
    if final.get("status") != "passed":
        raise SystemExit(
            "ERROR: persistent module validation failed: " +
            final.get("message", "missing final status")
        )

    # Targeted module validation has historically treated skipped required tests
    # and zero-test selections as failures. Preserve that stricter contract even
    # though the persistent master runner itself permits skipped tests.
    for phase, assemblies in (("editmode", edit_assemblies), ("playmode", play_assemblies)):
        if not assemblies:
            continue
        phase_summary = _parse_summary(out / f"persistent-{phase}.txt")
        counts = {}
        for key in ("passed", "failed", "skipped", "inconclusive"):
            try:
                counts[key] = int(phase_summary.get(key, "0"))
            except ValueError as exc:
                raise SystemExit(
                    f"ERROR: invalid persistent {phase} test count: {key}"
                ) from exc
        if sum(counts.values()) == 0:
            raise SystemExit(
                f"ERROR: required persistent {phase} module tests executed zero tests"
            )
        if counts["skipped"] or counts["failed"] or counts["inconclusive"]:
            raise SystemExit(
                f"ERROR: required persistent {phase} module tests did not all pass "
                f"({counts['failed']} failed, {counts['skipped']} skipped, "
                f"{counts['inconclusive']} inconclusive)"
            )
    return seconds


def main(argv=None) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--unity", required=True)
    ap.add_argument("--plan", required=True)
    ap.add_argument("--output", required=True)
    ns = ap.parse_args(argv)
    plan = json.loads(Path(ns.plan).read_text(encoding="utf-8"))
    root = Path(ns.output)
    root.mkdir(parents=True, exist_ok=True)
    summary = {"tests": [], "players": [], "editorBatches": [], "totalSeconds": 0.0}
    started_all = time.monotonic()

    tests = plan.get("tests", [])
    persistent = [
        item for item in tests
        if item["assembly"] not in PROCESS_ISOLATED_ASSEMBLIES
    ]
    isolated = [
        item for item in tests
        if item["assembly"] in PROCESS_ISOLATED_ASSEMBLIES
    ]

    if persistent:
        seconds = run_persistent_tests(ns.unity, persistent, root)
        summary["editorBatches"].append({
            "kind": "persistent",
            "editModeAssemblies": _phase_assemblies(persistent, "EditMode"),
            "playModeAssemblies": _phase_assemblies(persistent, "PlayMode"),
            "seconds": round(seconds, 2),
        })
        # Keep the historical numeric per-test timing field useful for consumers:
        # these are amortized shares of the one editor batch, not independent runs.
        amortized = round(seconds / len(persistent), 2)
        for item in persistent:
            summary["tests"].append({
                **item,
                "seconds": amortized,
                "execution": "persistent-editor",
            })

    for item in isolated:
        seconds = run_test(ns.unity, item, root)
        summary["tests"].append({
            **item,
            "seconds": round(seconds, 2),
            "execution": "isolated-editor",
        })

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
