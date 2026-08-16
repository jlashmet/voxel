#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import shutil
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter, ImageFile

ImageFile.LOAD_TRUNCATED_IMAGES = True
VIEW_NAMES = ("front", "back", "left", "right")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Convert Madeline's tight neutral turnaround layer into seamless body-only "
            "conditioning views while preserving the approved silhouette, face, and hair."
        )
    )
    parser.add_argument("--input-dir", required=True)
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--preclean-dir")
    parser.add_argument("--report")
    return parser.parse_args()


def _garment_seed(arr: np.ndarray) -> np.ndarray:
    r, g, b = (arr[..., index] for index in range(3))
    chroma = np.max(arr, axis=2) - np.min(arr, axis=2)
    return (
        (r > 145) & (g > 135) & (b > 110)
        & ((r - b) >= 12) & ((r - b) <= 55)
        & ((r - g) >= 4) & ((r - g) <= 32)
        & ((g - b) >= 3) & ((g - b) <= 27)
        & (chroma > 10)
    )


def _skin_sample(arr: np.ndarray, yy: np.ndarray) -> np.ndarray:
    r, g, b = (arr[..., index] for index in range(3))
    return (
        ((r - b) > 45) & ((r - g) > 20) & ((g - b) > 10)
        & (r > 180) & (g > 140) & (b > 105)
        & (yy > 0.27) & (yy < 0.92)
    )


def _body_region(seed: np.ndarray) -> np.ndarray:
    height, width = seed.shape
    center_lo = int(width * 0.20)
    center_hi = int(width * 0.80)
    rows: list[tuple[int, int, int]] = []
    for y in range(height):
        xs = np.where(seed[y, center_lo:center_hi])[0] + center_lo
        if len(xs) < 3:
            continue
        lo = int(xs.min())
        hi = int(xs.max())
        if (y / float(height)) < 0.36:
            lo = max(lo, int(width * 0.30))
            hi = min(hi, int(width * 0.70))
        span = hi - lo
        if span < 4 or span > int(width * 0.52):
            continue
        rows.append((y, lo, hi))

    region = np.zeros((height, width), dtype=bool)
    if not rows:
        return region

    ys = np.asarray([row[0] for row in rows], dtype=np.float32)
    los = np.asarray([row[1] for row in rows], dtype=np.float32)
    his = np.asarray([row[2] for row in rows], dtype=np.float32)
    start = max(0, int(ys.min()) - 8)
    end = min(height - 1, int(ys.max()) + 10)
    for y in range(start, end + 1):
        lo = int(round(float(np.interp(y, ys, los)))) - 4
        hi = int(round(float(np.interp(y, ys, his)))) + 4
        if (y / float(height)) < 0.36:
            lo = max(lo, int(width * 0.29))
            hi = min(hi, int(width * 0.71))
        region[y, max(0, lo) : min(width, hi + 1)] = True

    region = np.asarray(
        Image.fromarray((region * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(15))
    ) > 0
    yy = np.arange(height, dtype=np.float32)[:, None] / float(height)
    xx = np.arange(width, dtype=np.float32)[None, :] / float(width)
    region[(yy < 0.36) & ((xx < 0.285) | (xx > 0.715))] = False
    return region


def prepare_view(source: Path, destination: Path) -> dict[str, object]:
    image = Image.open(source).convert("RGB")
    arr = np.asarray(image).astype(np.float32)
    height, width = arr.shape[:2]
    r, g, b = (arr[..., index] for index in range(3))
    chroma = np.max(arr, axis=2) - np.min(arr, axis=2)
    yy = np.arange(height, dtype=np.float32)[:, None] / float(height)

    seed = _garment_seed(arr) & (yy > 0.225) & (yy < 0.64)
    region = _body_region(seed)
    if not region.any():
        raise RuntimeError(f"{source.name}: failed to infer torso/hip cleanup region")

    foreground = (chroma > 6) | (((r + g + b) / 3.0) < 205)
    hair = ((g - b) > 40) & ((r - b) > 78) & (r > 130)
    mask = region & foreground & (~hair)
    if int(mask.sum()) < max(256, int(width * height * 0.015)):
        raise RuntimeError(f"{source.name}: cleanup mask is unexpectedly small ({int(mask.sum())} pixels)")

    soft_mask = np.asarray(
        Image.fromarray((mask * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.5))
    ).astype(np.float32) / 255.0
    soft_mask *= (foreground & (~hair)).astype(np.float32)

    skin_values = arr[_skin_sample(arr, yy)]
    skin = np.median(skin_values, axis=0) if len(skin_values) else np.array([252.0, 211.0, 182.0], dtype=np.float32)
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
    after = int((_garment_seed(output_arr) & (yy > 0.225) & (yy < 0.64)).sum())
    reduction = 1.0 - (after / max(before, 1))
    return {
        "source": str(source), "output": str(destination), "width": width, "height": height,
        "base_layer_pixels_before": before, "base_layer_pixels_after": after,
        "base_layer_reduction_diagnostic": reduction, "masked_pixels": int(mask.sum()),
        "region_pixels": int(region.sum()),
        "sampled_skin_rgb": [round(float(value), 2) for value in skin],
    }


def copy_precleaned(source: Path, destination: Path) -> dict[str, object]:
    image = Image.open(source).convert("RGB")
    destination.parent.mkdir(parents=True, exist_ok=True)
    image.save(destination, quality=95, subsampling=0)
    width, height = image.size
    return {
        "source": str(source), "output": str(destination), "width": width, "height": height,
        "precleaned_override": True,
    }


def main() -> int:
    args = parse_args()
    input_dir = Path(args.input_dir).resolve()
    output_dir = Path(args.output_dir).resolve()
    preclean_dir = Path(args.preclean_dir).resolve() if args.preclean_dir else None
    report_path = Path(args.report).resolve() if args.report else output_dir / "body-only-report.json"

    report: dict[str, object] = {"views": {}}
    for name in VIEW_NAMES:
        destination = output_dir / f"{name}.jpg"
        preclean = preclean_dir / f"{name}.jpg" if preclean_dir else None
        if preclean is not None and preclean.is_file():
            report["views"][name] = copy_precleaned(preclean, destination)
            continue
        source = input_dir / f"{name}.jpg"
        if not source.is_file():
            raise RuntimeError(f"missing Madeline reference view: {source}")
        report["views"][name] = prepare_view(source, destination)

    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(report_path)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
