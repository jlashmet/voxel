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


def shaft_center(width: int, y: int, y_start: int, y_end: int) -> int:
    t = (y - y_start) / max(1, y_end - y_start - 1)
    return int(round((0.508 + (0.617 - 0.508) * t) * width))


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

    # Follow the actual shaft corridor, but retain only source pixels that look
    # like the brown/gold staff. This removes most of the hand, robe and scenery
    # without inventing replacement pixels where the shaft is occluded.
    y_start = int(0.235 * height)
    y_end = int(0.955 * height)
    half_width = max(4, int(round(0.018 * width)))
    corridor = np.zeros_like(mask)
    for y in range(y_start, y_end):
        center_x = shaft_center(width, y, y_start, y_end)
        lo = max(0, center_x - half_width)
        hi = min(width, center_x + half_width + 1)
        corridor[y, lo:hi] = 1

    shaft_color = (
        (hue >= 3)
        & (hue <= 38)
        & (saturation >= 65)
        & (value >= 25)
        & (value <= 205)
    ).astype(np.uint8)
    shaft = (corridor & shaft_color) * 255
    shaft = cv2.morphologyEx(
        shaft,
        cv2.MORPH_CLOSE,
        np.ones((3, 3), np.uint8),
        iterations=1,
    )
    shaft = cv2.dilate(shaft, np.ones((3, 3), np.uint8), iterations=1)
    mask = np.maximum(mask, shaft)

    # Preserve the wider gold finial only when gold pixels are close to the staff
    # centerline. This avoids the grassy/robe rectangle that contaminated the
    # previous diagnostic input.
    bottom_start = int(0.88 * height)
    bottom_gold = (
        (hue >= 4)
        & (hue <= 38)
        & (saturation >= 100)
        & (value >= 35)
    )
    bottom = np.zeros_like(mask)
    bottom_half_width = max(8, int(round(0.035 * width)))
    for y in range(bottom_start, height):
        center_x = shaft_center(width, min(y, y_end - 1), y_start, y_end)
        lo = max(0, center_x - bottom_half_width)
        hi = min(width, center_x + bottom_half_width + 1)
        row = bottom_gold[y, lo:hi]
        bottom[y, lo:hi][row] = 255
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

    # Match TripoSR's official preprocessing convention: isolated foreground on
    # neutral gray, centered and occupying about 85% of a square image.
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
