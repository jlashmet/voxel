#!/usr/bin/env python3
from __future__ import annotations

import argparse
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


def parse_args() -> argparse.Namespace:
    argv = sys.argv
    if "--" not in argv:
        raise SystemExit("Expected Blender arguments after '--'")
    argv = argv[argv.index("--") + 1 :]
    parser = argparse.ArgumentParser(
        description="Create a deterministic rigged T-pose mannequin and optional robe donor"
    )
    parser.add_argument("--canonical", required=True)
    parser.add_argument("--input", required=True)
    parser.add_argument(
        "--garment-input",
        help="Optional isolated robe render used by the generated-clothing smoke.",
    )
    return parser.parse_args(argv)


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)


def material(name: str, color: tuple[float, float, float, float], roughness: float = 0.62):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = color
    mat.use_nodes = True
    principled = mat.node_tree.nodes.get("Principled BSDF")
    if principled is not None:
        principled.inputs["Base Color"].default_value = color
        principled.inputs["Roughness"].default_value = roughness
        principled.inputs["Metallic"].default_value = 0.0
    return mat


def add_sphere(name: str, location, scale, mat) -> bpy.types.Object:
    bpy.ops.mesh.primitive_uv_sphere_add(
        segments=24,
        ring_count=16,
        location=location,
    )
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    obj.data.materials.append(mat)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return obj


def add_cylinder_x(name: str, x0: float, x1: float, z: float, radius: float, mat) -> bpy.types.Object:
    midpoint = (x0 + x1) * 0.5
    length = abs(x1 - x0)
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=24,
        radius=radius,
        depth=length,
        location=(midpoint, 0.0, z),
        rotation=(0.0, math.pi * 0.5, 0.0),
    )
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(mat)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return obj


def add_cylinder_z(name: str, x: float, z0: float, z1: float, radius: float, mat) -> bpy.types.Object:
    midpoint = (z0 + z1) * 0.5
    length = abs(z1 - z0)
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=24,
        radius=radius,
        depth=length,
        location=(x, 0.0, midpoint),
    )
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(mat)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return obj


def add_cone_z(
    name: str,
    z0: float,
    z1: float,
    bottom_radius: float,
    top_radius: float,
    depth_scale: float,
    mat,
) -> bpy.types.Object:
    midpoint = (z0 + z1) * 0.5
    length = abs(z1 - z0)
    bpy.ops.mesh.primitive_cone_add(
        vertices=32,
        radius1=bottom_radius,
        radius2=top_radius,
        depth=length,
        location=(0.0, 0.0, midpoint),
    )
    obj = bpy.context.object
    obj.name = name
    obj.scale.y = depth_scale
    obj.data.materials.append(mat)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return obj


def add_foot(name: str, x: float, mat) -> bpy.types.Object:
    bpy.ops.mesh.primitive_uv_sphere_add(
        segments=20,
        ring_count=12,
        location=(x, -0.085, 0.03),
    )
    obj = bpy.context.object
    obj.name = name
    obj.scale = (0.12, 0.22, 0.085)
    obj.data.materials.append(mat)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return obj


def create_armature() -> tuple[bpy.types.Object, dict[str, tuple[Vector, Vector]]]:
    armature_data = bpy.data.armatures.new("CanonicalArmatureData")
    armature = bpy.data.objects.new("Armature", armature_data)
    bpy.context.collection.objects.link(armature)
    bpy.context.view_layer.objects.active = armature
    armature.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")

    specs = {
        "Hips": ((0.0, 0.0, 0.94), (0.0, 0.0, 1.10), None),
        "Spine": ((0.0, 0.0, 1.10), (0.0, 0.0, 1.36), "Hips"),
        "Chest": ((0.0, 0.0, 1.36), (0.0, 0.0, 1.56), "Spine"),
        "Neck": ((0.0, 0.0, 1.56), (0.0, 0.0, 1.72), "Chest"),
        "Head": ((0.0, 0.0, 1.72), (0.0, 0.0, 1.98), "Neck"),
        "LeftUpperArm": ((0.0, 0.0, 1.53), (0.52, 0.0, 1.53), "Chest"),
        "LeftLowerArm": ((0.52, 0.0, 1.53), (0.98, 0.0, 1.53), "LeftUpperArm"),
        "LeftHand": ((0.98, 0.0, 1.53), (1.22, 0.0, 1.53), "LeftLowerArm"),
        "RightUpperArm": ((0.0, 0.0, 1.53), (-0.52, 0.0, 1.53), "Chest"),
        "RightLowerArm": ((-0.52, 0.0, 1.53), (-0.98, 0.0, 1.53), "RightUpperArm"),
        "RightHand": ((-0.98, 0.0, 1.53), (-1.22, 0.0, 1.53), "RightLowerArm"),
        "LeftUpperLeg": ((0.15, 0.0, 0.98), (0.15, 0.0, 0.55), "Hips"),
        "LeftLowerLeg": ((0.15, 0.0, 0.55), (0.15, 0.0, 0.12), "LeftUpperLeg"),
        "LeftFoot": ((0.15, 0.0, 0.12), (0.15, -0.18, 0.03), "LeftLowerLeg"),
        "RightUpperLeg": ((-0.15, 0.0, 0.98), (-0.15, 0.0, 0.55), "Hips"),
        "RightLowerLeg": ((-0.15, 0.0, 0.55), (-0.15, 0.0, 0.12), "RightUpperLeg"),
        "RightFoot": ((-0.15, 0.0, 0.12), (-0.15, -0.18, 0.03), "RightLowerLeg"),
    }

    edit_bones = {}
    segments: dict[str, tuple[Vector, Vector]] = {}
    for name, (head, tail, parent_name) in specs.items():
        bone = armature.data.edit_bones.new(name)
        bone.head = head
        bone.tail = tail
        if parent_name:
            bone.parent = edit_bones[parent_name]
        edit_bones[name] = bone
        segments[name] = (Vector(head), Vector(tail))

    bpy.ops.object.mode_set(mode="OBJECT")
    armature.show_in_front = True
    return armature, segments


