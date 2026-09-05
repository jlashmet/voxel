#!/usr/bin/env python3
"""Validate and finish one bounded Astra manager review pass."""
from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any

import astra_manager as core
import astra_manager_publish as publisher


def validate_decision(root: Path, runtime: Path, decision_path: Path) -> dict[str, Any]:
    decision = core.load_json(decision_path)
    signal = core.load_json(root / runtime / "signal.json")
    selected = {str(key) for key in signal.get("selectedReviewKeys", []) if key}
    reviewed = [str(item.get("key", "")) for item in decision.get("reviewedItems", [])]

    if len(reviewed) != len(set(reviewed)):
        raise core.ManagerError("decision contains duplicate reviewedItems keys")
    outside = [key for key in reviewed if key not in selected]
    if outside:
        raise core.ManagerError(
            "decision attempted to review keys outside the bounded review window: " + ", ".join(outside)
        )

    followups = decision.get("followups", [])
    if not isinstance(followups, list):
        raise core.ManagerError("decision.followups must be a list")
    created_results = sum(item.get("result") == "follow-up-created" for item in decision.get("reviewedItems", []))
    if followups and created_results == 0:
        raise core.ManagerError("decision contains followups but no selected review is marked follow-up-created")
    if created_results and not followups:
        raise core.ManagerError("review marked follow-up-created but decision.followups is empty")
    return decision


def finish(root: Path, runtime: Path, decision_path: Path, repository: str) -> dict[str, Any]:
    validate_decision(root, runtime, decision_path)
    applied = core.apply(root, runtime, decision_path)
    publication: dict[str, Any] = {"published": []}
    if applied.get("createdSceneIssues"):
        publication = publisher.publish(root, runtime, repository)
    return {"applied": applied, "publication": publication}


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo")
    parser.add_argument("--runtime-dir", default=str(core.DEFAULT_RUNTIME))
    parser.add_argument("--decision")
    parser.add_argument("--github-repo", default="jlashmet/voxel")
    args = parser.parse_args(argv)

    try:
        root = core.root_dir(args.repo)
        runtime = Path(args.runtime_dir)
        decision = root / (Path(args.decision) if args.decision else runtime / "decision.json")
        print(json.dumps(finish(root, runtime, decision, args.github_repo), indent=2))
        return 0
    except core.ManagerError as exc:
        print(f"astra-manager-finish: {exc}", file=__import__("sys").stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
