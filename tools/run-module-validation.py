#!/usr/bin/env python3
"""Execute a convention-derived module validation plan."""
from __future__ import annotations
import argparse, hashlib, json, os, subprocess, time, xml.etree.ElementTree as ET
from pathlib import Path

# This suite intentionally remains process-isolated. The full master workflow
# shards it into fresh editors because repeated scene/rendering loads retain
# native allocations across runs.
PROCESS_ISOLATED_ASSEMBLIES = {"VoxelEngine.Tests.PlayMode"}


def run_test(unity: str, item: dict, root: Path, test_filter: str | None = None) -> float:
    module = item["module"]
    platform = item["platform"]
    assembly = item["assembly"]
    safe = "".join(c if c.isalnum() or c in "-_" else "_" for c in module + "-" + platform + "-" + assembly)
    out = root / "Tests" / safe
    out.mkdir(parents=True, exist_ok=True)
    xml = out / "results.xml"
    log = out / "unity.log"
    args = ["tools/unity-run.sh", "-batchmode", "-job-worker-count", "1",
            "-projectPath", str(Path.cwd()), "-runTests", "-testPlatform", platform,
            "-assemblyNames", assembly]
    if test_filter:
        args.extend(["-testFilter", test_filter])
    args.extend(["-testResults", str(xml), "-logFile", str(log)])
    env = os.environ.copy()
    env.update({"UNITY_BIN": unity, "UNITY_MAX_RSS_MB": "14336", "UNITY_MAX_MINUTES": "4"})
    if platform == "PlayMode":
        env["VOXEL_DISABLE_GPU_CUTOVER"] = "1"
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
    return list(dict.fromkeys(item["assembly"] for item in items if item["platform"] == platform))


def _nearest_asmdef_name(source: Path, project_root: Path) -> str | None:
    project_root = project_root.resolve()
    parent = source.parent.resolve()
    while parent == project_root or project_root in parent.parents:
        asmdefs = sorted(parent.glob("*.asmdef"))
        if asmdefs:
            if len(asmdefs) != 1:
                return None
            try:
                value = json.loads(asmdefs[0].read_text(encoding="utf-8")).get("name")
            except (OSError, json.JSONDecodeError):
                return None
            return value if isinstance(value, str) and value else None
        if parent == project_root:
            break
        parent = parent.parent
    return None


def _requested_test_covered_by_selected_assembly(
    test_name: str,
    platform: str,
    items: list[dict],
    project_root: Path | None = None,
) -> bool:
    """Return true only when an exact requested leaf is owned by an already-selected assembly.

    The exact test request still gates the run: its source must resolve unambiguously to a
    selected assembly, and that assembly must pass every discovered test. This avoids
    re-running stateful/expensive tests a second time in the same persistent Unity editor.
    """
    if not test_name or platform not in ("EditMode", "PlayMode"):
        return False
    parts = test_name.rsplit(".", 2)
    if len(parts) < 3:
        return False
    class_name, method_name = parts[-2], parts[-1]
    root = (project_root or Path.cwd()).resolve()
    assets = root / "Assets"
    if not assets.is_dir():
        return False
    selected = {item["assembly"] for item in items if item.get("platform") == platform}
    if not selected:
        return False

    owners = set()
    for source in assets.rglob("*.cs"):
        try:
            text = source.read_text(encoding="utf-8")
        except (OSError, UnicodeDecodeError):
            continue
        if f"class {class_name}" not in text or method_name not in text:
            continue
        owner = _nearest_asmdef_name(source, root)
        if owner:
            owners.add(owner)
    return len(owners) == 1 and next(iter(owners)) in selected


def _validate_phase_summary(path: Path, expected: str) -> None:
    phase = _parse_summary(path)
    if phase.get("assembly") != expected:
        raise SystemExit(
            f"ERROR: persistent module validation summary mismatch: expected {expected}, "
            f"got {phase.get('assembly', '<missing>')}"
        )
    counts = {}
    for key in ("passed", "failed", "skipped", "inconclusive"):
        try:
            counts[key] = int(phase.get(key, "0"))
        except ValueError as exc:
            raise SystemExit(f"ERROR: invalid persistent test count for {expected}: {key}") from exc
    if sum(counts.values()) == 0:
        raise SystemExit(f"ERROR: required module test assembly executed zero tests: {expected}")
    if counts["skipped"] or counts["failed"] or counts["inconclusive"]:
        raise SystemExit(
            f"ERROR: required module test assembly did not all pass: {expected} "
            f"({counts['failed']} failed, {counts['skipped']} skipped, "
            f"{counts['inconclusive']} inconclusive)"
        )