def point_segment_distance(point: Vector, a: Vector, b: Vector) -> float:
    ab = b - a
    denom = ab.length_squared
    if denom <= 1e-10:
        return (point - a).length
    t = max(0.0, min(1.0, (point - a).dot(ab) / denom))
    return (point - (a + ab * t)).length


def join_weighted(
    parts: list[bpy.types.Object],
    name: str,
    armature: bpy.types.Object,
    segments,
) -> bpy.types.Object:
    bpy.ops.object.select_all(action="DESELECT")
    for obj in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    mesh = bpy.context.object
    mesh.name = name

    groups = {bone_name: mesh.vertex_groups.new(name=bone_name) for bone_name in segments}
    for vertex in mesh.data.vertices:
        point = vertex.co
        ranked = sorted(
            (point_segment_distance(point, a, b), bone_name)
            for bone_name, (a, b) in segments.items()
        )[:2]
        raw = [1.0 / max(distance, 0.025) ** 2 for distance, _ in ranked]
        total = sum(raw)
        for weight, (_, bone_name) in zip(raw, ranked):
            groups[bone_name].add([vertex.index], weight / total, "REPLACE")

    modifier = mesh.modifiers.new(name="CanonicalArmature", type="ARMATURE")
    modifier.object = armature
    mesh.parent = armature
    return mesh


def look_at(obj: bpy.types.Object, target: Vector) -> None:
    direction = target - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def ensure_render_scene() -> None:
    if bpy.context.scene.camera is not None:
        return

    world = bpy.data.worlds.new("FixtureWorld")
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg is not None:
        bg.inputs["Color"].default_value = (0.50, 0.50, 0.50, 1.0)
        bg.inputs["Strength"].default_value = 0.8
    bpy.context.scene.world = world

    target = Vector((0.0, 0.0, 1.02))
    camera_data = bpy.data.cameras.new("Camera")
    camera = bpy.data.objects.new("Camera", camera_data)
    bpy.context.collection.objects.link(camera)
    camera.location = Vector((0.0, -5.2, 1.02))
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 2.85
    look_at(camera, target)
    bpy.context.scene.camera = camera

    for name, location, energy, size in (
        ("Key", (2.2, -2.7, 3.4), 520.0, 2.0),
        ("Fill", (-2.0, -1.5, 2.2), 260.0, 2.8),
        ("Rim", (0.0, 2.5, 3.0), 300.0, 2.0),
    ):
        light_data = bpy.data.lights.new(name=name, type="AREA")
        light_data.energy = energy
        light_data.shape = "DISK"
        light_data.size = size
        light = bpy.data.objects.new(name, light_data)
        bpy.context.collection.objects.link(light)
        light.location = Vector(location)
        look_at(light, target)

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 768
    scene.render.resolution_y = 768
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGB"
    scene.render.film_transparent = False
    scene.view_settings.look = "AgX - Medium High Contrast"


def render_fixture(output: Path) -> None:
    ensure_render_scene()
    bpy.context.scene.render.filepath = str(output)
    bpy.ops.render.render(write_still=True)


def export_canonical(
    meshes: list[bpy.types.Object],
    armature: bpy.types.Object,
    output: Path,
) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    armature.select_set(True)
    for mesh in meshes:
        mesh.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.export_scene.gltf(
        filepath=str(output),
        export_format="GLB",
        use_selection=True,
        export_skins=True,
        export_animations=False,
    )


