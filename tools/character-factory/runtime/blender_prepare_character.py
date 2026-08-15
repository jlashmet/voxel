#!/usr/bin/env python3
from __future__ import annotations

import argparse
from pathlib import Path
import sys

import bpy
from mathutils import Vector

SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

from blender_common import (
    choose_object,
    clear_scene,
    export_fbx,
    generated_meshes,
    import_glb,
    transfer_weights,
)
from character_alignment import infer_axis_alignment


ALIGN_TO_CANONICAL_BLEND = 0.78


def parse_args() -> argparse.Namespace:
    argv = sys.argv
    if "--" not in argv:
        raise SystemExit("Expected Blender arguments after '--'")
    argv = argv[argv.index("--") + 1 :]

    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True)
    parser.add_argument("--canonical", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--body-object")
    parser.add_argument("--armature-object")
    parser.add_argument("--max-transfer-distance", type=float, default=0.25)
    parser.add_argument(
        "--no-auto-align",
        action="store_true",
        help="Skip global axis/scale/center alignment before weight transfer.",
    )
    return parser.parse_args(argv)


def world_points(meshes: list[bpy.types.Object]) -> list[Vector]:
    points: list[Vector] = []
    for mesh in meshes:
        matrix = mesh.matrix_world
        points.extend(matrix @ vertex.co for vertex in mesh.data.vertices)
    if not points:
        raise RuntimeError("character alignment found no generated vertices")
    return points


def stats(points: list[Vector]) -> tuple[Vector, Vector, Vector]:
    lo = Vector(tuple(min(point[axis] for point in points) for axis in range(3)))
    hi = Vector(tuple(max(point[axis] for point in points) for axis in range(3)))
    mean = Vector(
        tuple(sum(point[axis] for point in points) / len(points) for axis in range(3))
    )
    return lo, hi, mean


def mean_fractions(lo: Vector, hi: Vector, mean: Vector) -> tuple[float, float, float]:
    extent = hi - lo
    return tuple((mean[axis] - lo[axis]) / extent[axis] for axis in range(3))


def align_generated_to_canonical(
    generated: list[bpy.types.Object],
    donor_body: bpy.types.Object,
) -> None:
    generated_points = world_points(generated)
    canonical_points = world_points([donor_body])
    g_lo, g_hi, g_mean = stats(generated_points)
    c_lo, c_hi, c_mean = stats(canonical_points)
    g_extent = g_hi - g_lo
    c_extent = c_hi - c_lo
    g_center = (g_lo + g_hi) * 0.5
    c_center = (c_lo + c_hi) * 0.5

    alignment = infer_axis_alignment(
        tuple(g_extent),
        tuple(c_extent),
        mean_fractions(g_lo, g_hi, g_mean),
        mean_fractions(c_lo, c_hi, c_mean),
    )
    mapping = alignment.mapping
    flips = alignment.flips
    uniform_scale = alignment.uniform_scale

    for mesh in generated:
        inverse = mesh.matrix_world.inverted()
        matrix = mesh.matrix_world.copy()
        for vertex in mesh.data.vertices:
            source_world = matrix @ vertex.co
            target_world = Vector((0.0, 0.0, 0.0))
            for target_axis, source_axis in enumerate(mapping):
                normalized = (
                    source_world[source_axis] - g_lo[source_axis]
                ) / g_extent[source_axis]
                centered = source_world[source_axis] - g_center[source_axis]
                if flips[target_axis]:
                    normalized = 1.0 - normalized
                    centered = -centered

                exact = c_lo[target_axis] + normalized * c_extent[target_axis]
                uniform = c_center[target_axis] + centered * uniform_scale
                target_world[target_axis] = (
                    uniform * (1.0 - ALIGN_TO_CANONICAL_BLEND)
                    + exact * ALIGN_TO_CANONICAL_BLEND
                )
            vertex.co = inverse @ target_world
        mesh.data.update()

    aligned_points = world_points(generated)
    a_lo, a_hi, _ = stats(aligned_points)
    a_extent = a_hi - a_lo
    relative_error = max(
        abs(a_extent[axis] - c_extent[axis]) / c_extent[axis] for axis in range(3)
    )
    print(
        "character auto-align: "
        f"mapping={mapping} flips={tuple(int(value) for value in flips)} "
        f"uniformScale={uniform_scale:.4f} boundsError={relative_error:.4f} "
        f"score={alignment.score:.4f}",
        flush=True,
    )


def main() -> int:
    args = parse_args()
    output = Path(args.output).resolve()
    if output.suffix.lower() != ".fbx":
        raise RuntimeError("character output must use .fbx for Unity import")

    clear_scene()
    canonical_objects = import_glb(Path(args.canonical).resolve())
    armature = choose_object(
        canonical_objects, "ARMATURE", args.armature_object, "canonical armature"
    )
    donor_body = choose_object(
        canonical_objects, "MESH", args.body_object, "canonical body"
    )

    generated = generated_meshes(
        import_glb(Path(args.input).resolve()),
        "character",
    )
    if not args.no_auto_align:
        align_generated_to_canonical(generated, donor_body)

    for mesh in generated:
        transfer_weights(
            mesh,
            donor_body,
            armature,
            max_distance=args.max_transfer_distance,
        )

    donor_body.hide_render = True
    export_fbx(output, [armature, *generated])
    print(output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