def _validate_requested_summary(path: Path, expected_test: str) -> None:
    phase = _parse_summary(path)
    if phase.get("test") != expected_test:
        raise SystemExit("ERROR: persistent requested-test summary did not identify the requested test")
    counts = {}
    for key in ("passed", "failed", "skipped", "inconclusive"):
        counts[key] = int(phase.get(key, "0"))
    if sum(counts.values()) == 0:
        raise SystemExit("ERROR: requested filter matched zero tests")
    if counts["failed"] or counts["skipped"] or counts["inconclusive"]:
        raise SystemExit(
            f"ERROR: requested test did not pass ({counts['failed']} failed, "
            f"{counts['skipped']} skipped, {counts['inconclusive']} inconclusive)"
        )


def run_persistent_tests(
    unity: str,
    items: list[dict],
    root: Path,
    requested_test: str = "",
    requested_platform: str = "",
) -> float:
    edit_assemblies = _phase_assemblies(items, "EditMode")
    play_assemblies = _phase_assemblies(items, "PlayMode")
    unknown = sorted({item["platform"] for item in items if item["platform"] not in ("EditMode", "PlayMode")})
    if unknown:
        raise SystemExit("ERROR: unsupported module test platform(s): " + ", ".join(unknown))
    if requested_test and requested_platform not in ("EditMode", "PlayMode"):
        raise SystemExit("ERROR: requested test platform must be EditMode or PlayMode")

    out = root / "Tests" / "Persistent"
    out.mkdir(parents=True, exist_ok=True)
    selected_count = len(edit_assemblies) + len(play_assemblies) + (1 if requested_test else 0)
    max_minutes = max(8, min(18, 4 * max(1, selected_count)))
    env = os.environ.copy()
    env.update({
        "UNITY_BIN": unity,
        "UNITY_MAX_RSS_MB": "14336",
        "UNITY_MAX_MINUTES": str(max_minutes),
        "VOXEL_CI_EDITMODE_ASSEMBLIES": ";".join(edit_assemblies),
        "VOXEL_CI_PLAYMODE_ASSEMBLIES": ";".join(play_assemblies),
        "VOXEL_CI_RESULTS_ROOT": str(out.resolve()),
        "VOXEL_CI_BAKE_SHOWCASE": "0",
        "VOXEL_CI_PER_ASSEMBLY": "1",
        "VOXEL_CI_REQUESTED_TEST": requested_test,
        "VOXEL_CI_REQUESTED_PLATFORM": requested_platform,
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
        raise SystemExit("ERROR: persistent module validation failed: " + final.get("message", "missing final status"))

    for index, assembly in enumerate(edit_assemblies):
        _validate_phase_summary(out / f"persistent-editmode-{index}.txt", assembly)
    for index, assembly in enumerate(play_assemblies):
        _validate_phase_summary(out / f"persistent-playmode-{index}.txt", assembly)
    if requested_test:
        _validate_requested_summary(out / "persistent-requested.txt", requested_test)
    return seconds


def _requested_is_process_isolated(test_name: str) -> bool:
    return any(test_name == assembly or test_name.startswith(assembly + ".") for assembly in PROCESS_ISOLATED_ASSEMBLIES)


def _player_output_path(root: Path, item: dict) -> Path:
    """Preserve each scene/scenario's evidence, even within the same module.

    Include full identities in the digest: basenames, sanitized paths and plan positions
    alone collide. The readable prefix is bounded for filesystem component limits.
    """
    identity = json.dumps([item["module"], item["scene"], item["scenario"]],
                          ensure_ascii=True, separators=(",", ":"))
    digest = hashlib.sha256(identity.encode("utf-8")).hexdigest()[:16]
    module = "".join(c if c.isalnum() or c in "-_" else "_" for c in item["module"])[:120]
    scene = "".join(c if c.isalnum() or c in "-_" else "_" for c in Path(item["scene"]).stem)[:80]
    return root / "Players" / (module or "module") / f"{scene or 'scene'}-{digest}"


def main(argv=None) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--unity", required=True)
    ap.add_argument("--plan", required=True)
    ap.add_argument("--output", required=True)
    ap.add_argument("--requested-test", default="")
    ap.add_argument("--requested-platform", default="")
    ns = ap.parse_args(argv)
    plan = json.loads(Path(ns.plan).read_text(encoding="utf-8"))
    root = Path(ns.output)
    root.mkdir(parents=True, exist_ok=True)
    summary = {"tests": [], "players": [], "editorBatches": [], "totalSeconds": 0.0}
    started_all = time.monotonic()

    tests = plan.get("tests", [])
    persistent = [item for item in tests if item["assembly"] not in PROCESS_ISOLATED_ASSEMBLIES]
    isolated = [item for item in tests if item["assembly"] in PROCESS_ISOLATED_ASSEMBLIES]
    requested_isolated = bool(ns.requested_test and _requested_is_process_isolated(ns.requested_test))
    requested_covered = bool(
        ns.requested_test
        and not requested_isolated
        and _requested_test_covered_by_selected_assembly(
            ns.requested_test, ns.requested_platform, persistent
        )
    )
    persistent_requested = "" if requested_isolated or requested_covered else ns.requested_test
    persistent_requested_platform = "" if requested_isolated or requested_covered else ns.requested_platform

    if persistent or persistent_requested:
        seconds = run_persistent_tests(
            ns.unity,
            persistent,
            root,
            requested_test=persistent_requested,
            requested_platform=persistent_requested_platform,
        )
        summary["editorBatches"].append({
            "kind": "persistent",
            "editModeAssemblies": _phase_assemblies(persistent, "EditMode"),
            "playModeAssemblies": _phase_assemblies(persistent, "PlayMode"),
            "requestedTest": persistent_requested,
            "requestedTestCoveredByAssembly": ns.requested_test if requested_covered else "",
            "seconds": round(seconds, 2),
        })
        amortized = round(seconds / max(1, len(persistent) + (1 if persistent_requested else 0)), 2)
        for item in persistent:
            summary["tests"].append({**item, "seconds": amortized, "execution": "persistent-editor"})
        if persistent_requested:
            summary["requestedTest"] = {
                "test": persistent_requested,
                "platform": persistent_requested_platform,
                "seconds": amortized,
                "execution": "persistent-editor",
            }
        elif requested_covered:
            summary["requestedTest"] = {
                "test": ns.requested_test,
                "platform": ns.requested_platform,
                "seconds": amortized,
                "execution": "covered-by-module-assembly",
            }

    for item in isolated:
        seconds = run_test(ns.unity, item, root)
        summary["tests"].append({**item, "seconds": round(seconds, 2), "execution": "isolated-editor"})

    if requested_isolated:
        item = {"module": "requested", "platform": ns.requested_platform, "assembly": "VoxelEngine.Tests.PlayMode"}
        seconds = run_test(ns.unity, item, root, test_filter=ns.requested_test)
        summary["requestedTest"] = {
            "test": ns.requested_test,
            "platform": ns.requested_platform,
            "seconds": round(seconds, 2),
            "execution": "isolated-editor",
        }

    for item in plan.get("playerValidations", []):
        out = _player_output_path(root, item)
        player_env = os.environ.copy()
        player_env["VOXEL_DISABLE_GPU_CUTOVER"] = "1"
        started = time.monotonic()
        subprocess.run(["python3", "tools/player-validation.py", "--unity", ns.unity,
                        "--scene", item["scene"], "--scenario", item["scenario"],
                        "--output", str(out)], check=True, env=player_env)
        summary["players"].append({**item, "output": str(out), "seconds": round(time.monotonic() - started, 2)})
    summary["totalSeconds"] = round(time.monotonic() - started_all, 2)
    (root / "module-validation-summary.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(summary, sort_keys=True))
    return 0

if __name__ == "__main__":
    raise SystemExit(main())