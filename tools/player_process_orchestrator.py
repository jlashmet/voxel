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
BUILD_IDENTITY_MILESTONE = "build-identity"
BUILD_IDENTITY_TIMEOUT_SECONDS = 30.0
_SHA_RE = re.compile(r"^[0-9a-fA-F]{40}$")
_LIFECYCLE_OPS = {"launch", "wait", "terminate", "kill", "relaunch"}


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


@dataclass(frozen=True)
class LifecycleOperation:
    op: str
    role: str
    milestone: MilestoneExpectation | None = None


@dataclass
class RoleProcess:
    role: RoleSpec
    process: object
    root: Path
    player_log: Path
    stdout_log: Path
    stderr_log: Path
    attempt: int = 1


def _positive_number(value, name: str, minimum: float = 0.001, maximum: float = 3600.0) -> float:
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise OrchestrationError(f"{name} must be numeric")
    result = float(value)
    if result < minimum or result > maximum:
        raise OrchestrationError(f"{name} must be from {minimum:g} to {maximum:g}")
    return result


def _milestone(raw: Mapping[str, object], names: set[str], label: str) -> MilestoneExpectation:
    role = raw.get("role")
    name = raw.get("name")
    if role not in names:
        raise OrchestrationError(f"{label}.role must name a configured process")
    if not isinstance(name, str) or not name:
        raise OrchestrationError(f"{label}.name must be non-empty")
    timeout = _positive_number(raw.get("timeoutSeconds", 30), f"{label}.timeoutSeconds", 0.1, 600)
    fields = raw.get("fields", {})
    if not isinstance(fields, dict):
        raise OrchestrationError(f"{label}.fields must be an object")
    return MilestoneExpectation(str(role), name, timeout, dict(fields))


def _has_gameplay_semantic_wait(
    milestones: Sequence[MilestoneExpectation], operations: Sequence[LifecycleOperation]
) -> bool:
    if milestones:
        return any(item.name != BUILD_IDENTITY_MILESTONE for item in milestones)
    return any(
        item.op == "wait"
        and item.milestone is not None
        and item.milestone.name != BUILD_IDENTITY_MILESTONE
        for item in operations
    )


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
    if not isinstance(raw_milestones, list):
        raise OrchestrationError("milestones must be an array")
    milestones: list[MilestoneExpectation] = []
    for index, raw in enumerate(raw_milestones):
        if not isinstance(raw, dict):
            raise OrchestrationError(f"milestones[{index}] must be an object")
        milestones.append(_milestone(raw, names, f"milestones[{index}]"))

    raw_operations = data.get("operations", [])
    if not isinstance(raw_operations, list):
        raise OrchestrationError("operations must be an array")
    operations: list[LifecycleOperation] = []
    for index, raw in enumerate(raw_operations):
        if not isinstance(raw, dict):
            raise OrchestrationError(f"operations[{index}] must be an object")
        op = raw.get("op")
        role = raw.get("role")
        if op not in _LIFECYCLE_OPS:
            raise OrchestrationError(
                f"operations[{index}].op must be one of {', '.join(sorted(_LIFECYCLE_OPS))}"
            )
        if role not in names:
            raise OrchestrationError(f"operations[{index}].role must name a configured process")
        expectation = _milestone(raw, names, f"operations[{index}]") if op == "wait" else None
        operations.append(LifecycleOperation(str(op), str(role), expectation))

    if milestones and operations:
        raise OrchestrationError("use either milestones or operations, not both")
    if not milestones and not operations:
        raise OrchestrationError("milestones or operations must contain at least one semantic wait")
    if not _has_gameplay_semantic_wait(milestones, operations):
        raise OrchestrationError(
            "multi-process validation must include at least one gameplay semantic wait; "
            "build identity is verified automatically for every process"
        )

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
        "operations": operations,
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
    executables = [
        p for p in (apps[0] / "Contents" / "MacOS").iterdir()
        if p.is_file() and os.access(p, os.X_OK)
    ]
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
    attempt: int = 1,
) -> RoleProcess:
    role_root, env = role_environment(output_root, role, identity)
    attempt_root = role_root / f"attempt-{attempt:03d}"
    attempt_root.mkdir(parents=True, exist_ok=True)
    player_log = attempt_root / "player.log"
    stdout_log = attempt_root / "stdout.log"
    stderr_log = attempt_root / "stderr.log"
    args = [
        str(binary),
        "-logFile", str(player_log),
        "-screen-width", "1280", "-screen-height", "720", "-screen-fullscreen", "0",
        "-voxel-run-seconds", str(run_seconds),
        "-voxel-validation-role", role.name,
        "-voxel-validation-attempt", str(attempt),
        "-voxel-validation-source-sha", identity["sourceSha"],
        "-voxel-validation-executable-sha256", identity["executableSha256"],
        "-voxel-validation-state-root", env["VOXEL_VALIDATION_STATE_ROOT"],
    ]
    if role.headless:
        args.extend(["-batchmode", "-nographics"])
    args.extend(role.arguments)
    with stdout_log.open("wb") as stdout, stderr_log.open("wb") as stderr:
        process = subprocess.Popen(args, stdout=stdout, stderr=stderr, env=env)
    return RoleProcess(role, process, attempt_root, player_log, stdout_log, stderr_log, attempt)


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
        text = (
            record.player_log.read_text(encoding="utf-8", errors="replace")
            if record.player_log.exists() else ""
        )
        events = parse_milestones(text)
        for event in events:
            if _matches(event, expected):
                tagged = {"role": record.role.name, "attempt": record.attempt, **event}
                if tagged not in history:
                    history.append(tagged)
                return tagged
        status = record.process.poll()
        if status is not None:
            raise OrchestrationError(
                f"role {record.role.name} attempt {record.attempt} exited with {status} "
                f"before milestone {expected.name}; last milestones={events[-5:]}"
            )
        if monotonic() >= deadline:
            raise OrchestrationError(
                f"timed out waiting {expected.timeout_seconds:g}s for "
                f"{record.role.name}:{expected.name}; attempt={record.attempt} "
                f"last milestones={events[-5:]}"
            )
        sleep(poll_interval)


