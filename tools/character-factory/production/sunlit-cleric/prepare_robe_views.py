#!/usr/bin/env python3
from __future__ import annotations

import argparse
from collections import deque
from pathlib import Path

from PIL import Image, ImageFilter


VIEW_NAMES = ("front", "back", "left", "right")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Create isolated robe/cape T-pose views from the clean Sunlit Cleric "
            "turnaround without a generative image service."
        )
    )
    parser.add_argument("--views", required=True, help="Directory containing front/back/left/right.jpg")
    parser.add_argument("--output", required=True)
    return parser.parse_args()


def flood_background(image: Image.Image) -> Image.Image:
    rgb = image.convert("RGB")
    width, height = rgb.size
    pixels = rgb.load()
    background = bytearray(width * height)
    queue: deque[tuple[int, int]] = deque()

    def near_white(x: int, y: int) -> bool:
        r, g, b = pixels[x, y]
        return min(r, g, b) >= 238 and max(r, g, b) - min(r, g, b) <= 18

    def add(x: int, y: int) -> None:
        index = y * width + x
        if background[index] or not near_white(x, y):
            return
        background[index] = 1
        queue.append((x, y))

    for x in range(width):
        add(x, 0)
        add(x, height - 1)
    for y in range(height):
        add(0, y)
        add(width - 1, y)

    while queue:
        x, y = queue.popleft()
        if x > 0:
            add(x - 1, y)
        if x + 1 < width:
            add(x + 1, y)
        if y > 0:
            add(x, y - 1)
        if y + 1 < height:
            add(x, y + 1)

    mask = Image.new("L", (width, height), 0)
    data = mask.load()
    for y in range(height):
        row = y * width
        for x in range(width):
            if not background[row + x]:
                data[x, y] = 255

    # Recover pale fabric highlights enclosed by the silhouette and smooth tiny
    # JPEG pinholes while preserving the sleeves/cape outline.
    mask = mask.filter(ImageFilter.MaxFilter(5)).filter(ImageFilter.MinFilter(3))
    return mask


def mask_bounds(mask: Image.Image) -> tuple[int, int, int, int]:
    bbox = mask.getbbox()
    if bbox is None:
        raise RuntimeError("turnaround view contains no foreground subject")
    return bbox


def ellipse_cut(mask: Image.Image, cx: float, cy: float, rx: float, ry: float) -> None:
    pixels = mask.load()
    width, height = mask.size
    x0 = max(0, int(cx - rx))
    x1 = min(width - 1, int(cx + rx))
    y0 = max(0, int(cy - ry))
    y1 = min(height - 1, int(cy + ry))
    rx2 = max(rx * rx, 1.0)
    ry2 = max(ry * ry, 1.0)
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if ((x - cx) ** 2) / rx2 + ((y - cy) ** 2) / ry2 <= 1.0:
                pixels[x, y] = 0


def isolate_robe(image: Image.Image) -> Image.Image:
    rgb = image.convert("RGB")
    mask = flood_background(rgb)
    x0, y0, x1, y1 = mask_bounds(mask)
    subject_w = x1 - x0
    subject_h = y1 - y0

    # Remove head/hair/neck while preserving the high collar/shoulders. The
    # clean turnaround is a consistent T-pose, so normalized cuts remain stable
    # across all four views.
    head_cut = int(round(y0 + subject_h * 0.185))
    mask_pixels = mask.load()
    for y in range(max(0, y0), min(mask.height, head_cut)):
        for x in range(max(0, x0), min(mask.width, x1)):
            mask_pixels[x, y] = 0

    # Remove exposed hands at both T-pose endpoints. Oversizing the cut slightly
    # avoids baking fingers/wrists into the garment shell; sleeve cuffs remain.
    hand_y = y0 + subject_h * 0.305
    hand_rx = max(7.0, subject_w * 0.052)
    hand_ry = max(9.0, subject_h * 0.060)
    ellipse_cut(mask, x0 + subject_w * 0.018, hand_y, hand_rx, hand_ry)
    ellipse_cut(mask, x0 + subject_w * 0.982, hand_y, hand_rx, hand_ry)

    rgba = rgb.convert("RGBA")
    rgba.putalpha(mask)

    # Crop to the garment with breathing room, then center on a transparent
    # square. This is the same neutralized conditioning shape for every view.
    bbox = mask.getbbox()
    if bbox is None:
        raise RuntimeError("robe isolation removed the entire subject")
    bx0, by0, bx1, by1 = bbox
    pad = int(round(max(bx1 - bx0, by1 - by0) * 0.06))
    bx0 = max(0, bx0 - pad)
    by0 = max(0, by0 - pad)
    bx1 = min(rgba.width, bx1 + pad)
    by1 = min(rgba.height, by1 + pad)
    crop = rgba.crop((bx0, by0, bx1, by1))

    canvas = Image.new("RGBA", (512, 512), (255, 255, 255, 0))
    max_extent = int(round(512 * 0.90))
    scale = min(max_extent / crop.width, max_extent / crop.height)
    size = (
        max(1, int(round(crop.width * scale))),
        max(1, int(round(crop.height * scale))),
    )
    crop = crop.resize(size, Image.Resampling.LANCZOS)
    x = (512 - crop.width) // 2
    y = (512 - crop.height) // 2
    canvas.alpha_composite(crop, (x, y))
    return canvas


def main() -> int:
    args = parse_args()
    views = Path(args.views).resolve()
    output = Path(args.output).resolve()
    output.mkdir(parents=True, exist_ok=True)

    for name in VIEW_NAMES:
        source = views / f"{name}.jpg"
        if not source.is_file():
            raise FileNotFoundError(source)
        garment = isolate_robe(Image.open(source))
        destination = output / f"{name}.png"
        garment.save(destination)
        print(destination)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
