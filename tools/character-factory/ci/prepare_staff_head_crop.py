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
            "Enlarge the ornate staff head from an already-isolated TripoSR input. "
            "The input must use TripoSR's neutral-gray background; this script only "
            "crops and rescales existing pixels so the head occupies most of the model input."
        )
    )
    parser.add_argument("--input", required=True)
    parser.add_argument("--output", required=True)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    source = Path(args.input).resolve()
    output = Path(args.output).resolve()
    rgb = np.asarray(Image.open(source).convert("RGB"))
    height, width, _ = rgb.shape

    # The isolated full-staff fixture is vertical on a 128-gray canvas. Detect its
    # foreground rather than relying on coordinates from the original artwork.
    difference = np.max(np.abs(rgb.astype(np.int16) - 128), axis=2)
    foreground = difference > 8
    ys, xs = np.nonzero(foreground)
    if len(xs) == 0:
        raise RuntimeError("isolated staff input contains no foreground")

    y_min = int(ys.min())
    y_max = int(ys.max())
    x_min = int(xs.min())
    x_max = int(xs.max())

    # Find where the wide ornament transitions into the narrow shaft. A staff head
    # has a much larger row span than the shaft; require several consecutive narrow
    # rows so holes in the ornament cannot end the crop early.
    shaft_width_threshold = max(14, int(round((x_max - x_min + 1) * 0.20)))
    narrow_run = 0
    head_end = None
    for y in range(y_min, y_max + 1):
        row_x = np.nonzero(foreground[y])[0]
        span = int(row_x.max() - row_x.min() + 1) if len(row_x) else 0
        if span <= shaft_width_threshold and y > y_min + 40:
            narrow_run += 1
            if narrow_run >= 10:
                head_end = y - narrow_run + 1
                break
        else:
            narrow_run = 0

    if head_end is None:
        head_end = y_min + max(1, int(round((y_max - y_min + 1) * 0.30)))

    # Include a short neck of shaft under the ornament so TripoSR has a clear
    # attachment direction, but do not let the long shaft consume image resolution.
    head_end = min(y_max + 1, head_end + max(12, int(round(height * 0.025))))
    head_mask = foreground[y_min:head_end]
    local_ys, local_xs = np.nonzero(head_mask)
    if len(local_xs) == 0:
        raise RuntimeError("staff-head crop contains no foreground")

    crop_x0 = max(0, int(local_xs.min()) - 4)
    crop_x1 = min(width, int(local_xs.max()) + 5)
    crop_y0 = y_min
    crop_y1 = head_end
    cropped_rgb = rgb[crop_y0:crop_y1, crop_x0:crop_x1]
    cropped_mask = foreground[crop_y0:crop_y1, crop_x0:crop_x1]

    canvas_size = 768
    target_extent = int(round(canvas_size * 0.88))
    crop_h, crop_w = cropped_mask.shape
    scale = min(target_extent / crop_h, target_extent / crop_w)
    out_w = max(1, int(round(crop_w * scale)))
    out_h = max(1, int(round(crop_h * scale)))

    resized_rgb = cv2.resize(cropped_rgb, (out_w, out_h), interpolation=cv2.INTER_LANCZOS4)
    resized_mask = cv2.resize(
        cropped_mask.astype(np.uint8),
        (out_w, out_h),
        interpolation=cv2.INTER_NEAREST,
    ) > 0

    canvas = np.full((canvas_size, canvas_size, 3), 128, dtype=np.uint8)
    x = (canvas_size - out_w) // 2
    y = (canvas_size - out_h) // 2
    roi = canvas[y : y + out_h, x : x + out_w]
    roi[resized_mask] = resized_rgb[resized_mask]

    output.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(canvas, mode="RGB").save(output)
    print(output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
