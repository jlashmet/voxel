#!/usr/bin/env python3
from __future__ import annotations

import argparse
from pathlib import Path
import sys

SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

import bpy
from mathutils import Matrix, Vector

from blender_common import (
    apply_mesh_transforms,
    clear_scene,
    export_fbx,
    generated_meshes,
    import_glb,
)


AXES = {
    "x": Vector((1.0, 0.0, 0.0)),
    "y": Vector((0.0, 1.0, 0.0)),
    "z": Vector((0.0, 0.0, 1.0)),
}


def parse_args() -> argparse.Namespace:
    argv = sys.argv
    if "--" not in argv:
        raise SystemExit("Expected Blender arguments after '--'")
    argv = argv[argv.index("--") + 1 :]

    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--part-kind", choices=("weapon", "accessory"), required=True)
    parser.add_argument("--canonical-axis", choices=sorted(AXES))
    parser.add_argument("--target-length", type=float)
    parser.add_argument(
        "--anchor-fraction",
        nargs=3,
        type=float,
        metavar=("X", "Y", "Z"),
        help=(
            "Place this normalized bounds point at the local origin. For a weapon "
            "this is normally the grip; for an accessory it is the mount point."
        ),
    )
    return parser.parse_args(argv)


def bounds(meshes: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    points: list[Vector] = []
    for mesh in meshes:
        matrix = mesh.matrix_world
        points.extend(matrix @ Vector(corner) for corner in mesh.bound_box)
    if not points:
        raise RuntimeError("rigid asset has no bounds")
    lo = Vector(tuple(min(point[index] for point in points) for index in range(3)))
    hi = Vector(tuple(max(point[index] for point in points) for index in range(3)))
    return lo, hi


def longest_axis(meshes: list[bpy.types.Object]) -> tuple[str, float]:
    lo, hi = bounds(meshes)
    extent = hi - lo
    values = {"x": abs(extent.x), "y": abs(extent.y), "z": abs(extent.z)}
    axis = max(values, key=values.get)
    return axis, values[axis]


def rotate_long_axis(meshes: list[bpy.types.Object], target_axis: str) -> None:
    source_axis, _length = longest_axis(meshes)
    if source_axis == target_axis:
        return
    rotation = AXES[source_axis].rotation_difference(AXES[target_axis]).to_matrix().to_4x4()
    for mesh in meshes:
        mesh.matrix_world = rotation @ mesh.matrix_world
    apply_mesh_transforms(meshes)


def normalize_length(meshes: list[bpy.types.Object], target_length: float) -> None:
    _axis, current = longest_axis(meshes)
    if current <= 1e-8:
        raise RuntimeError("cannot normalize degenerate rigid asset length")
    scale = target_length / current
    transform = Matrix.Scale(scale, 4)
    for mesh in meshes:
        mesh.matrix_world = transform @ mesh.matrix_world
    apply_mesh_transforms(meshes)


def anchor_at_fraction(
    meshes: list[bpy.types.Object],
    fraction: tuple[float, float, float],
) -> None:
    lo, hi = bounds(meshes)
    extent = hi - lo
    anchor = Vector(
        (
            lo.x + extent.x * fraction[0],
            lo.y + extent.y * fraction[1],
            lo.z + extent.z * fraction[2],
        )
    )
    for mesh in meshes:
        matrix = mesh.matrix_world.copy()
        matrix.translation -= anchor
        mesh.matrix_world = matrix

        bpy.ops.object.select_all(action="DESELECT")
        mesh.select_set(True)
        bpy.context.view_layer.objects.active = mesh
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        mesh.select_set(False)


def main() -> int:
    args = parse_args()
    output = Path(args.output).resolve()
    if output.suffix.lower() != ".fbx":
        raise RuntimeError(f"{args.part_kind} output must use .fbx for Unity import")
    if args.target_length is not None and args.target_length <= 0.0:
        raise RuntimeError("--target-length must be > 0")
    if args.anchor_fraction is not None and any(
        value < 0.0 or value > 1.0 for value in args.anchor_fraction
    ):
        raise RuntimeError("--anchor-fraction values must be between 0 and 1")

    clear_scene()
    meshes = generated_meshes(
        import_glb(Path(args.input).resolve()),
        args.part_kind,
    )
    apply_mesh_transforms(meshes)

    source_axis, source_length = longest_axis(meshes)
    if args.canonical_axis:
        rotate_long_axis(meshes, args.canonical_axis)
    if args.target_length is not None:
        normalize_length(meshes, args.target_length)
    if args.anchor_fraction is not None:
        anchor_at_fraction(meshes, tuple(args.anchor_fraction))

    final_axis, final_length = longest_axis(meshes)
    lo, hi = bounds(meshes)
    print(
        "rigid canonicalization: "
        f"kind={args.part_kind} sourceAxis={source_axis} sourceLength={source_length:.5f} "
        f"finalAxis={final_axis} finalLength={final_length:.5f} "
        f"bounds=({tuple(round(v, 5) for v in lo)}, {tuple(round(v, 5) for v in hi)}) "
        f"anchorFraction={args.anchor_fraction}",
        flush=True,
    )

    export_fbx(output, meshes)
    print(output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
