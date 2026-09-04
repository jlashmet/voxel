#!/usr/bin/env python3
"""Generic build-once, multi-process standalone-player validation orchestration."""
from __future__ import annotations

import hashlib
import json
import os
import re
import shutil
import subprocess
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Callable, Iterable, Mapping, Sequence

MILESTONE_PREFIX = "VOXEL_VALIDATION_MILESTONE "
_SHA_RE = re.compile(r"^[0-9a-fA-F]{40}$")


class OrchestrationError(RuntimeError):
    pass


@dataclass(frozen=True)
class RoleSpec:
    name: str
    arguments: tuple[str, ...]
    environment: Mapping[str, str]
    headless: bool


@dataclass(frozen=True)
class MilestoneExpectation:
    role: str
    name: str
    timeout_seconds: float
    fields: Mapping[str, object]


@dataclass
class RoleProcess:
    role: RoleSpec
    process: object
    root: Path
    player_log: Path
    stdout_log: Path
    stderr_log: Path


def _positive_number(value, name: str, minimum: float = 0.001, maximum: float = 3600.0) -> float:
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise OrchestrationError(f"{name} must be numeric")
    result = float(value)
    if result < minimum or result > maximum:
        raise OrchestrationError(f"{name} must be from {minimum:g} to {maximum:g}")
    return result


def normalize_config(data: Mapping[str, object]) -> dict:
    if data.get("mode") != "multiProcess":
        raise OrchestrationError("mode must be 'multiProcess'")
    run_seconds = int(_positive_number(data.get("runSeconds"), "runSeconds", 10, 600))
    raw_roles = data.get("processes")
    if not isinstance(raw_roles, list) or not raw_roles:
        raise OrchestrationError("processes must be a non-empty array")

    roles: list[RoleSpec] = []
    names: set[str] = set()
    for index, raw in enumerate(raw_roles):
        if not isinstance(raw, dict):
            raise OrchestrationError(f"processes[{index}] must be an object")
        name = raw.get("role")
        if not isinstance(name, str) or not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9_.-]{0,63}", name):
            raise OrchestrationError(f"processes[{index}].role is invalid")
        if name in names:
            raise OrchestrationError(f"duplicate process role: {name}")
        names.add(name)
        args = raw.get("arguments", [])
        if not isinstance(args, list) or any(not isinstance(v, str) for v in args):
            raise OrchestrationError(f"processes[{index}].arguments must be an array of strings")
        env = raw.get("environment", {})
        if not isinstance(env, dict) or any(
            not isinstance(k, str) or not k or not isinstance(v, str) for k, v in env.items()
        ):
            raise OrchestrationError(f"processes[{index}].environment must be a string map")
        headless = raw.get("headless", True)
        if not isinstance(headless, bool):
            raise OrchestrationError(f"processes[{index}].headless must be boolean")
        roles.append(RoleSpec(name, tuple(args), dict(env), headless))

    raw_milestones = data.get("milestones", [])
    if not isinstance(raw_milestones, list) or not raw_milestones:
        raise OrchestrationError("milestones must be a non-empty array")
    milestones: list[MilestoneExpectation] = []
    for index, raw in enumerate(raw_milestones):
        if not isinstance(raw, dict):
            raise OrchestrationError(f"milestones[{index}] must be an object")
        role = raw.get("role")
        name = raw.get("name")
        if role not in names:
            raise OrchestrationError(f"milestones[{index}].role must name a configured process")
        if not isinstance(name, str) or not name:
            raise OrchestrationError(f"milestones[{index}].name must be non-empty")
        timeout = _positive_number(raw.get("timeoutSeconds", 30), f"milestones[{index}].timeoutSeconds", 0.1, 600)
        fields = raw.get("fields", {})
        if not isinstance(fields, dict):
            raise OrchestrationError(f"milestones[{index}].fields must be an object")
        milestones.append(MilestoneExpectation(role, name, timeout, dict(fields)))

    assertions = data.get("assertions", {})
    if not isinstance(assertions, dict):
        raise OrchestrationError("assertions must be an object")
    required = assertions.get("requiredLogPatterns", [])
    forbidden = assertions.get("forbiddenLogPatterns", [])
    for label, values in (("requiredLogPatterns", required), ("forbiddenLogPatterns", forbidden)):
        if not isinstance(values, list) or any(not isinstance(v, str) or not v for v in values):
            raise OrchestrationError(f"assertions.{label} must be an array of non-empty strings")

    return {
        "mode": "multiProcess",
        "runSeconds": run_seconds,
        "roles": roles,
        "milestones": milestones,
        "required": list(required),
        "forbidden": list(forbidden),
    }


