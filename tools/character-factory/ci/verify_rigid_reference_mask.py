#!/usr/bin/env python3
from __future__ import annotations

import argparse
from pathlib import Path
import sys

SCRIPT_DIR = Path(__file__).resolve().parent
RUNTIME_DIR = SCRIPT_DIR.parent / "runtime"
if str(RUNTIME_DIR) not in sys.path:
    sys.path.insert(0, str(RUNTIME_DIR))

from blender_rigid_reference import load_rigid_subject_image


def parse_args() -> argparse.Namespace:
    argv = sys.argv
    if "--" not in argv:
        raise SystemExit("Expected Blender arguments after '--'")
    argv = argv[argv.index("--") + 1 :]
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True)
    return parser.parse_args(argv)


def contains(info, x: int, y: int) -> bool:
    return any(start <= x <= end for start, end in info.foreground_runs[y])


def main() -> int:
    args = parse_args()
    info = load_rigid_subject_image(Path(args.input).resolve())

    # Main body and detached ornament must both survive.
    if not contains(info, 255, 250):
        raise RuntimeError("rigid mask lost the main fixture body")
    if not contains(info, 155, 342):
        raise RuntimeError("rigid mask lost the detached ornament")

    # Deliberate one-pixel noise must not survive.
    for x, y in ((20, 20), (480, 75), (35, 470)):
        if contains(info, x, y):
            raise RuntimeError(f"rigid mask retained compression speckle at ({x},{y})")

    print(
        "CI_RIGID_REFERENCE_MASK_OK "
        f"bbox=({info.x0},{info.y0})-({info.x1},{info.y1})",
        flush=True,
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
