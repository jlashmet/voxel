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
            "Create a staff-only TripoSR conditioning image from the Sunlit Cleric "
            "CI crop. Ornate regions are copied from the source; the straight shaft "
            "is deterministically reconstructed across hand/robe occlusions using "
            "colors sampled from visible shaft pixels. No generative model is used."
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


def sample_shaft_colors(rgb: np.ndarray, hsv: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
    height, width, _ = rgb.shape
    hue, saturation, value = cv2.split(hsv)
    y_start = int(0.45 * height)
    y_end = int(0.72 * height)
    samples = []
    for y in range(y_start, y_end):
        center_x = shaft_center(width, y, int(0.235 * height), int(0.955 * height))
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
    highlight = np.clip(base.astype(np.int16) + np.array([34, 24, 15]), 0, 255).astype(np.uint8)
    return base, highlight


def build_staff_layer(rgb: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
    height, width, _ = rgb.shape
    hsv = cv2.cvtColor(rgb, cv2.COLOR_RGB2HSV)
    hue, saturation, value = cv2.split(hsv)
    layer = np.zeros_like(rgb)
    mask = np.zeros((height, width), dtype=np.uint8)

    # Preserve the highly saturated gold ornament at the top. This region is
    # largely unobstructed in the source artwork and carries the distinctive
    # sun/cross silhouette we want TripoSR to reconstruct.
    head_region = np.zeros_like(mask)
    head_region[
        int(0.005 * height) : int(0.34 * height),
        int(0.28 * width) : int(0.70 * width),
    ] = 1
    gold = (
        (hue >= 5)
        & (hue <= 35)
        & (saturation >= 105)
        & (value >= 45)
    ).astype(np.uint8)
    head = largest_component(gold & head_region)
    head = cv2.dilate(head, np.ones((3, 3), np.uint8), iterations=1)
    layer[head > 0] = rgb[head > 0]
    mask = np.maximum(mask, head)

    # A staff shaft is geometrically simple but the source image has a hand and
    # robe crossing it. Feeding those occluders to a single-object reconstructor
    # is harmful, so rebuild only this straight primitive using color measured
    # from visible shaft pixels. This preserves the reference's proportions while
    # presenting TripoSR with the isolated-object silhouette it was trained for.
    y_start = int(0.235 * height)
    y_end = int(0.955 * height)
    base, highlight = sample_shaft_colors(rgb, hsv)
    shaft_half_width = max(3, int(round(0.011 * width)))
    for y in range(y_start, y_end):
        center_x = shaft_center(width, y, y_start, y_end)
        lo = max(0, center_x - shaft_half_width)
        hi = min(width, center_x + shaft_half_width + 1)
        layer[y, lo:hi] = base
        mask[y, lo:hi] = 255
        # One-pixel highlight keeps the conditioning image from looking like a
        # flat cutout without changing the silhouette.
        highlight_x = min(width - 1, center_x - max(1, shaft_half_width // 2))
        layer[y, highlight_x] = highlight

    # Preserve the wider gold foot/finial around the known staff endpoint.
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
    # Keep only bottom components close to the predicted shaft centerline.
    center_bottom = shaft_center(width, y_end - 1, y_start, y_end)
    x_gate = np.zeros_like(mask)
    gate_half = max(10, int(round(0.045 * width)))
    x_gate[:, max(0, center_bottom - gate_half) : min(width, center_bottom + gate_half + 1)] = 1
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

    # Match TripoSR's official preprocessing convention: centered isolated
    # foreground on neutral gray occupying roughly 85% of a square image.
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
    image.save(output)
    print(output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
