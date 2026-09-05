#!/usr/bin/env python3
"""Budgeted entrypoint for the Astra SceneIssue manager.

The underlying collector may retain an arbitrarily large pending backlog locally. This
entrypoint exposes only a bounded review window to Astra so backlog growth cannot grow
the model's bootstrap context.
"""
from __future__ import annotations

import argparse
import json
import os
import shlex
import subprocess
from pathlib import Path
from typing import Any

import astra_manager as core


def select_review_window(pending: list[dict[str, Any]], budget: dict[str, Any]) -> list[dict[str, Any]]:
    suspicious_limit = max(0, int(budget.get("suspiciousItems", 2)))
    routine_limit = max(0, int(budget.get("routineCompletions", 5)))

    suspicious = [item for item in pending if item.get("priority") == "suspicious"][:suspicious_limit]
    selected_keys = {item.get("key") for item in suspicious}
    routine = [item for item in pending if item.get("key") not in selected_keys][:routine_limit]
    return suspicious + routine


def markdown_section(text: str, heading: str, max_lines: int = 40) -> list[str]:
    lines = text.splitlines()
    try:
        start = lines.index(heading)
    except ValueError:
        return []
    out = [heading]
    for line in lines[start + 1 :]:
        if line.startswith("## "):
            break
        out.append(line)
        if len(out) >= max_lines:
            out.append("… section truncated by manager context budget")
            break
    return out


def build_review_window(
    root: Path,
    runtime: Path,
    cfg: dict[str, Any],
    signal: dict[str, Any],
) -> Path:
    state = core.state(root, runtime)
    pending = list(state.get("pendingReviews", []))
    selected = select_review_window(pending, cfg.get("reviewBudget", {}))
    selected_keys = [str(item.get("key", "")) for item in selected]

    digest_path = root / runtime / "digest.md"
    digest_text = digest_path.read_text(encoding="utf-8", errors="replace") if digest_path.exists() else ""
    active = markdown_section(digest_text, "## Active SceneIssues", max_lines=32)

    output = root / runtime / "review-window.md"
    output.parent.mkdir(parents=True, exist_ok=True)
    lines = [
        "# Astra bounded review window",
        "",
        f"- Master: `{signal.get('masterSha', '')}`",
        f"- Generated: `{signal.get('generatedUtc', '')}`",
        f"- Total pending reviews: `{len(pending)}`",
        f"- Selected this wake-up: `{len(selected)}`",
        f"- Deferred by context budget: `{max(0, len(pending) - len(selected))}`",
        f"- Deep-investigation budget: `{int(cfg.get('reviewBudget', {}).get('deepInvestigations', 1))}`",
        "",
    ]
    if active:
        lines.extend(active)
        lines.append("")

    lines.extend(["## Review these items only", ""])
    if not selected:
        lines.append("- none")
    else:
        for item in selected:
            lines.append(f"### `{item.get('key', '')}`")
            lines.append("")
            lines.append(f"- Kind: `{item.get('kind', '')}`")
            lines.append(f"- Priority: `{item.get('priority', 'routine')}`")
            if item.get("issueId"):
                lines.append(f"- SceneIssue: `{item['issueId']}`")
            if item.get("sha"):
                lines.append(f"- SHA: `{item['sha']}`")
            if item.get("packet"):
                lines.append(f"- Completion packet: `{item['packet']}`")
            lines.append(f"- Reason: {item.get('reason', '')}")
            lines.append("")

    if len(pending) > len(selected):
        lines += [
            "## Deferred backlog",
            "",
            f"`{len(pending) - len(selected)}` additional review item(s) remain in local state.",
            "Do not load them during this wake-up; they will be surfaced by a later bounded review window.",
            "",
        ]

    output.write_text("\n".join(lines), encoding="utf-8")

    signal = dict(signal)
    signal["selectedReviewKeys"] = selected_keys
    signal["selectedReviewCount"] = len(selected)
    signal["deferredByBudgetCount"] = max(0, len(pending) - len(selected))
    signal["reviewWindow"] = str(output.relative_to(root))
    core.save_json(root / runtime / "signal.json", signal)
    return output


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo")
    parser.add_argument("--runtime-dir", default=str(core.DEFAULT_RUNTIME))
    parser.add_argument("--config")
    parser.add_argument("--github-repo", default="jlashmet/voxel")
    parser.add_argument("--fetch", action="store_true")
    parser.add_argument("--no-ci", action="store_true")
    parser.add_argument("--wake-command")
    args = parser.parse_args(argv)

    try:
        root = core.root_dir(args.repo)
        runtime = Path(args.runtime_dir)
        cfg = core.config(root, args.config)
        if args.fetch:
            core.fetch(root)
        signal = core.collect(root, runtime, cfg, args.github_repo, not args.no_ci)
        window = build_review_window(root, runtime, cfg, signal)

        if not signal.get("wakeAstra"):
            print("Astra not required: " + "; ".join(signal.get("reasons", [])))
            return 0

        wake = args.wake_command or os.environ.get("ASTRA_MANAGER_WAKE_COMMAND", "")
        prompt = root / "SceneIssues/manager/WAKEUP_PROMPT.md"
        if not wake:
            print("ASTRA_MANAGER_WAKE_REQUIRED")
            print(f"prompt={prompt}")
            print(f"review_window={window}")
            print(f"decision_output={root / runtime / 'decision.json'}")
            return 10

        env = os.environ.copy()
        env.update({
            "ASTRA_MANAGER_PROMPT": str(prompt),
            "ASTRA_MANAGER_REVIEW_WINDOW": str(window),
            # Keep the older name for simple launchers, but point it at the bounded file.
            "ASTRA_MANAGER_DIGEST": str(window),
            "ASTRA_MANAGER_DECISION_OUTPUT": str(root / runtime / "decision.json"),
            "ASTRA_MANAGER_MASTER_SHA": str(signal.get("masterSha", "")),
        })
        return subprocess.run(shlex.split(wake), cwd=root, env=env).returncode
    except core.ManagerError as exc:
        print(f"astra-manager-loop: {exc}", file=__import__("sys").stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
