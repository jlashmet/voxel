from __future__ import annotations

from pathlib import Path
import statistics

import bpy

from blender_multiview_texture import ImageInfo
from projection_components import select_meaningful_components


def load_rigid_subject_image(path: Path) -> ImageInfo:
    """Load a rigid reference without collapsing it to one connected component."""

    image = bpy.data.images.load(str(path.resolve()), check_existing=False)
    width = int(image.size[0])
    height = int(image.size[1])
    if width <= 1 or height <= 1:
        raise RuntimeError(f"rigid multiview texture source has invalid dimensions: {path}")

    pixels = image.pixels[:]
    sample_radius = max(2, min(width, height) // 64)
    background_levels: list[float] = []
    for y0 in (0, height - sample_radius):
        for x0 in (0, width - sample_radius):
            for y in range(y0, min(height, y0 + sample_radius)):
                row = y * width * 4
                for x in range(x0, min(width, x0 + sample_radius)):
                    index = row + x * 4
                    r, g, b, _a = pixels[index : index + 4]
                    background_levels.append((r + g + b) / 3.0)
    background = statistics.median(background_levels)

    raw_rows: list[tuple[tuple[int, int], ...]] = []
    for y in range(height):
        row = y * width * 4
        runs: list[tuple[int, int]] = []
        run_start: int | None = None
        for x in range(width):
            index = row + x * 4
            r, g, b, a = pixels[index : index + 4]
            mean = (r + g + b) / 3.0
            chroma = max(r, g, b) - min(r, g, b)
            foreground = a > 0.05 and (chroma > 0.05 or mean < background - 0.035)
            if foreground:
                if run_start is None:
                    run_start = x
            elif run_start is not None:
                runs.append((run_start, x - 1))
                run_start = None
        if run_start is not None:
            runs.append((run_start, width - 1))
        raw_rows.append(tuple(runs))

    raw_foreground = tuple(raw_rows)
    selection = select_meaningful_components(raw_foreground)
    foreground_rows = selection.rows
    raw_pixels = sum(end - start + 1 for row in raw_foreground for start, end in row)

    min_x = width
    max_x = -1
    min_y = height
    max_y = -1
    for y, runs in enumerate(foreground_rows):
        for start, end in runs:
            min_x = min(min_x, start)
            max_x = max(max_x, end)
            min_y = min(min_y, y)
            max_y = max(max_y, y)

    if max_x < min_x or max_y < min_y:
        raise RuntimeError(f"could not find rigid subject against neutral background: {path}")

    pad_x = max(2, int(round(width * 0.01)))
    pad_y = max(2, int(round(height * 0.01)))
    info = ImageInfo(
        image=image,
        x0=max(0, min_x - pad_x),
        y0=max(0, min_y - pad_y),
        x1=min(width - 1, max_x + pad_x),
        y1=min(height - 1, max_y + pad_y),
        source_width=width,
        source_height=height,
        foreground_runs=foreground_rows,
    )
    print(
        f"rigid multiview source crop: {path.name} {width}x{height} "
        f"bbox=({info.x0},{info.y0})-({info.x1},{info.y1}) bg={background:.4f} "
        f"components={selection.kept_component_count}/{selection.component_count} "
        f"componentMinPixels={selection.minimum_pixels} "
        f"maskPixels={selection.kept_pixels}/{raw_pixels} "
        f"removed={raw_pixels - selection.kept_pixels}",
        flush=True,
    )
    return info
