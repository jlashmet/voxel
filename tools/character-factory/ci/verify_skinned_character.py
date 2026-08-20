#!/usr/bin/env python3
from __future__ import annotations

import argparse
import math
import sys
from pathlib import Path

import bpy


def parse_args() -> argparse.Namespace:
    argv = sys.argv
    if "--" not in argv:
        raise SystemExit("Expected Blender arguments after '--'")
    argv = argv[argv.index("--") + 1 :]
    parser = argparse.ArgumentParser(description="Verify an exported skinned character/clothing FBX")
    parser.add_argument("--input", required=True)
    parser.add_argument("--min-weighted-fraction", type=float, default=0.99)
    return parser.parse_args(argv)


def evaluated_positions(obj: bpy.types.Object) -> list:
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = obj.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh()
    try:
        matrix = evaluated.matrix_world
        return [matrix @ vertex.co for vertex in mesh.vertices]
    finally:
        evaluated.to_mesh_clear()


def weighted_fraction(mesh: bpy.types.Object) -> tuple[int, int, float, list[int]]:
    total = len(mesh.data.vertices)
    if total == 0:
        return 0, 0, 0.0, []
    unweighted: list[int] = []
    weighted = 0
    for vertex in mesh.data.vertices:
        if any(group.weight > 1e-6 for group in vertex.groups):
            weighted += 1
        elif len(unweighted) < 12:
            unweighted.append(vertex.index)
    return weighted, total, weighted / total, unweighted


def main() -> int:
    args = parse_args()
    if args.min_weighted_fraction <= 0.0 or args.min_weighted_fraction > 1.0:
        raise RuntimeError("--min-weighted-fraction must be in (0, 1]")

    path = Path(args.input).resolve()
    if not path.is_file() or path.stat().st_size == 0:
        raise RuntimeError(f"missing character/clothing FBX: {path}")

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path))

    armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if len(armatures) != 1:
        raise RuntimeError(f"expected one armature, found {len(armatures)}")
    if not meshes:
        raise RuntimeError("skinned FBX contains no generated mesh")

    armature = armatures[0]
    expected = {
        "Hips",
        "Spine",
        "Chest",
        "Head",
        "LeftUpperArm",
        "LeftLowerArm",
        "LeftHand",
        "RightUpperArm",
        "RightLowerArm",
        "RightHand",
        "LeftUpperLeg",
        "LeftLowerLeg",
        "RightUpperLeg",
        "RightLowerLeg",
    }
    bones = {bone.name for bone in armature.data.bones}
    missing = sorted(expected - bones)
    if missing:
        raise RuntimeError(f"armature missing expected bones: {missing}")

    skinned = []
    all_groups = set()
    minimum_coverage = 1.0
    coverage_summary: list[str] = []
    for mesh in meshes:
        modifiers = [modifier for modifier in mesh.modifiers if modifier.type == "ARMATURE"]
        if not modifiers:
            continue
        if not any(modifier.object == armature for modifier in modifiers):
            continue
        groups = {group.name for group in mesh.vertex_groups}
        all_groups.update(groups)
        skinned.append(mesh)

        weighted, total, fraction, unweighted = weighted_fraction(mesh)
        minimum_coverage = min(minimum_coverage, fraction)
        coverage_summary.append(f"{mesh.name}:{weighted}/{total}={fraction:.4f}")
        if fraction < args.min_weighted_fraction:
            raise RuntimeError(
                f"skin-weight coverage below {args.min_weighted_fraction:.3f} for "
                f"{mesh.name}: weighted={weighted}/{total} ({fraction:.4f}); "
                f"sampleUnweightedVertices={unweighted}"
            )

    if not skinned:
        raise RuntimeError("no generated mesh is bound to the exported armature")
    missing_groups = sorted(expected - all_groups)
    if missing_groups:
        raise RuntimeError(f"skinned meshes missing transferred vertex groups: {missing_groups}")

    target = armature.pose.bones.get("RightUpperArm")
    if target is None:
        raise RuntimeError("RightUpperArm pose bone missing")

    baseline = {mesh.name: evaluated_positions(mesh) for mesh in skinned}
    target.rotation_mode = "XYZ"
    target.rotation_euler[1] = math.radians(28.0)
    bpy.context.view_layer.update()

    max_delta = 0.0
    for mesh in skinned:
        posed = evaluated_positions(mesh)
        before = baseline[mesh.name]
        if len(posed) != len(before):
            raise RuntimeError("evaluated vertex count changed while posing")
        for left, right in zip(before, posed):
            max_delta = max(max_delta, (right - left).length)

    if max_delta < 0.01:
        raise RuntimeError(
            f"skinning did not deform under pose; max displacement={max_delta:.6f}"
        )

    print(
        "CI_CHARACTER_SKINNING_OK "
        f"meshes={len(skinned)} bones={len(bones)} groups={len(all_groups)} "
        f"minWeightedFraction={minimum_coverage:.4f} "
        f"maxPoseDelta={max_delta:.4f} coverage={';'.join(coverage_summary)}",
        flush=True,
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