def resolve_source_sha(explicit: str | None = None) -> str:
    candidate = explicit or os.environ.get("GITHUB_SHA")
    if not candidate:
        try:
            candidate = subprocess.check_output(
                ["git", "rev-parse", "HEAD"], text=True, stderr=subprocess.DEVNULL
            ).strip()
        except (OSError, subprocess.CalledProcessError) as exc:
            raise OrchestrationError(
                "source SHA is required (pass --source-sha, set GITHUB_SHA, or run inside the checkout)"
            ) from exc
    if not _SHA_RE.fullmatch(candidate):
        raise OrchestrationError(f"source SHA must be a 40-character git SHA: {candidate!r}")
    return candidate.lower()


def executable_sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def build_player(unity: str, scene: Path, output_root: Path, source_sha: str) -> tuple[Path, dict]:
    build_dir = output_root / "Player"
    build_log = output_root / "player-build.log"
    if build_dir.exists():
        shutil.rmtree(build_dir)
    build_dir.mkdir(parents=True)
    output_root.mkdir(parents=True, exist_ok=True)

    cmd = [
        "tools/unity-run.sh", "-batchmode", "-nographics", "-quit",
        "-projectPath", str(Path.cwd()),
        "-executeMethod", "VoxelEngine.Showcase.Editor.ShowcasePlayerBuild.Build",
        "-voxelScene", scene.as_posix(),
        "-voxelBuildOutput", str(build_dir),
        "-logFile", str(build_log),
    ]
    env = os.environ.copy()
    env["UNITY_BIN"] = unity
    env.setdefault("UNITY_MAX_RSS_MB", "12288")
    env.setdefault("UNITY_MAX_MINUTES", "25")
    subprocess.run(cmd, check=True, env=env)

    apps = sorted(build_dir.glob("*.app"))
    if len(apps) != 1:
        raise OrchestrationError(f"player build must produce exactly one .app, found {len(apps)}")
    executables = [p for p in (apps[0] / "Contents" / "MacOS").iterdir() if p.is_file() and os.access(p, os.X_OK)]
    if len(executables) != 1:
        raise OrchestrationError(f"player build must produce exactly one executable, found {len(executables)}")
    binary = executables[0].resolve()
    identity = {
        "sourceSha": source_sha,
        "executableSha256": executable_sha256(binary),
        "executable": str(binary),
    }
    (output_root / "build-identity.json").write_text(
        json.dumps(identity, indent=2, sort_keys=True) + "\n", encoding="utf-8"
    )
    return binary, identity


def role_environment(
    output_root: Path,
    role: RoleSpec,
    identity: Mapping[str, str],
    base_environment: Mapping[str, str] | None = None,
) -> tuple[Path, dict[str, str]]:
    role_root = output_root / "roles" / role.name
    home = role_root / "home"
    temp = role_root / "tmp"
    state = role_root / "state"
    config = role_root / "config"
    cache = role_root / "cache"
    for path in (role_root, home, temp, state, config, cache):
        path.mkdir(parents=True, exist_ok=True)

    env = dict(base_environment or os.environ)
    env.update({
        "HOME": str(home),
        "TMPDIR": str(temp),
        "TMP": str(temp),
        "TEMP": str(temp),
        "XDG_CONFIG_HOME": str(config),
        "XDG_CACHE_HOME": str(cache),
        "VOXEL_VALIDATION_STATE_ROOT": str(state),
        "VOXEL_VALIDATION_ROLE": role.name,
        "VOXEL_VALIDATION_SOURCE_SHA": identity["sourceSha"],
        "VOXEL_VALIDATION_EXECUTABLE_SHA256": identity["executableSha256"],
    })
    env.update(role.environment)
    return role_root, env


def launch_role(
    binary: Path,
    output_root: Path,
    role: RoleSpec,
    identity: Mapping[str, str],
    run_seconds: int,
) -> RoleProcess:
    role_root, env = role_environment(output_root, role, identity)
    player_log = role_root / "player.log"
    stdout_log = role_root / "stdout.log"
    stderr_log = role_root / "stderr.log"
    args = [
        str(binary),
        "-logFile", str(player_log),
        "-screen-width", "1280", "-screen-height", "720", "-screen-fullscreen", "0",
        "-voxel-run-seconds", str(run_seconds),
        "-voxel-validation-role", role.name,
        "-voxel-validation-source-sha", identity["sourceSha"],
        "-voxel-validation-executable-sha256", identity["executableSha256"],
        "-voxel-validation-state-root", env["VOXEL_VALIDATION_STATE_ROOT"],
    ]
    if role.headless:
        args.extend(["-batchmode", "-nographics"])
    args.extend(role.arguments)
    with stdout_log.open("wb") as stdout, stderr_log.open("wb") as stderr:
        process = subprocess.Popen(args, stdout=stdout, stderr=stderr, env=env)
    return RoleProcess(role, process, role_root, player_log, stdout_log, stderr_log)


