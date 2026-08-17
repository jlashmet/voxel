#!/usr/bin/env python3
from __future__ import annotations

import argparse
from collections import deque
from pathlib import Path

from PIL import Image, ImageFilter


VIEW_NAMES = ("front", "back", "left", "right")
SUPPORTED_EXTENSIONS = (".png", ".jpg", ".jpeg")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Extract garment-only front/back/left/right references from a clean T-pose "
            "turnaround. Background, head/neck, and exposed hands are removed using "
            "normalized geometric rules so the operation can be reused across outfits."
        )
    )
    parser.add_argument("--views", required=True, help="Directory containing canonical view images")
    parser.add_argument("--output", required=True)
    parser.add_argument("--canvas-size", type=int, default=512)
    parser.add_argument("--target-occupancy", type=float, default=0.90)
    parser.add_argument("--padding-fraction", type=float, default=0.06)
    parser.add_argument("--head-cut-fraction", type=float, default=0.185)
    parser.add_argument("--hand-y-fraction", type=float, default=0.305)
    parser.add_argument("--hand-rx-fraction", type=float, default=0.052)
    parser.add_argument("--hand-ry-fraction", type=float, default=0.060)
    parser.add_argument("--background-min", type=int, default=238)
    parser.add_argument("--background-max-chroma", type=int, default=18)
    return parser.parse_args()


def resolve_view(directory: Path, name: str) -> Path:
    matches = [directory / f"{name}{extension}" for extension in SUPPORTED_EXTENSIONS]
    matches = [path for path in matches if path.is_file()]
    if not matches:
        raise FileNotFoundError(
            f"missing T-pose garment source view {name!r} in {directory}"
        )
    if len(matches) > 1:
        raise RuntimeError(
            f"ambiguous T-pose garment source view {name!r}: "
            + ", ".join(str(path) for path in matches)
        )
    return matches[0]


def flood_background(
    image: Image.Image,
    *,
    background_min: int,
    background_max_chroma: int,
) -> Image.Image:
    rgb = image.convert("RGB")
    width, height = rgb.size
    pixels = rgb.load()
    background = bytearray(width * height)
    queue: deque[tuple[int, int]] = deque()

    def near_background(x: int, y: int) -> bool:
        r, g, b = pixels[x, y]
        return (
            min(r, g, b) >= background_min
            and max(r, g, b) - min(r, g, b) <= background_max_chroma
        )

    def add(x: int, y: int) -> None:
        index = y * width + x
        if background[index] or not near_background(x, y):
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
    # compression pinholes without changing the broad sleeve/cape outline.
    return mask.filter(ImageFilter.MaxFilter(5)).filter(ImageFilter.MinFilter(3))


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


def isolate_garment(
    image: Image.Image,
    *,
    canvas_size: int,
    target_occupancy: float,
    padding_fraction: float,
    head_cut_fraction: float,
    hand_y_fraction: float,
    hand_rx_fraction: float,
    hand_ry_fraction: float,
    background_min: int,
    background_max_chroma: int,
) -> Image.Image:
    if canvas_size < 64:
        raise ValueError("canvas-size must be >= 64")
    for name, value in (
        ("target-occupancy", target_occupancy),
        ("padding-fraction", padding_fraction),
        ("head-cut-fraction", head_cut_fraction),
        ("hand-y-fraction", hand_y_fraction),
        ("hand-rx-fraction", hand_rx_fraction),
        ("hand-ry-fraction", hand_ry_fraction),
    ):
        if value < 0.0 or value > 1.0:
            raise ValueError(f"{name} must be between 0 and 1")

    rgb = image.convert("RGB")
    mask = flood_background(
        rgb,
        background_min=background_min,
        background_max_chroma=background_max_chroma,
    )
    bbox = mask.getbbox()
    if bbox is None:
        raise RuntimeError("turnaround view contains no foreground subject")
    x0, y0, x1, y1 = bbox
    subject_w = x1 - x0
    subject_h = y1 - y0

    head_cut = int(round(y0 + subject_h * head_cut_fraction))
    mask_pixels = mask.load()
    for y in range(max(0, y0), min(mask.height, head_cut)):
        for x in range(max(0, x0), min(mask.width, x1)):
            mask_pixels[x, y] = 0

    hand_y = y0 + subject_h * hand_y_fraction
    hand_rx = max(7.0, subject_w * hand_rx_fraction)
    hand_ry = max(9.0, subject_h * hand_ry_fraction)
    ellipse_cut(mask, x0 + subject_w * 0.018, hand_y, hand_rx, hand_ry)
    ellipse_cut(mask, x0 + subject_w * 0.982, hand_y, hand_rx, hand_ry)

    rgba = rgb.convert("RGBA")
    rgba.putalpha(mask)
    bbox = mask.getbbox()
    if bbox is None:
        raise RuntimeError("garment isolation removed the entire subject")
    bx0, by0, bx1, by1 = bbox
    pad = int(round(max(bx1 - bx0, by1 - by0) * padding_fraction))
    bx0 = max(0, bx0 - pad)
    by0 = max(0, by0 - pad)
    bx1 = min(rgba.width, bx1 + pad)
    by1 = min(rgba.height, by1 + pad)
    crop = rgba.crop((bx0, by0, bx1, by1))

    canvas = Image.new("RGBA", (canvas_size, canvas_size), (255, 255, 255, 0))
    max_extent = int(round(canvas_size * target_occupancy))
    scale = min(max_extent / crop.width, max_extent / crop.height)
    size = (
        max(1, int(round(crop.width * scale))),
        max(1, int(round(crop.height * scale))),
    )
    crop = crop.resize(size, Image.Resampling.LANCZOS)
    x = (canvas_size - crop.width) // 2
    y = (canvas_size - crop.height) // 2
    canvas.alpha_composite(crop, (x, y))
    return canvas


def main() -> int:
    args = parse_args()
    views = Path(args.views).resolve()
    output = Path(args.output).resolve()
    output.mkdir(parents=True, exist_ok=True)

    for name in VIEW_NAMES:
        source = resolve_view(views, name)
        garment = isolate_garment(
            Image.open(source),
            canvas_size=args.canvas_size,
            target_occupancy=args.target_occupancy,
            padding_fraction=args.padding_fraction,
            head_cut_fraction=args.head_cut_fraction,
            hand_y_fraction=args.hand_y_fraction,
            hand_rx_fraction=args.hand_rx_fraction,
            hand_ry_fraction=args.hand_ry_fraction,
            background_min=args.background_min,
            background_max_chroma=args.background_max_chroma,
        )
        destination = output / f"{name}.png"
        garment.save(destination, format="PNG", optimize=False, compress_level=9)
        print(destination)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
