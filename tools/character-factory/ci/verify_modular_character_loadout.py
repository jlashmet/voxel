#!/usr/bin/env python3
from __future__ import annotations

import argparse
import math
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Vector


def parse_args() -> argparse.Namespace:
    argv = sys.argv
    if "--" not in argv:
        raise SystemExit("Expected Blender arguments after '--'")
    argv = argv[argv.index("--") + 1 :]
    parser = argparse.ArgumentParser(
        description=(
            "Verify that independently exported clothing and a rigid weapon can be "
            "mounted onto one generated character skeleton"
        )
    )
    parser.add_argument("--character", required=True)
    parser.add_argument("--clothing", required=True)
    parser.add_argument("--weapon", required=True)
    parser.add_argument("--socket", default="RightHand")
    parser.add_argument("--output", required=True)
    return parser.parse_args(argv)


def import_fbx(path: Path) -> list[bpy.types.Object]:
    if not path.is_file() or path.stat().st_size == 0:
        raise RuntimeError(f"missing FBX: {path}")
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(path))
    return [obj for obj in bpy.data.objects if obj not in before]


def one_armature(objects: list[bpy.types.Object], label: str) -> bpy.types.Object:
    armatures = [obj for obj in objects if obj.type == "ARMATURE"]
    if len(armatures) != 1:
        raise RuntimeError(f"expected one {label} armature, found {len(armatures)}")
    return armatures[0]


def meshes(objects: list[bpy.types.Object], label: str) -> list[bpy.types.Object]:
    result = [obj for obj in objects if obj.type == "MESH"]
    if not result:
        raise RuntimeError(f"{label} FBX contains no mesh objects")
    return result


def evaluated_positions(obj: bpy.types.Object) -> list[Vector]:
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = obj.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh()
    try:
        matrix = evaluated.matrix_world
        return [matrix @ vertex.co for vertex in mesh.vertices]
    finally:
        evaluated.to_mesh_clear()


def max_pose_delta(objects: list[bpy.types.Object], baseline: dict[str, list[Vector]]) -> float:
    maximum = 0.0
    for obj in objects:
        posed = evaluated_positions(obj)
        before = baseline[obj.name]
        if len(posed) != len(before):
            raise RuntimeError(f"evaluated vertex count changed for {obj.name}")
        for left, right in zip(before, posed):
            maximum = max(maximum, (right - left).length)
    return maximum


