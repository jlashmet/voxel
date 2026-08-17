#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import math
from pathlib import Path
import sys

import bpy
from mathutils import Vector


AXIS_INDEX = {"x": 0, "y": 1, "z": 2}


def parse_args() -> argparse.Namespace:
    argv = sys.argv
    if "--" not in argv:
        raise SystemExit("Expected Blender arguments after '--'")
    parser = argparse.ArgumentParser(description="Verify a rigid Character Factory asset FBX")
    parser.add_argument("--input", required=True)
    parser.add_argument("--length-relative-tolerance", type=float, default=0.03)
    parser.add_argument("--origin-fraction-tolerance", type=float, default=0.03)
    return parser.parse_args(argv[argv.index("--") + 1 :])


def load_contract(path: Path) -> dict[str, object] | None:
    contract_path = path.with_suffix(".rigid-contract.json")
    if not contract_path.is_file():
        return None
    payload = json.loads(contract_path.read_text(encoding="utf-8"))
    if not isinstance(payload, dict) or payload.get("schemaVersion") != 1:
        raise RuntimeError(f"invalid rigid preparation contract: {contract_path}")
    return payload


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
    extents = [abs(extent.x), abs(extent.y), abs(extent.z)]
    long_length = max(extents)
    if long_length <= 1e-6:
        raise RuntimeError(f"rigid asset bounds are degenerate: {tuple(extent)}")
    long_axis = ("x", "y", "z")[extents.index(long_length)]

    contract = load_contract(path)
    if contract is not None:
        expected_axis = contract.get("canonicalAxis")
        if expected_axis is not None:
            expected_axis = str(expected_axis)
            if long_axis != expected_axis:
                raise RuntimeError(
                    f"rigid canonical axis mismatch: expected {expected_axis}, "
                    f"got {long_axis} extent={tuple(extent)}"
                )

        target_length = contract.get("targetLength")
        if target_length is not None:
            target = float(target_length)
            tolerance = max(1e-6, abs(target) * args.length_relative_tolerance)
            if abs(long_length - target) > tolerance:
                raise RuntimeError(
                    f"rigid target length mismatch: expected {target:.5f} +/- {tolerance:.5f}, "
                    f"got {long_length:.5f}"
                )

        anchor = contract.get("anchorFraction")
        if anchor is not None:
            if not isinstance(anchor, list) or len(anchor) != 3:
                raise RuntimeError("rigid contract anchorFraction must contain 3 numbers")
            observed = [
                0.5 if extents[index] <= 1e-8 else (0.0 - lo[index]) / extents[index]
                for index in range(3)
            ]
            for index, expected_value in enumerate(anchor):
                expected = float(expected_value)
                if abs(observed[index] - expected) > args.origin_fraction_tolerance:
                    axis_name = ("x", "y", "z")[index]
                    raise RuntimeError(
                        f"rigid anchor mismatch on {axis_name}: expected fraction "
                        f"{expected:.5f} +/- {args.origin_fraction_tolerance:.5f}, "
                        f"got {observed[index]:.5f}"
                    )

    print(
        "CI_RIGID_ASSET_OK "
        f"meshes={len(meshes)} longAxis={long_axis} longLength={long_length:.4f} "
        f"extent={tuple(round(value, 4) for value in extent)} "
        f"contract={'yes' if contract is not None else 'no'}",
        flush=True,
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
