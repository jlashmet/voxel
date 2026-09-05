#!/usr/bin/env python3
"""Budgeted entrypoint for the Astra SceneIssue manager.

The underlying collector may retain an arbitrarily large pending backlog locally. This
entrypoint keeps a dedicated manager checkout synchronized with origin/master and exposes
only a bounded review window to Astra.
"""
from __future__ import annotations

import argparse
import json
import os
import shlex
import shutil
import subprocess
from pathlib import Path
from typing import Any

import astra_manager as core
import astra_manager_codex as codex_launcher
import astra_manager_finish as finisher
import astra_manager_publish as publisher


def select_review_window(pending: list[dict[str, Any]], budget: dict[str, Any]) -> list[dict[str, Any]]:
    suspicious_limit = max(0, int(budget.get("suspiciousItems", 2)))
    routine_limit = max(0, int(budget.get("routineCompletions", 5)))

    suspicious = [item for item in pending if item.get("priority") == "suspicious"][:suspicious_limit]
    routine = [item for item in pending if item.get("priority") != "suspicious"][:routine_limit]
    return suspicious + routine


def _git_ok(root: Path, *args: str) -> bool:
    return subprocess.run(
        ["git", "-C", str(root), *args],
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    ).returncode == 0


def _manager_followup(path: Path) -> bool:
    issue = path / "issue.json"
    if not issue.exists():
        return False
    try:
        data = json.loads(issue.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return False
    return str(data.get("note", "")).startswith("MANAGER FOLLOW-UP /") and data.get("status") == "open"


def _untracked_issue_dirs(root: Path) -> list[Path]:
    out = core.git(root, "status", "--porcelain", "--untracked-files=all", "--", "SceneIssues/open", check=False)
    issue_ids: set[str] = set()
    for line in out.splitlines():
        if not line.startswith("?? "):
            continue
        rel = line[3:].strip()
        parts = Path(rel).parts
        if len(parts) >= 3 and parts[0:2] == ("SceneIssues", "open") and core.ISSUE_ID_RE.match(parts[2]):
            issue_ids.add(parts[2])
    return [root / "SceneIssues/open" / issue_id for issue_id in sorted(issue_ids)]


def _load_published(root: Path, runtime: Path) -> dict[str, Any]:
    path = root / runtime / "published-followups.json"
    return core.load_json(path, {}) if path.exists() else {}


def _save_published(root: Path, runtime: Path, value: dict[str, Any]) -> None:
    core.save_json(root / runtime / "published-followups.json", value)


def sync_master_worktree(root: Path, runtime: Path) -> None:
    """Make file reads match origin/master without losing manager follow-ups awaiting merge."""
    branch = core.git(root, "branch", "--show-current", check=False)
    if branch != "master":
        raise core.ManagerError(
            f"dedicated Astra manager checkout must remain on local master; current branch is {branch or '(detached)'}"
        )

    tracked = core.git(root, "status", "--porcelain", "--untracked-files=no", check=False)
    if tracked:
        raise core.ManagerError("manager checkout has tracked local changes; refuse to overwrite them")

    candidates = _untracked_issue_dirs(root)
    for path in candidates:
        if not _manager_followup(path):
            raise core.ManagerError(f"unexpected untracked SceneIssue in manager checkout: {path.relative_to(root)}")

    # A published follow-up remains untracked locally so duplicate prevention can see it while
    # its PR is pending. Once the same issue arrives on origin/master, remove the local copy
    # before fast-forwarding so Git is not blocked by an untracked-file collision.
    published = _load_published(root, runtime)
    for path in candidates:
        rel_issue = f"SceneIssues/open/{path.name}/issue.json"
        if _git_ok(root, "cat-file", "-e", f"origin/master:{rel_issue}"):
            shutil.rmtree(path)
            published.pop(path.name, None)
    _save_published(root, runtime, published)

    remaining = core.git(root, "status", "--porcelain", "--untracked-files=all", check=False)
    allowed_prefixes = {f"?? SceneIssues/open/{p.name}/" for p in _untracked_issue_dirs(root)}
    unexpected = []
    for line in remaining.splitlines():
        if line.startswith("?? ") and any(line.startswith(prefix) for prefix in allowed_prefixes):
            continue
        unexpected.append(line)
    if unexpected:
        raise core.ManagerError("manager checkout contains unexpected local files: " + "; ".join(unexpected[:5]))

    core.git(root, "merge", "--ff-only", "origin/master")


def retry_followup_transport(root: Path, runtime: Path, repository: str) -> None:
    """Retry only unpublished/auto-merge-incomplete follow-ups; never spend Astra on transport."""
    candidates = _untracked_issue_dirs(root)
    if not candidates:
        return
    published = _load_published(root, runtime)
    needs_transport = any(
        path.name not in published or not published[path.name].get("autoMergeEnabled")
        for path in candidates
    )
    if needs_transport:
        publisher.publish(root, runtime, repository)


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
    parser.add_argument("--wake-command", help="legacy/custom wake launcher override; default is direct Codex CLI + GPT-6 Astra")
    args = parser.parse_args(argv)

    try:
        root = core.root_dir(args.repo)
        runtime = Path(args.runtime_dir)
        cfg = core.config(root, args.config)
        if args.fetch:
            core.fetch(root)
            sync_master_worktree(root, runtime)
            retry_followup_transport(root, runtime, args.github_repo)
        signal = core.collect(root, runtime, cfg, args.github_repo, not args.no_ci)
        window = build_review_window(root, runtime, cfg, signal)

        if not signal.get("wakeAstra"):
            print("Astra not required: " + "; ".join(signal.get("reasons", [])))
            return 0

        # Explicit override remains available for testing/emergency compatibility. Normal
        # operation launches a fresh ephemeral Codex CLI session using GPT-6 Astra directly.
        wake = args.wake_command or os.environ.get("ASTRA_MANAGER_WAKE_COMMAND", "")
        if wake:
            prompt = root / "SceneIssues/manager/WAKEUP_PROMPT.md"
            env = os.environ.copy()
            env.update({
                "ASTRA_MANAGER_PROMPT": str(prompt),
                "ASTRA_MANAGER_REVIEW_WINDOW": str(window),
                "ASTRA_MANAGER_DIGEST": str(window),
                "ASTRA_MANAGER_DECISION_OUTPUT": str(root / runtime / "decision.json"),
                "ASTRA_MANAGER_MASTER_SHA": str(signal.get("masterSha", "")),
            })
            return subprocess.run(shlex.split(wake), cwd=root, env=env).returncode

        decision = codex_launcher.launch(root, runtime, cfg, window)
        result = finisher.finish(root, runtime, decision, args.github_repo)
        print(json.dumps(result, indent=2))
        return 0
    except core.ManagerError as exc:
        print(f"astra-manager-loop: {exc}", file=__import__("sys").stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