def parse_milestones(text: str) -> list[dict]:
    events: list[dict] = []
    for line in text.splitlines():
        marker = line.find(MILESTONE_PREFIX)
        if marker < 0:
            continue
        payload = line[marker + len(MILESTONE_PREFIX):].strip()
        try:
            event = json.loads(payload)
        except json.JSONDecodeError:
            continue
        if isinstance(event, dict) and isinstance(event.get("name"), str) and event["name"]:
            events.append(event)
    return events


def _matches(event: Mapping[str, object], expected: MilestoneExpectation) -> bool:
    return event.get("name") == expected.name and all(
        event.get(key) == value for key, value in expected.fields.items()
    )


def wait_for_milestone(
    record: RoleProcess,
    expected: MilestoneExpectation,
    history: list[dict],
    *,
    poll_interval: float = 0.1,
    monotonic: Callable[[], float] = time.monotonic,
    sleep: Callable[[float], None] = time.sleep,
) -> dict:
    deadline = monotonic() + expected.timeout_seconds
    while True:
        text = record.player_log.read_text(encoding="utf-8", errors="replace") if record.player_log.exists() else ""
        events = parse_milestones(text)
        for event in events:
            if _matches(event, expected):
                tagged = {"role": record.role.name, **event}
                if tagged not in history:
                    history.append(tagged)
                return tagged
        status = record.process.poll()
        if status is not None:
            raise OrchestrationError(
                f"role {record.role.name} exited with {status} before milestone {expected.name}; last milestones={events[-5:]}"
            )
        if monotonic() >= deadline:
            raise OrchestrationError(
                f"timed out waiting {expected.timeout_seconds:g}s for {record.role.name}:{expected.name}; last milestones={events[-5:]}"
            )
        sleep(poll_interval)


def _assert_logs(records: Iterable[RoleProcess], required: Sequence[str], forbidden: Sequence[str]) -> None:
    combined: list[str] = []
    for record in records:
        chunks = []
        for path in (record.player_log, record.stdout_log, record.stderr_log):
            if path.exists():
                chunks.append(path.read_text(encoding="utf-8", errors="replace"))
        combined.append(f"\n===== ROLE {record.role.name} =====\n" + "\n".join(chunks))
    text = "\n".join(combined)
    for pattern in required:
        if pattern not in text:
            raise OrchestrationError(f"required multi-process log pattern missing: {pattern}")
    for pattern in forbidden:
        if pattern in text:
            raise OrchestrationError(f"forbidden multi-process log pattern found: {pattern}")


def _terminate(records: Iterable[RoleProcess]) -> None:
    records = list(records)
    for record in records:
        if record.process.poll() is None:
            record.process.terminate()
    deadline = time.monotonic() + 5
    for record in records:
        while record.process.poll() is None and time.monotonic() < deadline:
            time.sleep(0.05)
        if record.process.poll() is None:
            record.process.kill()
    for record in records:
        try:
            record.process.wait(timeout=2)
        except subprocess.TimeoutExpired:
            pass


def run(unity: str, scene: Path, output_root: Path, config: Mapping[str, object], source_sha: str) -> dict:
    output_root = output_root.resolve()
    output_root.mkdir(parents=True, exist_ok=True)
    binary, identity = build_player(unity, scene, output_root, source_sha)
    records: dict[str, RoleProcess] = {}
    history: list[dict] = []
    summary = {
        "sourceSha": identity["sourceSha"],
        "executableSha256": identity["executableSha256"],
        "roles": {},
        "milestones": history,
        "result": "running",
    }
    summary_path = output_root / "multi-process-summary.json"

    try:
        for role in config["roles"]:
            record = launch_role(binary, output_root, role, identity, config["runSeconds"])
            records[role.name] = record
            summary["roles"][role.name] = {
                "pid": record.process.pid,
                "root": str(record.root),
                "playerLog": str(record.player_log),
                "stdoutLog": str(record.stdout_log),
                "stderrLog": str(record.stderr_log),
            }

        for expected in config["milestones"]:
            event = wait_for_milestone(records[expected.role], expected, history)
            if expected.name == "build-identity":
                if event.get("sourceSha") != identity["sourceSha"] or event.get("executableSha256") != identity["executableSha256"]:
                    raise OrchestrationError(f"role {expected.role} reported mismatched build identity: {event}")

        _assert_logs(records.values(), config["required"], config["forbidden"])
        summary["result"] = "passed"
        return summary
    except Exception as exc:
        summary["result"] = "failed"
        summary["error"] = str(exc)
        raise
    finally:
        _terminate(records.values())
        for name, record in records.items():
            summary["roles"][name]["exitCode"] = record.process.poll()
        summary_path.write_text(json.dumps(summary, indent=2, sort_keys=True) + "\n", encoding="utf-8")
