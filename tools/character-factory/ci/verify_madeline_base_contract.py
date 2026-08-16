#!/usr/bin/env python3
from __future__ import annotations

import argparse
import math
import re
import sys
from pathlib import Path

import bpy
from mathutils import Vector


BANNED_TOKENS = {
    "robe",
    "cape",
    "boot",
    "staff",
    "belt",
    "armor",
    "armour",
    "jewel",
    "necklace",
    "bracelet",
    "pouch",
    "book",
    "weapon",
    "shield",
}

EXPECTED_BONES = {
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


def parse_args() -> argparse.Namespace:
    argv = sys.argv
    if "--" not in argv:
        raise SystemExit("Expected Blender arguments after '--'")
    parser = argparse.ArgumentParser(
        description="Verify Madeline remains a reusable body+hair base rather than a baked Cleric loadout"
    )
    parser.add_argument("--input", required=True)
    return parser.parse_args(argv[argv.index("--") + 1 :])


def normalized_tokens(name: str) -> set[str]:
    return {
        token
        for token in re.split(r"[^a-z0-9]+", name.lower())
        if token
    }


def scene_bounds(meshes: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    points: list[Vector] = []
    for obj in meshes:
        for corner in obj.bound_box:
            point = obj.matrix_world @ Vector(corner)
            if not all(math.isfinite(value) for value in point):
                raise RuntimeError(f"non-finite character bounds in {obj.name}")
            points.append(point)
    if not points:
        raise RuntimeError("Madeline base contains no mesh bounds")
    lo = Vector(tuple(min(point[axis] for point in points) for axis in range(3)))
    hi = Vector(tuple(max(point[axis] for point in points) for axis in range(3)))
    return lo, hi


def main() -> int:
    args = parse_args()
    path = Path(args.input).resolve()
    if not path.is_file() or path.stat().st_size == 0:
        raise RuntimeError(f"missing Madeline FBX: {path}")

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path))

    armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if len(armatures) != 1:
        raise RuntimeError(f"expected one Madeline armature, found {len(armatures)}")
    if not meshes:
        raise RuntimeError("Madeline base contains no meshes")

    armature = armatures[0]
    bone_names = {bone.name for bone in armature.data.bones}
    missing = sorted(EXPECTED_BONES - bone_names)
    if missing:
        raise RuntimeError(f"Madeline armature missing expected bones: {missing}")

    offenders: list[str] = []
    for obj in bpy.context.scene.objects:
        tokens = normalized_tokens(obj.name)
        banned = sorted(tokens & BANNED_TOKENS)
        if banned:
            offenders.append(f"object {obj.name!r}: {banned}")
        if obj.type == "MESH":
            for material in obj.data.materials:
                if material is None:
                    continue
                mtokens = normalized_tokens(material.name)
                mbanned = sorted(mtokens & BANNED_TOKENS)
                if mbanned:
                    offenders.append(f"material {material.name!r}: {mbanned}")
    if offenders:
        raise RuntimeError(
            "Madeline base contains names associated with baked clothing/equipment: "
            + "; ".join(offenders)
        )

    unskinned: list[str] = []
    wrong_armature: list[str] = []
    empty_groups: list[str] = []
    for mesh in meshes:
        modifiers = [modifier for modifier in mesh.modifiers if modifier.type == "ARMATURE"]
        if not modifiers:
            unskinned.append(mesh.name)
            continue
        if not any(modifier.object == armature for modifier in modifiers):
            wrong_armature.append(mesh.name)
        if not mesh.vertex_groups:
            empty_groups.append(mesh.name)

    if unskinned:
        raise RuntimeError(f"Madeline base contains unskinned mesh objects: {unskinned}")
    if wrong_armature:
        raise RuntimeError(f"Madeline meshes are bound to the wrong armature: {wrong_armature}")
    if empty_groups:
        raise RuntimeError(f"Madeline meshes contain no skin-weight groups: {empty_groups}")

    rigid_meshes = [
        mesh.name
        for mesh in meshes
        if not any(modifier.type == "ARMATURE" for modifier in mesh.modifiers)
    ]
    if rigid_meshes:
        raise RuntimeError(f"unexpected rigid mesh objects in Madeline base: {rigid_meshes}")

    lo, hi = scene_bounds(meshes)
    extent = hi - lo
    dimensions = sorted((abs(extent.x), abs(extent.y), abs(extent.z)), reverse=True)
    if dimensions[0] <= 0.0 or dimensions[2] <= 0.0:
        raise RuntimeError(f"degenerate Madeline bounds: lo={tuple(lo)} hi={tuple(hi)}")
    # Broad enough for stylized T-pose proportions, but rejects the flattened
    # card/sheet failure mode seen in earlier single-view reconstruction attempts.
    if dimensions[0] / dimensions[2] > 12.0:
        raise RuntimeError(
            f"Madeline bounds are implausibly thin/flat: extent={tuple(round(v, 4) for v in extent)}"
        )

    print(
        "CI_MADELINE_BASE_CONTRACT_OK "
        f"meshes={len(meshes)} bones={len(bone_names)} "
        f"extent={tuple(round(v, 4) for v in extent)}",
        flush=True,
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
