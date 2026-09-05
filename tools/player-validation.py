#!/usr/bin/env python3
"""Run one declarative standalone-player validation through the shared capture harness."""
from __future__ import annotations
import argparse, json, subprocess
from pathlib import Path


def fail(msg: str) -> None:
    raise SystemExit(f"ERROR: {msg}")


def positive_int(value, name: str, minimum: int = 1, maximum: int = 600) -> int:
    if isinstance(value, bool) or not isinstance(value, int) or not minimum <= value <= maximum:
        fail(f"{name} must be an integer from {minimum} to {maximum}")
    return value


def optional_number(value, name: str):
    if value is None:
        return None
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        fail(f"{name} must be numeric")
    return value


def nonnegative_number(value, name: str):
    value = optional_number(value, name)
    if value is None:
        return 0
    if value < 0:
        fail(f"{name} must be non-negative")
    return value


def load_scenario(path: Path) -> dict:
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        fail(f"{path}: {exc}")
    if not isinstance(data, dict) or data.get("schemaVersion") != 1:
        fail(f"{path}: schemaVersion must be 1")
    run_seconds = positive_int(data.get("runSeconds"), "runSeconds", 10, 300)
    capture = data.get("capture")
    if not isinstance(capture, dict):
        fail("capture must be an object")
    width = positive_int(capture.get("width"), "capture.width", 320, 7680)
    height = positive_int(capture.get("height"), "capture.height", 240, 4320)
    interval = positive_int(capture.get("intervalSeconds"), "capture.intervalSeconds", 1, 60)
    minimum = positive_int(capture.get("minimumFrames"), "capture.minimumFrames", 1, 100)
    evidence_after = nonnegative_number(capture.get("evidenceAfterSeconds", 0),
                                        "capture.evidenceAfterSeconds")
    if evidence_after >= run_seconds:
        fail("capture.evidenceAfterSeconds must be less than runSeconds")
    timeline = data.get("timeline", {})
    if not isinstance(timeline, dict):
        fail("timeline must be an object")
    allowed_timeline = {"autoDialogue", "autowalkAfter", "convergingBuilds", "surveyAfter", "surveyHeight", "surveySpin"}
    unknown = set(timeline) - allowed_timeline
    if unknown:
        fail("unsupported timeline key(s): " + ", ".join(sorted(unknown)))
    for key in timeline:
        optional_number(timeline[key], f"timeline.{key}")
    player_arguments = data.get("playerArguments", [])
    if not isinstance(player_arguments, list) or any(
        not isinstance(value, str) or not value or "\x00" in value
        for value in player_arguments
    ):
        fail("playerArguments must be an array of non-empty strings")
    assertions = data.get("assertions", {})
    if not isinstance(assertions, dict):
        fail("assertions must be an object")
    required = assertions.get("requiredLogPatterns", [])
    forbidden = assertions.get("forbiddenLogPatterns", [])
    for field, values in (("requiredLogPatterns", required), ("forbiddenLogPatterns", forbidden)):
        if not isinstance(values, list) or any(not isinstance(v, str) or not v for v in values):
            fail(f"assertions.{field} must be an array of non-empty strings")
    return {"runSeconds": run_seconds, "width": width, "height": height, "interval": interval,
            "minimum": minimum, "evidenceAfter": evidence_after, "timeline": timeline,
            "playerArguments": player_arguments, "required": required, "forbidden": forbidden}


def main(argv=None) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--unity", required=True)
    ap.add_argument("--scene", required=True)
    ap.add_argument("--scenario", required=True)
    ap.add_argument("--output", required=True)
    ns = ap.parse_args(argv)
    scene, scenario = Path(ns.scene), Path(ns.scenario)
    if not scene.is_file() or scene.suffix != ".unity":
        fail(f"scene does not exist: {scene}")
    if not scenario.is_file() or not scenario.name.endswith(".player-scenario.json"):
        fail(f"scenario does not exist: {scenario}")
    cfg = load_scenario(scenario)
    cmd = ["bash", "tools/showcase-player-capture.sh", "--unity", ns.unity, "--output", ns.output,
           "--scene", scene.as_posix(), "--run-seconds", str(cfg["runSeconds"]),
           "--width", str(cfg["width"]), "--height", str(cfg["height"]),
           "--screenshot-every", str(cfg["interval"]), "--minimum-frames", str(cfg["minimum"]),
           "--evidence-after", str(cfg["evidenceAfter"])]
    flags = {"autoDialogue":"--auto-dialogue", "autowalkAfter":"--autowalk-after",
             "convergingBuilds":"--converging-builds", "surveyAfter":"--survey-after",
             "surveyHeight":"--survey-height", "surveySpin":"--survey-spin"}
    for key, flag in flags.items():
        if key in cfg["timeline"]:
            cmd += [flag, str(cfg["timeline"][key])]
    for value in cfg["playerArguments"]:
        cmd += ["--player-arg", value]
    for pattern in cfg["required"]:
        cmd += ["--require-log-pattern", pattern]
    for pattern in cfg["forbidden"]:
        cmd += ["--forbid-log-pattern", pattern]
    print("player-validation:", scene, "scenario=", scenario)
    subprocess.run(cmd, check=True)
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
