#!/usr/bin/env python3
"""Direct Codex CLI launcher for one fresh Astra manager review pass."""
from __future__ import annotations

import json
import re
import shutil
import subprocess
from pathlib import Path
from typing import Any

import astra_manager as core

DEFAULTS = {
    "binary": "codex",
    "minimumVersion": "0.153.0",
    "model": "gpt-6-astra",
    "reasoningEffort": "low",
    "sandbox": "read-only",
    "approvalPolicy": "never",
    "webSearch": "disabled",
}


def settings(cfg: dict[str, Any]) -> dict[str, Any]:
    value = dict(DEFAULTS)
    raw = cfg.get("codex", {})
    if raw is not None:
        if not isinstance(raw, dict):
            raise core.ManagerError("manager config codex must be an object")
        value.update(raw)
    return value


def parse_version(text: str) -> tuple[int, int, int]:
    match = re.search(r"(?<!\d)(\d+)\.(\d+)\.(\d+)(?!\d)", text)
    if not match:
        raise core.ManagerError(f"could not parse Codex CLI version from: {text.strip() or '(empty)'}")
    major, minor, patch = match.groups()
    return int(major), int(minor), int(patch)


def require_codex(cfg: dict[str, Any]) -> str:
    opts = settings(cfg)
    binary = str(opts["binary"])
    executable = shutil.which(binary)
    if not executable:
        raise core.ManagerError(
            f"Codex CLI not found: {binary}. Install/update Codex and sign in with ChatGPT before scheduling the manager loop."
        )
    result = subprocess.run(
        [executable, "--version"],
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        timeout=10,
    )
    if result.returncode:
        raise core.ManagerError(f"Codex CLI version check failed: {(result.stderr or result.stdout).strip()}")
    current = parse_version(result.stdout or result.stderr)
    minimum = parse_version(str(opts["minimumVersion"]))
    if current < minimum:
        raise core.ManagerError(
            f"Codex CLI {'.'.join(map(str, current))} is too old for Astra; require >= {opts['minimumVersion']}"
        )
    return executable


def build_command(
    cfg: dict[str, Any],
    executable: str,
    output_schema: Path,
    decision_output: Path,
) -> list[str]:
    opts = settings(cfg)
    return [
        executable,
        "exec",
        "--strict-config",
        "--ephemeral",
        "--ignore-user-config",
        "--color",
        "never",
        "--model",
        str(opts["model"]),
        "--sandbox",
        str(opts["sandbox"]),
        "--output-schema",
        str(output_schema),
        "--output-last-message",
        str(decision_output),
        "--config",
        f"model_reasoning_effort={json.dumps(str(opts['reasoningEffort']))}",
        "--config",
        f"approval_policy={json.dumps(str(opts['approvalPolicy']))}",
        "--config",
        f"web_search={json.dumps(str(opts['webSearch']))}",
        "-",
    ]


def invocation_prompt(root: Path, review_window: Path) -> str:
    relative = review_window.relative_to(root)
    return (
        "Run exactly one fresh Astra repository-manager pass for this checkout.\n"
        "Read and follow `SceneIssues/manager/WAKEUP_PROMPT.md`.\n"
        f"The only initial review payload is `{relative}`.\n"
        "Do not resume or inspect prior Codex sessions or conversation history.\n"
        "Do not implement, edit, create, or publish repository files.\n"
        "Return only the manager decision JSON as your final response. The Codex harness will "
        "write that structured response to `SceneIssues/manager/runtime/decision.json`, and the "
        "outer deterministic controller will validate/apply it after Codex exits.\n"
    )


def launch(root: Path, runtime: Path, cfg: dict[str, Any], review_window: Path) -> Path:
    decision = root / runtime / "decision.json"
    decision.parent.mkdir(parents=True, exist_ok=True)
    if decision.exists():
        decision.unlink()

    schema = root / "SceneIssues/manager/decision.schema.json"
    if not schema.exists():
        raise core.ManagerError(f"missing Astra manager decision schema: {schema.relative_to(root)}")

    before = core.git(root, "status", "--porcelain", "--untracked-files=all", check=False)
    executable = require_codex(cfg)
    command = build_command(cfg, executable, schema, decision)
    result = subprocess.run(
        command,
        cwd=root,
        input=invocation_prompt(root, review_window),
        text=True,
    )
    if result.returncode:
        raise core.ManagerError(f"Codex Astra manager exited with code {result.returncode}")

    after = core.git(root, "status", "--porcelain", "--untracked-files=all", check=False)
    if after != before:
        raise core.ManagerError(
            "Codex Astra manager changed tracked/untracked repository files; the manager is required to run read-only"
        )
    if not decision.exists():
        raise core.ManagerError("Codex Astra manager exited without producing runtime/decision.json")
    try:
        json.loads(decision.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        raise core.ManagerError(f"Codex Astra manager produced invalid decision JSON: {exc}") from exc
    return decision
