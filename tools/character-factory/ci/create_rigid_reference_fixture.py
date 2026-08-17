#!/usr/bin/env python3
from __future__ import annotations

import argparse
from pathlib import Path
import sys

import bpy


def parse_args() -> argparse.Namespace:
    argv = sys.argv
    if "--" not in argv:
        raise SystemExit("Expected Blender arguments after '--'")
    argv = argv[argv.index("--") + 1 :]
    parser = argparse.ArgumentParser(
        description="Create a neutral-canvas rigid reference with a detached ornament and speckles"
    )
    parser.add_argument("--output", required=True)
    return parser.parse_args(argv)


def paint_rect(
    pixels: list[float],
    width: int,
    *,
    x0: int,
    y0: int,
    x1: int,
    y1: int,
    color: tuple[float, float, float, float],
) -> None:
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            index = (y * width + x) * 4
            pixels[index : index + 4] = color


def main() -> int:
    args = parse_args()
    output = Path(args.output).resolve()
    output.parent.mkdir(parents=True, exist_ok=True)

    width = 512
    height = 512
    background = (0.5, 0.5, 0.5, 1.0)
    pixels = list(background) * (width * height)

    # Main rigid body, plus a deliberately detached ornament that is large enough
    # to be meaningful but far smaller than the body.
    paint_rect(
        pixels,
        width,
        x0=235,
        y0=60,
        x1=276,
        y1=450,
        color=(0.42, 0.16, 0.05, 1.0),
    )
    paint_rect(
        pixels,
        width,
        x0=145,
        y0=330,
        x1=170,
        y1=355,
        color=(0.95, 0.58, 0.08, 1.0),
    )

    # Compression-noise analogues: these should be discarded by the rigid
    # component selector even though they pass the foreground color test.
    for x, y in ((20, 20), (480, 75), (35, 470)):
        index = (y * width + x) * 4
        pixels[index : index + 4] = (0.9, 0.1, 0.1, 1.0)

    image = bpy.data.images.new("RigidMultipartReference", width=width, height=height, alpha=True)
    image.pixels[:] = pixels
    image.filepath_raw = str(output)
    image.file_format = "PNG"
    image.save()

    if not output.is_file() or output.stat().st_size == 0:
        raise RuntimeError(f"failed to create rigid reference fixture: {output}")
    print(output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