def _assert_logs(records: Iterable[RoleProcess], required: Sequence[str], forbidden: Sequence[str]) -> None:
    combined: list[str] = []
    for record in records:
        chunks = []
        for path in (record.player_log, record.stdout_log, record.stderr_log):
            if path.exists():
                chunks.append(path.read_text(encoding="utf-8", errors="replace"))
        combined.append(
            f"\n===== ROLE {record.role.name} ATTEMPT {record.attempt} =====\n" + "\n".join(chunks)
        )
    text = "\n".join(combined)
    for pattern in required:
        if pattern not in text:
            raise OrchestrationError(f"required multi-process log pattern missing: {pattern}")
    for pattern in forbidden:
        if pattern in text:
            raise OrchestrationError(f"forbidden multi-process log pattern found: {pattern}")


def _stop_record(record: RoleProcess, *, unexpected: bool) -> None:
    if record.process.poll() is not None:
        return
    if unexpected:
        record.process.kill()
        try:
            record.process.wait(timeout=2)
        except subprocess.TimeoutExpired:
            return
        return
    record.process.terminate()
    try:
        record.process.wait(timeout=5)
    except subprocess.TimeoutExpired:
        record.process.kill()
        try:
            record.process.wait(timeout=2)
        except subprocess.TimeoutExpired:
            pass


def _terminate(records: Iterable[RoleProcess]) -> None:
    for record in records:
        _stop_record(record, unexpected=False)


def _record_attempt(summary: dict, record: RoleProcess) -> dict:
    role_summary = summary["roles"].setdefault(record.role.name, {"attempts": []})
    attempt_summary = {
        "attempt": record.attempt,
        "pid": record.process.pid,
        "root": str(record.root),
        "playerLog": str(record.player_log),
        "stdoutLog": str(record.stdout_log),
        "stderrLog": str(record.stderr_log),
        "identityVerified": False,
    }
    role_summary["attempts"].append(attempt_summary)
    return attempt_summary


def _validate_build_identity(
    event: Mapping[str, object], expected_role: str, identity: Mapping[str, str]
) -> None:
    if (
        event.get("sourceSha") != identity["sourceSha"]
        or event.get("executableSha256") != identity["executableSha256"]
    ):
        raise OrchestrationError(f"role {expected_role} reported mismatched build identity: {event}")


