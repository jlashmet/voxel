#!/usr/bin/env python3
from __future__ import annotations

import argparse
import math
from pathlib import Path
import sys

import bpy
from mathutils import Vector


def parse_args() -> argparse.Namespace:
    argv = sys.argv
    if "--" not in argv:
        raise SystemExit("Expected Blender arguments after '--'")
    parser = argparse.ArgumentParser(description="Verify a rigid Character Factory asset FBX")
    parser.add_argument("--input", required=True)
    return parser.parse_args(argv[argv.index("--") + 1 :])


def main() -> int:
    args = parse_args()
    path = Path(args.input).resolve()
    if not path.is_file() or path.stat().st_size == 0:
        raise RuntimeError(f"missing rigid asset FBX: {path}")

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path))
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    if not meshes:
        raise RuntimeError("rigid asset contains no mesh")
    if armatures:
        raise RuntimeError(
            "rigid weapon/accessory unexpectedly contains armatures: "
            + ", ".join(obj.name for obj in armatures)
        )

    points: list[Vector] = []
    for mesh in meshes:
        matrix = mesh.matrix_world
        for corner in mesh.bound_box:
            point = matrix @ Vector(corner)
            if not all(math.isfinite(value) for value in point):
                raise RuntimeError(f"non-finite rigid asset bounds in {mesh.name}")
            points.append(point)
    if not points:
        raise RuntimeError("rigid asset has no finite bounds")

    lo = Vector(tuple(min(point[axis] for point in points) for axis in range(3)))
    hi = Vector(tuple(max(point[axis] for point in points) for axis in range(3)))
    extent = hi - lo
    if max(abs(extent.x), abs(extent.y), abs(extent.z)) <= 1e-6:
        raise RuntimeError(f"rigid asset bounds are degenerate: {tuple(extent)}")

    print(
        "CI_RIGID_ASSET_OK "
        f"meshes={len(meshes)} extent={tuple(round(value, 4) for value in extent)}",
        flush=True,
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
