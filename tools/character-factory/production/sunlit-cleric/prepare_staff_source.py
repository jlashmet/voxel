#!/usr/bin/env python3
from __future__ import annotations

import argparse
from pathlib import Path

import cv2
import numpy as np
from PIL import Image


SHAFT_Y_START = 0.305
SHAFT_Y_END = 0.955
SHAFT_X_START = 0.526
SHAFT_X_END = 0.617


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Create the Sunlit Cleric staff-only conditioning image from its source "
            "artwork. This is intentionally asset-specific: the ornament envelope, "
            "warm-metal segmentation, and occluded shaft reconstruction are tuned to "
            "this reference image rather than presented as a generic factory stage."
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
    return int(round((SHAFT_X_START + (SHAFT_X_END - SHAFT_X_START) * t) * width))


def build_head_mask(rgb: np.ndarray, hsv: np.ndarray) -> np.ndarray:
    height, width, _ = rgb.shape
    hue, saturation, value = cv2.split(hsv)
    red = rgb[:, :, 0].astype(np.float32)
    green = rgb[:, :, 1].astype(np.float32)
    blue = rgb[:, :, 2].astype(np.float32)

    envelope = np.zeros((height, width), dtype=np.uint8)
    points = np.array(
        [
            [int(0.46 * width), int(0.015 * height)],
            [int(0.55 * width), int(0.015 * height)],
            [int(0.66 * width), int(0.070 * height)],
            [int(0.73 * width), int(0.130 * height)],
            [int(0.73 * width), int(0.210 * height)],
            [int(0.64 * width), int(0.270 * height)],
            [int(0.59 * width), int(0.320 * height)],
            [int(0.41 * width), int(0.320 * height)],
            [int(0.35 * width), int(0.270 * height)],
            [int(0.27 * width), int(0.210 * height)],
            [int(0.27 * width), int(0.120 * height)],
            [int(0.35 * width), int(0.070 * height)],
        ],
        dtype=np.int32,
    )
    cv2.fillPoly(envelope, [points], 1)

    warm_metal = (
        (hue >= 5)
        & (hue <= 35)
        & (saturation >= 90)
        & (value >= 40)
        & (red >= green * 1.14)
        & (green >= blue * 1.05)
    ).astype(np.uint8)
    head = largest_component(warm_metal & envelope)
    return cv2.dilate(head, np.ones((2, 2), np.uint8), iterations=1)


def sample_shaft_colors(rgb: np.ndarray, hsv: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
    height, width, _ = rgb.shape
    hue, saturation, value = cv2.split(hsv)
    full_y_start = int(SHAFT_Y_START * height)
    full_y_end = int(SHAFT_Y_END * height)
    y_start = int(0.45 * height)
    y_end = int(0.72 * height)
    samples = []
    for y in range(y_start, y_end):
        center_x = shaft_center(width, y, full_y_start, full_y_end)
        lo = max(0, center_x - 4)
        hi = min(width, center_x + 5)
        keep = (
            (hue[y, lo:hi] >= 3)
            & (hue[y, lo:hi] <= 35)
            & (saturation[y, lo:hi] >= 70)
            & (value[y, lo:hi] >= 25)
            & (value[y, lo:hi] <= 175)
        )
        if np.any(keep):
            samples.append(rgb[y, lo:hi][keep])
    if samples:
        pixels = np.concatenate(samples, axis=0)
        base = np.median(pixels, axis=0).astype(np.uint8)
    else:
        base = np.array([88, 52, 28], dtype=np.uint8)
    highlight = np.clip(
        base.astype(np.int16) + np.array([34, 24, 15]),
        0,
        255,
    ).astype(np.uint8)
    return base, highlight


def build_staff_layer(rgb: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
    height, width, _ = rgb.shape
    hsv = cv2.cvtColor(rgb, cv2.COLOR_RGB2HSV)
    hue, saturation, value = cv2.split(hsv)
    layer = np.zeros_like(rgb)
    mask = np.zeros((height, width), dtype=np.uint8)

    head = build_head_mask(rgb, hsv)
    layer[head > 0] = rgb[head > 0]
    mask = np.maximum(mask, head)

    y_start = int(SHAFT_Y_START * height)
    y_end = int(SHAFT_Y_END * height)
    base, highlight = sample_shaft_colors(rgb, hsv)
    shaft_half_width = max(3, int(round(0.011 * width)))
    for y in range(y_start, y_end):
        center_x = shaft_center(width, y, y_start, y_end)
        lo = max(0, center_x - shaft_half_width)
        hi = min(width, center_x + shaft_half_width + 1)
        row_mask = mask[y, lo:hi]
        row_layer = layer[y, lo:hi]
        empty = row_mask == 0
        row_layer[empty] = base
        row_mask[empty] = 255

        highlight_x = min(width - 1, center_x - max(1, shaft_half_width // 2))
        if head[y, highlight_x] == 0:
            layer[y, highlight_x] = highlight
            mask[y, highlight_x] = 255

    bottom_region = np.zeros_like(mask)
    bottom_region[
        int(0.865 * height) : height,
        int(0.54 * width) : int(0.68 * width),
    ] = 1
    bottom_gold = (
        (hue >= 4)
        & (hue <= 38)
        & (saturation >= 90)
        & (value >= 35)
    ).astype(np.uint8)
    bottom = (bottom_gold & bottom_region) * 255
    center_bottom = shaft_center(width, y_end - 1, y_start, y_end)
    x_gate = np.zeros_like(mask)
    gate_half = max(10, int(round(0.045 * width)))
    x_gate[
        :,
        max(0, center_bottom - gate_half) : min(width, center_bottom + gate_half + 1),
    ] = 1
    bottom = (bottom * x_gate).astype(np.uint8)
    bottom = cv2.dilate(bottom, np.ones((3, 3), np.uint8), iterations=1)
    layer[bottom > 0] = rgb[bottom > 0]
    mask = np.maximum(mask, bottom)

    return layer, mask


def fit_on_gray_canvas(layer: np.ndarray, mask: np.ndarray) -> Image.Image:
    ys, xs = np.nonzero(mask)
    if len(xs) == 0:
        raise RuntimeError("staff mask is empty")

    x0, x1 = int(xs.min()), int(xs.max()) + 1
    y0, y1 = int(ys.min()), int(ys.max()) + 1
    cropped_rgb = layer[y0:y1, x0:x1]
    cropped_mask = mask[y0:y1, x0:x1]

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
    layer, mask = build_staff_layer(rgb)
    image = fit_on_gray_canvas(layer, mask)
    output.parent.mkdir(parents=True, exist_ok=True)
    image.save(output, format="PNG", optimize=False, compress_level=9)
    print(output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
