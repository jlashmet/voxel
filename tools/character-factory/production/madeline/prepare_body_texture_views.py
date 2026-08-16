#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

VIEW_NAMES = ("front", "back", "left", "right")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Convert Madeline's tight base-layer turnaround into body-only texture references. "
            "The original images remain the geometry inputs; this only neutralizes the base layer "
            "for final source-color projection so clothing is not baked into the character texture."
        )
    )
    parser.add_argument("--input-dir", required=True)
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--report")
    return parser.parse_args()


def _garment_seed(arr: np.ndarray) -> np.ndarray:
    r, g, b = (arr[..., index] for index in range(3))
    chroma = np.max(arr, axis=2) - np.min(arr, axis=2)
    # The modeling base layer is a low-chroma warm beige. The gray studio background
    # has almost no chroma, exposed skin is warmer, and blonde hair is substantially
    # more chromatic. This intentionally favors false negatives over touching hair.
    return (
        (r > 145)
        & (g > 135)
        & (b > 110)
        & ((r - b) >= 7)
        & ((r - b) <= 58)
        & ((r - g) >= 1)
        & ((r - g) <= 38)
        & ((g - b) >= 3)
        & ((g - b) <= 32)
        & (chroma > 7)
    )


def _skin_sample(arr: np.ndarray, yy: np.ndarray) -> np.ndarray:
    r, g, b = (arr[..., index] for index in range(3))
    return (
        ((r - b) > 45)
        & ((r - g) > 20)
        & ((g - b) > 10)
        & (r > 180)
        & (g > 140)
        & (b > 105)
        & (yy > 0.27)
        & (yy < 0.92)
    )


def _body_region(seed: np.ndarray) -> np.ndarray:
    height, width = seed.shape
    region = np.zeros((height, width), dtype=bool)
    center_lo = int(width * 0.20)
    center_hi = int(width * 0.80)

    for y in range(height):
        xs = np.where(seed[y, center_lo:center_hi])[0] + center_lo
        if len(xs) < 3:
            continue
        lo = int(xs.min())
        hi = int(xs.max())
        span = hi - lo
        if span < 4 or span > int(width * 0.52):
            continue
        region[y, max(0, lo - 2) : min(width, hi + 3)] = True

    return np.asarray(
        Image.fromarray((region * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(3))
    ) > 0


def prepare_view(source: Path, destination: Path) -> dict[str, object]:
    image = Image.open(source).convert("RGB")
    arr = np.asarray(image).astype(np.float32)
    height, width = arr.shape[:2]
    r, g, b = (arr[..., index] for index in range(3))
    chroma = np.max(arr, axis=2) - np.min(arr, axis=2)
    yy = np.arange(height, dtype=np.float32)[:, None] / float(height)

    seed = _garment_seed(arr) & (yy > 0.235) & (yy < 0.625)
    region = _body_region(seed)

    foreground = (chroma > 6) | (((r + g + b) / 3.0) < 205)
    hair = ((r - b) > 70) | ((r - g) > 60)
    mask = region & foreground & (~hair)

    soft_mask = np.asarray(
        Image.fromarray((mask * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.2))
    ).astype(np.float32) / 255.0
    soft_mask *= (foreground & (~hair)).astype(np.float32)

    skin_mask = _skin_sample(arr, yy)
    skin_values = arr[skin_mask]
    skin = (
        np.median(skin_values, axis=0)
        if len(skin_values)
        else np.array([252.0, 211.0, 182.0], dtype=np.float32)
    )

    luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b
    smooth_luminance = np.asarray(
        Image.fromarray(luminance.astype(np.uint8)).filter(ImageFilter.GaussianBlur(8.0))
    ).astype(np.float32)
    median_luminance = float(np.median(smooth_luminance[mask])) if mask.any() else 220.0
    ratio = np.clip(smooth_luminance / max(median_luminance, 1.0), 0.90, 1.06)[..., None]
    skin_fill = np.clip(skin[None, None, :] * ratio, 0.0, 255.0)

    output = arr * (1.0 - soft_mask[..., None]) + skin_fill * soft_mask[..., None]
    destination.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(output.astype(np.uint8)).save(destination, quality=95, subsampling=0)

    output_arr = np.asarray(Image.open(destination).convert("RGB")).astype(np.float32)
    before = int(seed.sum())
    after = int((_garment_seed(output_arr) & (yy > 0.235) & (yy < 0.625)).sum())
    reduction = 1.0 - (after / max(before, 1))
    if reduction < 0.70:
        raise RuntimeError(
            f"{source.name}: body-only preparation removed only {reduction:.1%} of base-layer pixels"
        )

    return {
        "source": str(source),
        "output": str(destination),
        "width": width,
        "height": height,
        "base_layer_pixels_before": before,
        "base_layer_pixels_after": after,
        "base_layer_reduction": reduction,
        "sampled_skin_rgb": [round(float(value), 2) for value in skin],
    }


def main() -> int:
    args = parse_args()
    input_dir = Path(args.input_dir).resolve()
    output_dir = Path(args.output_dir).resolve()
    report_path = Path(args.report).resolve() if args.report else output_dir / "body-only-report.json"

    report: dict[str, object] = {"views": {}}
    for name in VIEW_NAMES:
        source = input_dir / f"{name}.jpg"
        if not source.is_file():
            raise RuntimeError(f"missing Madeline reference view: {source}")
        result = prepare_view(source, output_dir / f"{name}.jpg")
        report["views"][name] = result

    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(report_path)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
