#!/usr/bin/env python3
from __future__ import annotations

import argparse
from pathlib import Path

import cv2
import numpy as np
from PIL import Image


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Extract and enlarge the wider terminal detail from an already-isolated "
            "linear asset (staff head, pommel, lamp shade, tool head, etc.). The source "
            "is expected on a uniform neutral background."
        )
    )
    parser.add_argument("--input", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--axis", choices=("vertical", "horizontal"), default="vertical")
    parser.add_argument(
        "--terminal",
        choices=("min", "max"),
        default="min",
        help="min is top/left; max is bottom/right",
    )
    parser.add_argument("--background", type=int, default=128)
    parser.add_argument("--difference-threshold", type=int, default=8)
    parser.add_argument("--narrow-span-fraction", type=float, default=0.20)
    parser.add_argument("--narrow-run", type=int, default=10)
    parser.add_argument("--scan-start-fraction", type=float, default=0.08)
    parser.add_argument("--fallback-terminal-fraction", type=float, default=0.30)
    parser.add_argument("--neck-fraction", type=float, default=0.025)
    parser.add_argument("--canvas-size", type=int, default=768)
    parser.add_argument("--target-occupancy", type=float, default=0.88)
    return parser.parse_args()


def _validate(args: argparse.Namespace) -> None:
    if not 0 <= args.background <= 255:
        raise ValueError("background must be between 0 and 255")
    if args.difference_threshold < 0:
        raise ValueError("difference-threshold must be >= 0")
    if args.narrow_run < 1:
        raise ValueError("narrow-run must be >= 1")
    if args.canvas_size < 64:
        raise ValueError("canvas-size must be >= 64")
    for name in (
        "narrow_span_fraction",
        "scan_start_fraction",
        "fallback_terminal_fraction",
        "neck_fraction",
        "target_occupancy",
    ):
        value = float(getattr(args, name))
        if value <= 0.0 or value > 1.0:
            raise ValueError(name.replace("_", "-") + " must be in (0, 1]")


def foreground_mask(rgb: np.ndarray, background: int, threshold: int) -> np.ndarray:
    difference = np.max(
        np.abs(rgb.astype(np.int16) - int(background)),
        axis=2,
    )
    return difference > threshold


def _slice_span(mask: np.ndarray, primary: int) -> int:
    indexes = np.nonzero(mask)[0]
    return int(indexes.max() - indexes.min() + 1) if len(indexes) else 0


def terminal_bounds(
    foreground: np.ndarray,
    *,
    axis: str,
    terminal: str,
    narrow_span_fraction: float,
    narrow_run: int,
    scan_start_fraction: float,
    fallback_terminal_fraction: float,
    neck_fraction: float,
) -> tuple[int, int, int, int]:
    ys, xs = np.nonzero(foreground)
    if len(xs) == 0:
        raise RuntimeError("isolated linear asset contains no foreground")

    y_min, y_max = int(ys.min()), int(ys.max())
    x_min, x_max = int(xs.min()), int(xs.max())
    vertical = axis == "vertical"
    primary_min, primary_max = (y_min, y_max) if vertical else (x_min, x_max)
    cross_min, cross_max = (x_min, x_max) if vertical else (y_min, y_max)
    primary_extent = primary_max - primary_min + 1
    cross_extent = cross_max - cross_min + 1
    narrow_threshold = max(3, int(round(cross_extent * narrow_span_fraction)))
    scan_guard = max(1, int(round(primary_extent * scan_start_fraction)))

    coordinates = list(range(primary_min, primary_max + 1))
    if terminal == "max":
        coordinates.reverse()

    narrow_count = 0
    transition: int | None = None
    for coordinate in coordinates:
        distance = (
            coordinate - primary_min
            if terminal == "min"
            else primary_max - coordinate
        )
        if distance < scan_guard:
            continue
        slice_mask = foreground[coordinate, :] if vertical else foreground[:, coordinate]
        span = _slice_span(slice_mask, coordinate)
        if span <= narrow_threshold:
            narrow_count += 1
            if narrow_count >= narrow_run:
                transition = (
                    coordinate - narrow_count + 1
                    if terminal == "min"
                    else coordinate + narrow_count - 1
                )
                break
        else:
            narrow_count = 0

    if transition is None:
        fallback = max(1, int(round(primary_extent * fallback_terminal_fraction)))
        transition = (
            primary_min + fallback if terminal == "min" else primary_max - fallback
        )

    neck = max(1, int(round((foreground.shape[0] if vertical else foreground.shape[1]) * neck_fraction)))
    if terminal == "min":
        primary_a = primary_min
        primary_b = min(primary_max + 1, transition + neck)
    else:
        primary_a = max(primary_min, transition - neck)
        primary_b = primary_max + 1

    region = (
        foreground[primary_a:primary_b, :]
        if vertical
        else foreground[:, primary_a:primary_b]
    )
    local_ys, local_xs = np.nonzero(region)
    if len(local_xs) == 0:
        raise RuntimeError("terminal detail crop contains no foreground")

    if vertical:
        x0 = max(0, int(local_xs.min()) - 4)
        x1 = min(foreground.shape[1], int(local_xs.max()) + 5)
        y0, y1 = primary_a, primary_b
    else:
        y0 = max(0, int(local_ys.min()) - 4)
        y1 = min(foreground.shape[0], int(local_ys.max()) + 5)
        x0, x1 = primary_a, primary_b
    return x0, y0, x1, y1


def fit_on_canvas(
    rgb: np.ndarray,
    foreground: np.ndarray,
    bounds: tuple[int, int, int, int],
    *,
    background: int,
    canvas_size: int,
    target_occupancy: float,
) -> Image.Image:
    x0, y0, x1, y1 = bounds
    cropped_rgb = rgb[y0:y1, x0:x1]
    cropped_mask = foreground[y0:y1, x0:x1]
    crop_h, crop_w = cropped_mask.shape
    if crop_h <= 0 or crop_w <= 0:
        raise RuntimeError("terminal detail crop is empty")

    target_extent = int(round(canvas_size * target_occupancy))
    scale = min(target_extent / crop_h, target_extent / crop_w)
    out_w = max(1, int(round(crop_w * scale)))
    out_h = max(1, int(round(crop_h * scale)))
    resized_rgb = cv2.resize(cropped_rgb, (out_w, out_h), interpolation=cv2.INTER_LANCZOS4)
    resized_mask = cv2.resize(
        cropped_mask.astype(np.uint8),
        (out_w, out_h),
        interpolation=cv2.INTER_NEAREST,
    ) > 0

    canvas = np.full((canvas_size, canvas_size, 3), background, dtype=np.uint8)
    x = (canvas_size - out_w) // 2
    y = (canvas_size - out_h) // 2
    roi = canvas[y : y + out_h, x : x + out_w]
    roi[resized_mask] = resized_rgb[resized_mask]
    return Image.fromarray(canvas, mode="RGB")


def main() -> int:
    args = parse_args()
    _validate(args)
    source = Path(args.input).resolve()
    output = Path(args.output).resolve()
    rgb = np.asarray(Image.open(source).convert("RGB"))
    foreground = foreground_mask(rgb, args.background, args.difference_threshold)
    bounds = terminal_bounds(
        foreground,
        axis=args.axis,
        terminal=args.terminal,
        narrow_span_fraction=args.narrow_span_fraction,
        narrow_run=args.narrow_run,
        scan_start_fraction=args.scan_start_fraction,
        fallback_terminal_fraction=args.fallback_terminal_fraction,
        neck_fraction=args.neck_fraction,
    )
    image = fit_on_canvas(
        rgb,
        foreground,
        bounds,
        background=args.background,
        canvas_size=args.canvas_size,
        target_occupancy=args.target_occupancy,
    )
    output.parent.mkdir(parents=True, exist_ok=True)
    image.save(output, format="PNG", optimize=False, compress_level=9)
    print(output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
