#!/usr/bin/env python3
from __future__ import annotations

import argparse
from pathlib import Path
import sys

from mathutils import Vector
import bpy

SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

from blender_common import clear_scene, export_fbx, generated_meshes, import_glb


AXES = {
    "x": 0,
    "y": 1,
    "z": 2,
}


def parse_args() -> argparse.Namespace:
    argv = sys.argv
    if "--" not in argv:
        raise SystemExit("Expected Blender arguments after '--'")
    argv = argv[argv.index("--") + 1 :]

    parser = argparse.ArgumentParser(
        description=(
            "Assemble a long staff from a reconstructed ornament/head plus a simple "
            "procedural shaft. This keeps learned reconstruction focused on the part "
            "that actually needs it instead of spending the image budget on a cylinder."
        )
    )
    parser.add_argument("--input-head", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--total-length", type=float, default=1.8)
    parser.add_argument("--head-length", type=float, default=0.38)
    parser.add_argument("--shaft-radius", type=float, default=0.024)
    parser.add_argument("--axis", choices=("auto", "x", "y", "z"), default="auto")
    parser.add_argument("--attachment-side", choices=("min", "max"), default="min")
    parser.add_argument("--overlap", type=float, default=0.025)
    return parser.parse_args(argv)


def world_vertices(meshes: list[bpy.types.Object]) -> list[Vector]:
    points: list[Vector] = []
    for obj in meshes:
        matrix = obj.matrix_world
        points.extend(matrix @ vertex.co for vertex in obj.data.vertices)
    if not points:
        raise RuntimeError("reconstructed staff head contains no vertices")
    return points


def bounds(points: list[Vector]) -> tuple[Vector, Vector]:
    lo = Vector((
        min(p.x for p in points),
        min(p.y for p in points),
        min(p.z for p in points),
    ))
    hi = Vector((
        max(p.x for p in points),
        max(p.y for p in points),
        max(p.z for p in points),
    ))
    return lo, hi


def choose_axis(points: list[Vector], requested: str) -> int:
    if requested != "auto":
        return AXES[requested]
    lo, hi = bounds(points)
    extents = hi - lo
    return max(range(3), key=lambda index: extents[index])


def apply_uniform_scale(meshes: list[bpy.types.Object], scale: float) -> None:
    for mesh in meshes:
        mesh.scale *= scale
        bpy.context.view_layer.objects.active = mesh
        mesh.select_set(True)
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        mesh.select_set(False)


def attachment_center(
    points: list[Vector],
    axis: int,
    side: str,
) -> Vector:
    lo, hi = bounds(points)
    span = hi[axis] - lo[axis]
    band = max(span * 0.09, 1e-5)
    if side == "min":
        candidates = [p for p in points if p[axis] <= lo[axis] + band]
        axis_value = lo[axis]
    else:
        candidates = [p for p in points if p[axis] >= hi[axis] - band]
        axis_value = hi[axis]
    if not candidates:
        candidates = points

    center = Vector((0.0, 0.0, 0.0))
    center[axis] = axis_value
    for other in range(3):
        if other == axis:
            continue
        values = sorted(p[other] for p in candidates)
        center[other] = values[len(values) // 2]
    return center


def add_shaft(
    attachment: Vector,
    axis: int,
    side: str,
    total_length: float,
    head_bounds: tuple[Vector, Vector],
    shaft_radius: float,
    overlap: float,
) -> bpy.types.Object:
    lo, hi = head_bounds
    if side == "min":
        far_end = hi[axis] - total_length
        near_end = attachment[axis] + overlap
    else:
        far_end = lo[axis] + total_length
        near_end = attachment[axis] - overlap

    shaft_length = abs(near_end - far_end)
    if shaft_length <= shaft_radius * 2.0:
        raise RuntimeError("requested staff dimensions leave no usable shaft length")

    center = attachment.copy()
    center[axis] = (near_end + far_end) * 0.5

    bpy.ops.mesh.primitive_cylinder_add(
        vertices=32,
        radius=shaft_radius,
        depth=shaft_length,
        end_fill_type="NGON",
        location=center,
    )
    shaft = bpy.context.active_object
    shaft.name = "ProceduralStaffShaft"

    # Blender cylinders are created along local Z. Rotate to the inferred ornament axis.
    if axis == 0:
        shaft.rotation_euler[1] = 1.5707963267948966
    elif axis == 1:
        shaft.rotation_euler[0] = 1.5707963267948966
    bpy.context.view_layer.objects.active = shaft
    shaft.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    shaft.select_set(False)
    return shaft


def main() -> int:
    args = parse_args()
    if args.total_length <= 0 or args.head_length <= 0 or args.shaft_radius <= 0:
        raise RuntimeError("staff dimensions must be positive")
    if args.total_length <= args.head_length:
        raise RuntimeError("total staff length must exceed reconstructed head length")

    output = Path(args.output).resolve()
    if output.suffix.lower() != ".fbx":
        raise RuntimeError("assembled staff output must use .fbx for Unity import")

    clear_scene()
    meshes = generated_meshes(import_glb(Path(args.input_head).resolve()), "staff head")
    for index, mesh in enumerate(meshes):
        mesh.name = "ReconstructedStaffHead" if index == 0 else f"ReconstructedStaffHead_{index}"

    points = world_vertices(meshes)
    axis = choose_axis(points, args.axis)
    lo, hi = bounds(points)
    current_head_length = hi[axis] - lo[axis]
    if current_head_length <= 0:
        raise RuntimeError("reconstructed staff head has zero extent")

    apply_uniform_scale(meshes, args.head_length / current_head_length)
    points = world_vertices(meshes)
    head_bounds = bounds(points)
    attachment = attachment_center(points, axis, args.attachment_side)
    shaft = add_shaft(
        attachment=attachment,
        axis=axis,
        side=args.attachment_side,
        total_length=args.total_length,
        head_bounds=head_bounds,
        shaft_radius=args.shaft_radius,
        overlap=args.overlap,
    )

    export_fbx(output, [*meshes, shaft])
    axis_name = "xyz"[axis]
    print(
        f"assembled staff: axis={axis_name} side={args.attachment_side} "
        f"length={args.total_length:.3f}m head={args.head_length:.3f}m "
        f"radius={args.shaft_radius:.3f}m"
    )
    print(output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
