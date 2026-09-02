#!/usr/bin/env python3
"""Generic standalone-player screenshot evidence window handling."""
from __future__ import annotations

import argparse
import re
from pathlib import Path

_CAPTURE_TIME = re.compile(
    r"(?:^|[-_])t(?P<seconds>[0-9]+(?:\.[0-9]+)?)(?:s)?(?=[-_.]|$)"
)


def capture_seconds(name: str) -> float | None:
    """Return a timestamp encoded in a generic player-capture filename, if present."""
    match = _CAPTURE_TIME.search(name)
    return float(match.group("seconds")) if match else None


def prune_before(root: Path, threshold: float) -> list[Path]:
    """Delete timestamped PNG evidence captured before the semantic readiness window."""
    if threshold < 0:
        raise ValueError("evidence threshold must be non-negative")
    removed: list[Path] = []
    for shot in root.glob("*.png"):
        seconds = capture_seconds(shot.name)
        if seconds is not None and seconds < threshold:
            shot.unlink()
            removed.append(shot)
    return removed


def main(argv=None) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--screenshots", required=True)
    parser.add_argument("--evidence-after", type=float, required=True)
    ns = parser.parse_args(argv)
    if ns.evidence_after < 0:
        parser.error("--evidence-after must be non-negative")
    root = Path(ns.screenshots)
    if not root.is_dir():
        parser.error(f"screenshot directory does not exist: {root}")
    removed = prune_before(root, ns.evidence_after)
    print(f"pre-readiness screenshots removed: {len(removed)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