def scene_bounds(objects: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    points: list[Vector] = []
    for obj in objects:
        for corner in obj.bound_box:
            points.append(obj.matrix_world @ Vector(corner))
    if not points:
        raise RuntimeError("cannot compute scene bounds without renderable objects")
    lo = Vector(tuple(min(point[axis] for point in points) for axis in range(3)))
    hi = Vector(tuple(max(point[axis] for point in points) for axis in range(3)))
    return lo, hi


def look_at(obj: bpy.types.Object, target: Vector) -> None:
    direction = target - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def fallback_material() -> bpy.types.Material:
    material = bpy.data.materials.new("ModularLoadoutFallback")
    material.diffuse_color = (0.45, 0.48, 0.52, 1.0)
    material.use_nodes = True
    principled = material.node_tree.nodes.get("Principled BSDF")
    if principled is not None:
        principled.inputs["Base Color"].default_value = (0.45, 0.48, 0.52, 1.0)
        principled.inputs["Roughness"].default_value = 0.62
    return material


def render(objects: list[bpy.types.Object], output: Path) -> None:
    fallback = fallback_material()
    for obj in objects:
        if not obj.data.materials:
            obj.data.materials.append(fallback)

    lo, hi = scene_bounds(objects)
    center = (lo + hi) * 0.5
    radius = max((hi - lo)) * 0.5
    if radius <= 0.0:
        radius = 1.0

    world = bpy.data.worlds.new("ModularLoadoutWorld")
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    if background is not None:
        background.inputs["Color"].default_value = (0.035, 0.035, 0.035, 1.0)
        background.inputs["Strength"].default_value = 0.45
    bpy.context.scene.world = world

    camera_data = bpy.data.cameras.new("Camera")
    camera = bpy.data.objects.new("Camera", camera_data)
    bpy.context.collection.objects.link(camera)
    camera.location = center + Vector((radius * 2.5, -radius * 2.8, radius * 1.55))
    camera.data.lens = 58
    look_at(camera, center)
    bpy.context.scene.camera = camera

    def add_area(name: str, location: Vector, energy: float, size: float) -> None:
        data = bpy.data.lights.new(name=name, type="AREA")
        data.energy = energy
        data.shape = "DISK"
        data.size = size
        light = bpy.data.objects.new(name, data)
        bpy.context.collection.objects.link(light)
        light.location = location
        look_at(light, center)

    add_area("Key", center + Vector((radius * 2.0, -radius * 1.6, radius * 2.2)), 650, radius * 1.4)
    add_area("Fill", center + Vector((-radius * 1.6, -radius * 0.8, radius * 1.1)), 280, radius * 1.8)
    add_area("Rim", center + Vector((0.0, radius * 2.0, radius * 1.8)), 420, radius * 1.2)

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 768
    scene.render.resolution_y = 768
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = False
    scene.render.filepath = str(output)
    scene.view_settings.look = "AgX - Medium High Contrast"
    bpy.ops.render.render(write_still=True)


def rebind_clothing(
    character_armature: bpy.types.Object,
    clothing_armature: bpy.types.Object,
    clothing_meshes: list[bpy.types.Object],
) -> None:
    bone_names = {bone.name for bone in character_armature.data.bones}
    for garment in clothing_meshes:
        garment_groups = {group.name for group in garment.vertex_groups}
        unknown = sorted(garment_groups - bone_names)
        if unknown:
            raise RuntimeError(f"clothing contains groups absent from character skeleton: {unknown}")

        modifiers = [modifier for modifier in garment.modifiers if modifier.type == "ARMATURE"]
        if not modifiers:
            raise RuntimeError(f"clothing mesh {garment.name} has no armature modifier")
        for modifier in modifiers:
            modifier.object = character_armature
        garment.parent = character_armature

    bpy.data.objects.remove(clothing_armature, do_unlink=True)
    bpy.context.view_layer.update()


def mount_weapon(
    character_armature: bpy.types.Object,
    weapon_objects: list[bpy.types.Object],
    weapon_meshes: list[bpy.types.Object],
    socket: str,
) -> None:
    if character_armature.pose.bones.get(socket) is None:
        raise RuntimeError(f"character skeleton does not contain weapon socket bone {socket!r}")

    weapon_armatures = [obj for obj in weapon_objects if obj.type == "ARMATURE"]
    if weapon_armatures:
        raise RuntimeError(
            f"rigid weapon unexpectedly contains armatures: {[obj.name for obj in weapon_armatures]}"
        )

    for weapon in weapon_meshes:
        weapon.parent = character_armature
        weapon.parent_type = "BONE"
        weapon.parent_bone = socket
        weapon.matrix_parent_inverse = Matrix.Identity(4)
        weapon.location = (0.0, 0.0, 0.0)
        weapon.rotation_mode = "XYZ"
        weapon.rotation_euler = (0.0, 0.0, 0.0)
        weapon.scale = (1.0, 1.0, 1.0)
    bpy.context.view_layer.update()


def main() -> int:
    args = parse_args()
    character_path = Path(args.character).resolve()
    clothing_path = Path(args.clothing).resolve()
    weapon_path = Path(args.weapon).resolve()
    output = Path(args.output).resolve()
    output.parent.mkdir(parents=True, exist_ok=True)

    bpy.ops.wm.read_factory_settings(use_empty=True)

    character_objects = import_fbx(character_path)
    character_armature = one_armature(character_objects, "character")
    character_meshes = meshes(character_objects, "character")

    clothing_objects = import_fbx(clothing_path)
    clothing_armature = one_armature(clothing_objects, "clothing")
    clothing_meshes = meshes(clothing_objects, "clothing")
    rebind_clothing(character_armature, clothing_armature, clothing_meshes)

    weapon_objects = import_fbx(weapon_path)
    weapon_meshes = meshes(weapon_objects, "weapon")
    mount_weapon(character_armature, weapon_objects, weapon_meshes, args.socket)

    character_baseline = {
        obj.name: evaluated_positions(obj) for obj in character_meshes
    }
    clothing_baseline = {
        obj.name: evaluated_positions(obj) for obj in clothing_meshes
    }
    weapon_baseline = {
        obj.name: evaluated_positions(obj) for obj in weapon_meshes
    }

    upper_arm = character_armature.pose.bones.get("RightUpperArm")
    lower_arm = character_armature.pose.bones.get("RightLowerArm")
    if upper_arm is None or lower_arm is None:
        raise RuntimeError("character skeleton is missing the right arm pose chain")
    upper_arm.rotation_mode = "XYZ"
    lower_arm.rotation_mode = "XYZ"
    upper_arm.rotation_euler[1] = math.radians(28.0)
    lower_arm.rotation_euler[0] = math.radians(-18.0)
    bpy.context.view_layer.update()

    character_delta = max_pose_delta(character_meshes, character_baseline)
    clothing_delta = max_pose_delta(clothing_meshes, clothing_baseline)
    weapon_delta = max_pose_delta(weapon_meshes, weapon_baseline)
    if character_delta < 0.01:
        raise RuntimeError(f"character did not deform after modular pose: {character_delta:.6f}")
    if clothing_delta < 0.01:
        raise RuntimeError(f"clothing did not follow character skeleton: {clothing_delta:.6f}")
    if weapon_delta < 0.01:
        raise RuntimeError(f"weapon did not follow socket bone {args.socket}: {weapon_delta:.6f}")

    render([*character_meshes, *clothing_meshes, *weapon_meshes], output)
    if not output.is_file() or output.stat().st_size == 0:
        raise RuntimeError("failed to render modular character + clothing + weapon composition")

    print(
        "CI_MODULAR_CHARACTER_LOADOUT_OK "
        f"characterMeshes={len(character_meshes)} clothingMeshes={len(clothing_meshes)} "
        f"weaponMeshes={len(weapon_meshes)} socket={args.socket} "
        f"characterPoseDelta={character_delta:.4f} clothingPoseDelta={clothing_delta:.4f} "
        f"weaponPoseDelta={weapon_delta:.4f}",
        flush=True,
    )
    print(output, flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
