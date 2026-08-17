#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import math
import statistics
import sys
from collections import defaultdict
from pathlib import Path

import bpy
from mathutils import Vector

SCRIPT_DIR = Path(__file__).resolve().parent
RUNTIME_DIR = SCRIPT_DIR.parent / "runtime"
if str(RUNTIME_DIR) not in sys.path:
    sys.path.insert(0, str(RUNTIME_DIR))

from blender_multiview_texture import (  # noqa: E402
    ImageInfo,
    _bounds,
    _load_subject_image,
    _projection_for_normal,
    _source_uv,
)


def parse_args() -> argparse.Namespace:
    argv = sys.argv
    if "--" not in argv:
        raise SystemExit("Expected Blender arguments after '--'")
    parser = argparse.ArgumentParser(
        description="Diagnose deterministic four-view projection before texturing a rigged character"
    )
    parser.add_argument("--input", required=True)
    parser.add_argument("--front", required=True)
    parser.add_argument("--back", required=True)
    parser.add_argument("--left", required=True)
    parser.add_argument("--right", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--sample-limit", type=int, default=80)
    parser.add_argument(
        "--large-snap-fraction",
        type=float,
        default=0.02,
        help="Flag projections that must move more than this fraction of the shorter image axis.",
    )
    parser.add_argument(
        "--outer-span-fraction",
        type=float,
        default=0.45,
        help="Flag side-view projection beyond this normalized half-span on world X.",
    )
    return parser.parse_args(argv[argv.index("--") + 1 :])


def _subject_uv(source: ImageInfo, u: float, v: float) -> tuple[float, float]:
    x0 = source.x0 / max(1, source.width - 1)
    x1 = source.x1 / max(1, source.width - 1)
    y0 = source.y0 / max(1, source.height - 1)
    y1 = source.y1 / max(1, source.height - 1)
    return x0 + u * (x1 - x0), y0 + v * (y1 - y0)


def _foreground_pixel_count(source: ImageInfo) -> int:
    return sum(end - start + 1 for row in source.foreground_runs for start, end in row)


def _nearest_foreground_distance(
    source: ImageInfo,
    u: float,
    v: float,
) -> tuple[float, int, int, bool]:
    width = source.width
    height = source.height
    target_x = int(round(max(0.0, min(1.0, u)) * (width - 1)))
    target_y = int(round(max(0.0, min(1.0, v)) * (height - 1)))
    inset = max(1, int(round(min(width, height) * 0.002)))

    best: tuple[float, int, int] | None = None
    for radius in range(max(width, height) + 1):
        rows = [target_y] if radius == 0 else [target_y - radius, target_y + radius]
        found_at_radius = False
        for y in rows:
            if y < 0 or y >= height:
                continue
            runs = source.foreground_runs[y]
            if not runs:
                continue
            for start, end in runs:
                safe_start = min(end, start + inset)
                safe_end = max(start, end - inset)
                if safe_start > safe_end:
                    safe_start, safe_end = start, end
                x = min(max(target_x, safe_start), safe_end)
                dx = x - target_x
                dy = y - target_y
                distance_sq = float(dx * dx + dy * dy)
                if best is None or distance_sq < best[0]:
                    best = (distance_sq, x, y)
                if y == target_y and safe_start <= target_x <= safe_end:
                    return 0.0, target_x, target_y, True
                found_at_radius = True
        if found_at_radius and best is not None:
            break

    if best is None:
        return math.inf, target_x, target_y, False
    distance_sq, x, y = best
    return math.sqrt(distance_sq), x, y, False


def _point_tuple(point: Vector) -> list[float]:
    return [round(float(value), 6) for value in point]


def main() -> int:
    args = parse_args()
    source_path = Path(args.input).resolve()
    output_path = Path(args.output).resolve()
    if not source_path.is_file():
        raise RuntimeError(f"projection diagnostic input does not exist: {source_path}")

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(source_path))
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if not meshes:
        raise RuntimeError(f"projection diagnostic found no meshes: {source_path}")

    sources = {
        "front": _load_subject_image(Path(args.front)),
        "back": _load_subject_image(Path(args.back)),
        "left": _load_subject_image(Path(args.left)),
        "right": _load_subject_image(Path(args.right)),
    }
    lo, hi = _bounds(meshes)
    center = (lo + hi) * 0.5
    x_half_span = max(abs(hi.x - lo.x) * 0.5, 1e-8)

    per_view: dict[str, dict[str, float | int]] = {
        name: {
            "polygons": 0,
            "loops": 0,
            "snappedLoops": 0,
            "largeSnapLoops": 0,
            "maxSnapPixels": 0.0,
            "snapPixelsTotal": 0.0,
            "outerSpanSidePolygons": 0,
        }
        for name in sources
    }
    snap_distances: list[float] = []
    large_samples: list[dict[str, object]] = []
    side_outer_samples: list[dict[str, object]] = []
    normal_transform_disagreements = 0
    polygon_count = 0
    loop_count = 0

    for mesh in meshes:
        world_matrix = mesh.matrix_world
        direct_normal_matrix = world_matrix.to_3x3()
        correct_normal_matrix = direct_normal_matrix.inverted().transposed()

        for polygon in mesh.data.polygons:
            polygon_count += 1
            direct_normal = direct_normal_matrix @ polygon.normal
            correct_normal = correct_normal_matrix @ polygon.normal
            if direct_normal.length_squared > 0.0:
                direct_normal.normalize()
            if correct_normal.length_squared > 0.0:
                correct_normal.normalize()
            view = _projection_for_normal(direct_normal)
            correct_view = _projection_for_normal(correct_normal)
            if view != correct_view:
                normal_transform_disagreements += 1

            source = sources[view]
            stats = per_view[view]
            stats["polygons"] = int(stats["polygons"]) + 1

            centroid = world_matrix @ polygon.center
            outer_span = abs(float(centroid.x - center.x)) / x_half_span
            if view in {"left", "right"} and outer_span >= args.outer_span_fraction:
                stats["outerSpanSidePolygons"] = int(stats["outerSpanSidePolygons"]) + 1
                if len(side_outer_samples) < max(0, args.sample_limit):
                    side_outer_samples.append(
                        {
                            "mesh": mesh.name,
                            "polygon": int(polygon.index),
                            "view": view,
                            "outerSpan": round(outer_span, 6),
                            "centroidWorld": _point_tuple(centroid),
                            "normalWorldDirect": _point_tuple(direct_normal),
                        }
                    )

            large_threshold = max(
                4.0,
                min(source.width, source.height) * max(0.0, args.large_snap_fraction),
            )
            for loop_index in polygon.loop_indices:
                loop_count += 1
                stats["loops"] = int(stats["loops"]) + 1
                vertex_index = mesh.data.loops[loop_index].vertex_index
                point = world_matrix @ mesh.data.vertices[vertex_index].co
                raw_u, raw_v = _source_uv(view, point, lo, hi)
                subject_u, subject_v = _subject_uv(source, raw_u, raw_v)
                distance, nearest_x, nearest_y, already_foreground = _nearest_foreground_distance(
                    source,
                    subject_u,
                    subject_v,
                )
                if not math.isfinite(distance):
                    distance = float(max(source.width, source.height))
                snap_distances.append(distance)
                stats["snapPixelsTotal"] = float(stats["snapPixelsTotal"]) + distance
                stats["maxSnapPixels"] = max(float(stats["maxSnapPixels"]), distance)
                if not already_foreground:
                    stats["snappedLoops"] = int(stats["snappedLoops"]) + 1
                if distance > large_threshold:
                    stats["largeSnapLoops"] = int(stats["largeSnapLoops"]) + 1
                    if len(large_samples) < max(0, args.sample_limit):
                        large_samples.append(
                            {
                                "mesh": mesh.name,
                                "polygon": int(polygon.index),
                                "vertex": int(vertex_index),
                                "view": view,
                                "world": _point_tuple(point),
                                "rawUv": [round(raw_u, 6), round(raw_v, 6)],
                                "subjectUv": [round(subject_u, 6), round(subject_v, 6)],
                                "nearestForegroundPixel": [nearest_x, nearest_y],
                                "snapPixels": round(distance, 4),
                                "largeThresholdPixels": round(large_threshold, 4),
                            }
                        )

    for name, stats in per_view.items():
        loops = max(1, int(stats["loops"]))
        stats["snappedLoopRatio"] = round(int(stats["snappedLoops"]) / loops, 6)
        stats["largeSnapLoopRatio"] = round(int(stats["largeSnapLoops"]) / loops, 6)
        stats["meanSnapPixels"] = round(float(stats["snapPixelsTotal"]) / loops, 6)
        stats["maxSnapPixels"] = round(float(stats["maxSnapPixels"]), 6)
        del stats["snapPixelsTotal"]

    sorted_distances = sorted(snap_distances)
    if sorted_distances:
        p95_index = min(len(sorted_distances) - 1, int(round((len(sorted_distances) - 1) * 0.95)))
        median_snap = statistics.median(sorted_distances)
        p95_snap = sorted_distances[p95_index]
        max_snap = sorted_distances[-1]
    else:
        median_snap = p95_snap = max_snap = 0.0

    report = {
        "input": str(source_path),
        "characterBounds": {
            "lo": _point_tuple(lo),
            "hi": _point_tuple(hi),
        },
        "sources": {
            name: {
                "width": source.width,
                "height": source.height,
                "bbox": [source.x0, source.y0, source.x1, source.y1],
                "foregroundPixels": _foreground_pixel_count(source),
            }
            for name, source in sources.items()
        },
        "projection": {
            "polygons": polygon_count,
            "loops": loop_count,
            "perView": per_view,
            "normalTransformDisagreements": normal_transform_disagreements,
            "snapPixelsMedian": round(float(median_snap), 6),
            "snapPixelsP95": round(float(p95_snap), 6),
            "snapPixelsMax": round(float(max_snap), 6),
            "largeSnapSamples": large_samples,
            "sideOuterSpanSamples": side_outer_samples,
        },
        "interpretation": {
            "largeSnap": (
                "A large snap means the current unbounded foreground fallback would move a "
                "projected loop a substantial distance to reach any subject pixel. Repeated "
                "large snaps can collapse unrelated geometry onto narrow image strips."
            ),
            "sideOuterSpan": (
                "Side projection drops world X. Outer-span T-pose geometry is therefore "
                "foreshortened or occluded in side references and is a high-risk source for arm smears."
            ),
        },
    }

    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(
        "CI_MULTIVIEW_PROJECTION_DIAGNOSTIC "
        f"polygons={polygon_count} loops={loop_count} "
        f"medianSnap={median_snap:.2f}px p95Snap={p95_snap:.2f}px maxSnap={max_snap:.2f}px "
        f"normalTransformDisagreements={normal_transform_disagreements} output={output_path}",
        flush=True,
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
