#!/usr/bin/env python3
from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Crop the ornate head of the Sunlit Cleric staff for a TripoSR detail test. "
            "This is a deterministic crop of the original CI reference; TripoSR's own "
            "background-removal path performs the foreground isolation."
        )
    )
    parser.add_argument("--input", required=True)
    parser.add_argument("--output", required=True)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    source = Path(args.input).resolve()
    output = Path(args.output).resolve()
    image = Image.open(source).convert("RGB")
    width, height = image.size

    # The CI fixture is a stable crop from the original cleric artwork. Keep the
    # whole ornament plus a short length of shaft while excluding the hand/robe.
    box = (
        int(round(width * 0.273)),
        int(round(height * 0.013)),
        int(round(width * 0.781)),
        int(round(height * 0.352)),
    )
    cropped = image.crop(box)
    output.parent.mkdir(parents=True, exist_ok=True)
    cropped.save(output)
    print(output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
