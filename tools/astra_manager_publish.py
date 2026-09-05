#!/usr/bin/env python3
"""Publish Astra-created follow-up SceneIssues through a normal protected-master PR.

This is deterministic transport only. It never edits implementation code and never waits
for the PR to merge; it creates the PR, enables auto-merge, records it locally, and exits.
"""
from __future__ import annotations

import argparse
import datetime as dt
import json
import os
import subprocess
import tempfile
from pathlib import Path
from typing import Any

import astra_manager as core


def _run(root: Path, args: list[str], *, env: dict[str, str] | None = None, check: bool = True) -> str:
    merged_env = os.environ.copy()
    if env:
        merged_env.update(env)
    p = subprocess.run(args, cwd=root, env=merged_env, text=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if check and p.returncode:
        raise core.ManagerError(f"{' '.join(args)} failed: {p.stderr.strip()}")
    return p.stdout.strip()


def _manager_followup(path: Path) -> dict[str, Any] | None:
    issue = path / "issue.json"
    if not issue.exists():
        return None
    try:
        data = json.loads(issue.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return None
    if not str(data.get("note", "")).startswith("MANAGER FOLLOW-UP /") or data.get("status") != "open":
        return None
    if not (path / "plan.md").exists() or not (path / "tasks.md").exists():
        return None
    return data


def untracked_followups(root: Path) -> list[Path]:
    tracked = core.git(root, "status", "--porcelain", "--untracked-files=no", check=False)
    if tracked:
        raise core.ManagerError("manager checkout has tracked local changes; follow-up publisher refuses to mix them into a PR")

    status = core.git(root, "status", "--porcelain", "--untracked-files=all", "--", "SceneIssues/open", check=False)
    ids: set[str] = set()
    for line in status.splitlines():
        if not line.startswith("?? "):
            raise core.ManagerError(f"publisher only accepts new untracked SceneIssues, found: {line}")
        parts = Path(line[3:].strip()).parts
        if len(parts) >= 3 and parts[:2] == ("SceneIssues", "open") and core.ISSUE_ID_RE.match(parts[2]):
            ids.add(parts[2])

    paths = [root / "SceneIssues/open" / issue_id for issue_id in sorted(ids)]
    for path in paths:
        if _manager_followup(path) is None:
            raise core.ManagerError(f"untracked SceneIssue is not a valid Astra manager follow-up: {path.relative_to(root)}")
    return paths


def load_published(root: Path, runtime: Path) -> dict[str, Any]:
    path = root / runtime / "published-followups.json"
    return core.load_json(path, {}) if path.exists() else {}


def save_published(root: Path, runtime: Path, value: dict[str, Any]) -> None:
    core.save_json(root / runtime / "published-followups.json", value)


def choose_merge_flag(settings: dict[str, Any]) -> str:
    if settings.get("allow_merge_commit"):
        return "--merge"
    if settings.get("allow_squash_merge"):
        return "--squash"
    if settings.get("allow_rebase_merge"):
        return "--rebase"
    raise core.ManagerError("repository reports no supported pull-request merge method")


def _create_commit(root: Path, paths: list[Path], parent: str, message: str) -> str:
    with tempfile.TemporaryDirectory(prefix="astra-manager-index-") as tmp:
        index = str(Path(tmp) / "index")
        env = {"GIT_INDEX_FILE": index}
        _run(root, ["git", "read-tree", parent], env=env)
        rels = [str(path.relative_to(root)) for path in paths]
        _run(root, ["git", "add", "--", *rels], env=env)
        tree = _run(root, ["git", "write-tree"], env=env)
        return _run(root, ["git", "commit-tree", tree, "-p", parent, "-m", message])


def publish(root: Path, runtime: Path, repository: str) -> dict[str, Any]:
    if not shutil_which("gh"):
        raise core.ManagerError("gh CLI is required to publish manager follow-up SceneIssues")

    core.fetch(root)
    all_followups = untracked_followups(root)
    published = load_published(root, runtime)
    fresh = [path for path in all_followups if path.name not in published]
    if not fresh:
        return {"published": [], "alreadyPublished": [path.name for path in all_followups]}

    parent = core.git(root, "rev-parse", "origin/master")
    stamp = dt.datetime.now(dt.timezone.utc).replace(microsecond=0)
    branch = f"astra-manager/followups-{stamp.strftime('%Y%m%d-%H%M%S')}"
    issue_ids = [path.name for path in fresh]
    message = "SceneIssues: publish Astra manager follow-ups\n\n" + "\n".join(f"- {issue_id}" for issue_id in issue_ids)
    commit = _create_commit(root, fresh, parent, message)

    _run(root, ["git", "push", "origin", f"{commit}:refs/heads/{branch}"])

    title = f"SceneIssues: Astra manager follow-ups {stamp.strftime('%Y-%m-%d %H:%M UTC')}"
    body = "Astra manager generated concrete follow-up SceneIssues only; implementation remains owned by normal agents.\n\n" + "\n".join(
        f"- `{issue_id}`" for issue_id in issue_ids
    )
    pr_url = _run(
        root,
        ["gh", "pr", "create", "--repo", repository, "--base", "master", "--head", branch, "--title", title, "--body", body],
    ).splitlines()[-1]

    settings_text = _run(root, ["gh", "api", f"repos/{repository}"])
    try:
        merge_flag = choose_merge_flag(json.loads(settings_text))
    except json.JSONDecodeError as exc:
        raise core.ManagerError(f"could not parse repository merge settings: {exc}") from exc

    auto = subprocess.run(
        ["gh", "pr", "merge", pr_url, "--repo", repository, "--auto", merge_flag],
        cwd=root, text=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
    )

    record = {
        "branch": branch,
        "commit": commit,
        "parentMasterSha": parent,
        "pr": pr_url,
        "publishedUtc": core.iso(stamp),
        "autoMergeEnabled": auto.returncode == 0,
        "autoMergeError": auto.stderr.strip() if auto.returncode else "",
    }
    for issue_id in issue_ids:
        published[issue_id] = dict(record)
    save_published(root, runtime, published)

    if auto.returncode:
        raise core.ManagerError(
            f"follow-up PR was created at {pr_url}, but auto-merge could not be enabled: {auto.stderr.strip()}"
        )
    return {"published": issue_ids, "pr": pr_url, "branch": branch, "commit": commit, "autoMergeEnabled": True}


def shutil_which(command: str) -> str | None:
    # Kept behind a tiny wrapper for deterministic unit tests.
    import shutil
    return shutil.which(command)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo")
    parser.add_argument("--runtime-dir", default=str(core.DEFAULT_RUNTIME))
    parser.add_argument("--github-repo", default="jlashmet/voxel")
    args = parser.parse_args(argv)
    try:
        root = core.root_dir(args.repo)
        result = publish(root, Path(args.runtime_dir), args.github_repo)
        print(json.dumps(result, indent=2))
        return 0
    except core.ManagerError as exc:
        print(f"astra-manager-publish: {exc}", file=__import__("sys").stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