def run(unity: str, scene: Path, output_root: Path, config: Mapping[str, object], source_sha: str) -> dict:
    output_root = output_root.resolve()
    output_root.mkdir(parents=True, exist_ok=True)
    binary, identity = build_player(unity, scene, output_root, source_sha)
    role_specs = {role.name: role for role in config["roles"]}
    current: dict[str, RoleProcess] = {}
    all_records: list[RoleProcess] = []
    attempts = {name: 0 for name in role_specs}
    history: list[dict] = []
    summary = {
        "sourceSha": identity["sourceSha"],
        "executableSha256": identity["executableSha256"],
        "roles": {},
        "milestones": history,
        "operations": [],
        "result": "running",
    }
    summary_path = output_root / "multi-process-summary.json"

    def start(role_name: str) -> RoleProcess:
        existing = current.get(role_name)
        if existing is not None and existing.process.poll() is None:
            raise OrchestrationError(f"role {role_name} is already running")
        attempts[role_name] += 1
        record = launch_role(
            binary,
            output_root,
            role_specs[role_name],
            identity,
            config["runSeconds"],
            attempts[role_name],
        )
        current[role_name] = record
        all_records.append(record)
        attempt_summary = _record_attempt(summary, record)
        identity_expected = MilestoneExpectation(
            role_name,
            BUILD_IDENTITY_MILESTONE,
            BUILD_IDENTITY_TIMEOUT_SECONDS,
            {},
        )
        identity_event = wait_for_milestone(record, identity_expected, history)
        _validate_build_identity(identity_event, role_name, identity)
        attempt_summary["identityVerified"] = True
        return record

    try:
        operations = config.get("operations", [])
        if operations:
            for operation in operations:
                op_summary = {"op": operation.op, "role": operation.role}
                if operation.op == "launch":
                    record = start(operation.role)
                    op_summary["attempt"] = record.attempt
                    op_summary["identityVerified"] = True
                elif operation.op == "relaunch":
                    existing = current.get(operation.role)
                    if existing is not None and existing.process.poll() is None:
                        raise OrchestrationError(f"role {operation.role} must be stopped before relaunch")
                    record = start(operation.role)
                    op_summary["attempt"] = record.attempt
                    op_summary["identityVerified"] = True
                elif operation.op in ("terminate", "kill"):
                    record = current.get(operation.role)
                    if record is None or record.process.poll() is not None:
                        raise OrchestrationError(f"role {operation.role} is not running")
                    _stop_record(record, unexpected=operation.op == "kill")
                    op_summary["attempt"] = record.attempt
                    op_summary["exitCode"] = record.process.poll()
                elif operation.op == "wait":
                    record = current.get(operation.role)
                    if record is None or record.process.poll() is not None:
                        raise OrchestrationError(f"role {operation.role} is not running")
                    event = wait_for_milestone(record, operation.milestone, history)
                    if operation.milestone.name == BUILD_IDENTITY_MILESTONE:
                        _validate_build_identity(event, operation.role, identity)
                    op_summary["attempt"] = record.attempt
                    op_summary["milestone"] = operation.milestone.name
                summary["operations"].append(op_summary)
        else:
            for role in config["roles"]:
                start(role.name)
            for expected in config["milestones"]:
                event = wait_for_milestone(current[expected.role], expected, history)
                if expected.name == BUILD_IDENTITY_MILESTONE:
                    _validate_build_identity(event, expected.role, identity)

        _assert_logs(all_records, config["required"], config["forbidden"])
        summary["result"] = "passed"
        return summary
    except Exception as exc:
        summary["result"] = "failed"
        summary["error"] = str(exc)
        raise
    finally:
        _terminate(all_records)
        records_by_attempt = {(record.role.name, record.attempt): record for record in all_records}
        for role_name, role_summary in summary["roles"].items():
            for attempt_summary in role_summary["attempts"]:
                record = records_by_attempt[(role_name, attempt_summary["attempt"])]
                attempt_summary["exitCode"] = record.process.poll()
        summary_path.write_text(json.dumps(summary, indent=2, sort_keys=True) + "\n", encoding="utf-8")
