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
            "Create a non-generative, staff-only TripoSR input from the Sunlit Cleric "
            "CI crop. The script only masks/copies source pixels and places them on "
            "TripoSR's expected neutral-gray square canvas."
        )
    )
    parser.add_argument("--input", required=True)
    parser.add_argument("--output", required=True)
    return parser.parse_args()


def largest_component(mask: np.ndarray) -> np.ndarray:
    count, labels, stats, _ = cv2.connectedComponentsWithStats(mask.astype(np.uint8), 8)
    if count <= 1:
        raise RuntimeError("staff-head threshold produced no connected component")
    index = 1 + int(np.argmax(stats[1:, cv2.CC_STAT_AREA]))
    return (labels == index).astype(np.uint8) * 255


def build_staff_mask(rgb: np.ndarray) -> np.ndarray:
    height, width, _ = rgb.shape
    hsv = cv2.cvtColor(rgb, cv2.COLOR_RGB2HSV)
    hue, saturation, value = cv2.split(hsv)
    mask = np.zeros((height, width), dtype=np.uint8)

    # The ornate head is a highly saturated gold connected shape. Restrict the
    # search to the upper-center region so similarly colored scenery cannot win.
    head_region = np.zeros_like(mask)
    head_region[
        int(0.005 * height) : int(0.34 * height),
        int(0.28 * width) : int(0.70 * width),
    ] = 1
    gold = (
        (hue >= 5)
        & (hue <= 35)
        & (saturation >= 115)
        & (value >= 45)
    ).astype(np.uint8)
    head = largest_component(gold & head_region)
    head = cv2.dilate(head, np.ones((3, 3), np.uint8), iterations=1)
    mask = np.maximum(mask, head)

    # The shaft is visible almost continuously but is occluded by the hand and
    # robe in places. Use a narrow corridor following the visible centerline.
    # This does not synthesize missing pixels; it only keeps source pixels in the
    # corridor so the model sees one long, thin object rather than the full scene.
    y_start = int(0.235 * height)
    y_end = int(0.955 * height)
    x_start = 0.508 * width
    x_end = 0.617 * width
    half_width = max(4, int(round(0.018 * width)))
    for y in range(y_start, y_end):
        t = (y - y_start) / max(1, y_end - y_start - 1)
        center_x = int(round(x_start + (x_end - x_start) * t))
        lo = max(0, center_x - half_width)
        hi = min(width, center_x + half_width + 1)
        mask[y, lo:hi] = 255

    # Preserve the wider gold finial at the bottom, but only inside a tight
    # corridor around the known staff endpoint.
    bottom_region = np.zeros_like(mask)
    bottom_region[
        int(0.885 * height) : height,
        int(0.54 * width) : int(0.67 * width),
    ] = 1
    bottom_gold = (
        (hue >= 4)
        & (hue <= 38)
        & (saturation >= 100)
        & (value >= 35)
    ).astype(np.uint8)
    bottom = (bottom_gold & bottom_region) * 255
    bottom = cv2.dilate(bottom, np.ones((3, 3), np.uint8), iterations=1)
    mask = np.maximum(mask, bottom)

    return mask


def fit_on_gray_canvas(rgb: np.ndarray, mask: np.ndarray) -> Image.Image:
    ys, xs = np.nonzero(mask)
    if len(xs) == 0:
        raise RuntimeError("staff mask is empty")

    x0, x1 = int(xs.min()), int(xs.max()) + 1
    y0, y1 = int(ys.min()), int(ys.max()) + 1
    cropped_rgb = rgb[y0:y1, x0:x1]
    cropped_mask = mask[y0:y1, x0:x1]

    # TripoSR conditions on a 512x512 image and its official preprocessing places
    # the foreground on neutral gray at about 85% of the canvas. Use a larger
    # square here; TripoSR will downsample while preserving this layout.
    canvas_size = 768
    target_extent = int(round(canvas_size * 0.85))
    crop_h, crop_w = cropped_mask.shape
    scale = min(target_extent / crop_h, target_extent / crop_w)
    out_w = max(1, int(round(crop_w * scale)))
    out_h = max(1, int(round(crop_h * scale)))

    resized_rgb = cv2.resize(cropped_rgb, (out_w, out_h), interpolation=cv2.INTER_LANCZOS4)
    resized_mask = cv2.resize(cropped_mask, (out_w, out_h), interpolation=cv2.INTER_NEAREST)

    canvas = np.full((canvas_size, canvas_size, 3), 128, dtype=np.uint8)
    x = (canvas_size - out_w) // 2
    y = (canvas_size - out_h) // 2
    roi = canvas[y : y + out_h, x : x + out_w]
    roi[resized_mask > 0] = resized_rgb[resized_mask > 0]
    return Image.fromarray(canvas, mode="RGB")


def main() -> int:
    args = parse_args()
    source = Path(args.input).resolve()
    output = Path(args.output).resolve()
    rgb = np.asarray(Image.open(source).convert("RGB"))
    mask = build_staff_mask(rgb)
    image = fit_on_gray_canvas(rgb, mask)
    output.parent.mkdir(parents=True, exist_ok=True)
    image.save(output)
    print(output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