def main() -> int:
    args = parse_args()
    canonical = Path(args.canonical).resolve()
    input_image = Path(args.input).resolve()
    garment_input = Path(args.garment_input).resolve() if args.garment_input else None
    canonical.parent.mkdir(parents=True, exist_ok=True)
    input_image.parent.mkdir(parents=True, exist_ok=True)
    if garment_input:
        garment_input.parent.mkdir(parents=True, exist_ok=True)

    clear_scene()
    skin = material("MannequinSkin", (0.68, 0.48, 0.34, 1.0), 0.68)
    accent = material("RightSideMarker", (0.18, 0.32, 0.58, 1.0), 0.54)

    body_parts = [
        add_sphere("HipsMesh", (0.0, 0.0, 0.99), (0.29, 0.18, 0.22), skin),
        add_sphere("TorsoMesh", (0.0, 0.0, 1.34), (0.34, 0.19, 0.36), skin),
        add_sphere("ChestMesh", (0.0, 0.0, 1.50), (0.39, 0.20, 0.22), skin),
        add_cylinder_z("NeckMesh", 0.0, 1.58, 1.71, 0.085, skin),
        add_sphere("HeadMesh", (0.0, 0.0, 1.86), (0.17, 0.15, 0.20), skin),
        add_cylinder_x("LeftUpperArmMesh", 0.24, 0.58, 1.53, 0.105, skin),
        add_cylinder_x("LeftLowerArmMesh", 0.58, 1.02, 1.53, 0.085, skin),
        add_sphere("LeftHandMesh", (1.14, 0.0, 1.53), (0.12, 0.075, 0.075), skin),
        add_cylinder_x("RightUpperArmMesh", -0.58, -0.24, 1.53, 0.105, skin),
        add_cylinder_x("RightLowerArmMesh", -1.02, -0.58, 1.53, 0.085, skin),
        add_sphere("RightHandMesh", (-1.14, 0.0, 1.53), (0.145, 0.085, 0.085), accent),
        add_cylinder_z("LeftUpperLegMesh", 0.15, 0.56, 1.00, 0.13, skin),
        add_cylinder_z("LeftLowerLegMesh", 0.15, 0.13, 0.57, 0.105, skin),
        add_foot("LeftFootMesh", 0.15, skin),
        add_cylinder_z("RightUpperLegMesh", -0.15, 0.56, 1.00, 0.13, skin),
        add_cylinder_z("RightLowerLegMesh", -0.15, 0.13, 0.57, 0.105, skin),
        add_foot("RightFootMesh", -0.15, skin),
    ]

    armature, segments = create_armature()
    body = join_weighted(body_parts, "Body", armature, segments)
    render_fixture(input_image)

    garment = None
    if garment_input:
        robe = material("ClericRobe", (0.82, 0.72, 0.47, 1.0), 0.58)
        trim = material("ClericRobeTrim", (0.24, 0.39, 0.62, 1.0), 0.48)
        garment_parts = [
            add_sphere("RobeHood", (0.0, 0.0, 1.86), (0.205, 0.18, 0.235), robe),
            add_sphere("RobeChest", (0.0, 0.0, 1.43), (0.43, 0.225, 0.33), robe),
            add_sphere("RobeWaist", (0.0, 0.0, 1.16), (0.34, 0.21, 0.26), robe),
            add_cylinder_x("RobeLeftSleeve", 0.22, 1.10, 1.53, 0.13, robe),
            add_cylinder_x("RobeRightSleeve", -1.10, -0.22, 1.53, 0.13, robe),
            add_sphere("RobeLeftCuff", (1.10, 0.0, 1.53), (0.13, 0.10, 0.10), trim),
            add_sphere("RobeRightCuff", (-1.10, 0.0, 1.53), (0.13, 0.10, 0.10), robe),
            add_cone_z("RobeSkirt", 0.16, 1.18, 0.46, 0.31, 0.48, robe),
            add_sphere("RobeOrientationMark", (-0.29, -0.225, 1.47), (0.09, 0.045, 0.09), trim),
        ]
        garment = join_weighted(garment_parts, "GarmentDonor", armature, segments)
        body.hide_render = True
        render_fixture(garment_input)
        body.hide_render = False

    meshes = [body] + ([garment] if garment is not None else [])
    export_canonical(meshes, armature, canonical)

    if not canonical.is_file() or canonical.stat().st_size == 0:
        raise RuntimeError("failed to create canonical GLB")
    if not input_image.is_file() or input_image.stat().st_size == 0:
        raise RuntimeError("failed to render character input")
    if garment_input and (not garment_input.is_file() or garment_input.stat().st_size == 0):
        raise RuntimeError("failed to render garment input")

    print(f"canonical={canonical}")
    print(f"input={input_image}")
    if garment_input:
        print(f"garmentInput={garment_input}")
    print(
        f"bodyVertices={len(body.data.vertices)} "
        f"garmentVertices={len(garment.data.vertices) if garment else 0} "
        f"bones={len(armature.data.bones)}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
