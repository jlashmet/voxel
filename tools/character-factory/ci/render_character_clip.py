#!/usr/bin/env python3
from __future__ import annotations

import argparse
from pathlib import Path
import sys

import bpy
from mathutils import Vector


def parse_args() -> argparse.Namespace:
    argv = sys.argv
    if "--" not in argv:
        raise SystemExit("Expected Blender arguments after '--'")
    argv = argv[argv.index("--") + 1 :]
    parser = argparse.ArgumentParser(
        description="Render one frame from an embedded Character Factory animation clip"
    )
    parser.add_argument("--input", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--clip", required=True)
    parser.add_argument("--frame", type=float)
    return parser.parse_args(argv)


def normalized_action_name(name: str) -> str:
    return name.rsplit("|", 1)[-1].rsplit("::", 1)[-1]


def scene_bounds(meshes: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    points: list[Vector] = []
    for obj in meshes:
        for corner in obj.bound_box:
            points.append(obj.matrix_world @ Vector(corner))
    if not points:
        raise RuntimeError("No mesh bounds to render")
    lo = Vector(
        (
            min(point.x for point in points),
            min(point.y for point in points),
            min(point.z for point in points),
        )
    )
    hi = Vector(
        (
            max(point.x for point in points),
            max(point.y for point in points),
            max(point.z for point in points),
        )
    )
    return lo, hi


def look_at(obj: bpy.types.Object, target: Vector) -> None:
    direction = target - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def add_area(name: str, location: Vector, target: Vector, energy: float, size: float) -> None:
    data = bpy.data.lights.new(name=name, type="AREA")
    data.energy = energy
    data.shape = "DISK"
    data.size = size
    light = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(light)
    light.location = location
    look_at(light, target)


def choose_action(name: str) -> bpy.types.Action:
    exact = bpy.data.actions.get(name)
    if exact is not None:
        return exact
    for action in bpy.data.actions:
        if normalized_action_name(action.name) == name:
            return action
    discovered = ", ".join(sorted(action.name for action in bpy.data.actions))
    raise RuntimeError(f"Animation clip '{name}' not found. Discovered: {discovered}")


def main() -> int:
    args = parse_args()
    source = Path(args.input).resolve()
    output = Path(args.output).resolve()

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(source))

    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    if not meshes or not armatures:
        raise RuntimeError("Character preview requires an FBX with mesh and armature")

    armature = armatures[0]
    action = choose_action(args.clip)
    armature.animation_data_create()
    armature.animation_data.action = action

    start, end = action.frame_range
    frame = args.frame if args.frame is not None else (start + end) * 0.5
    frame = max(start, min(end, frame))
    bpy.context.scene.frame_set(int(round(frame)))
    bpy.context.view_layer.update()

    lo, hi = scene_bounds(meshes)
    center = (lo + hi) * 0.5
    size = hi - lo
    radius = max(size.x, size.y, size.z) * 0.5
    if radius <= 0.0:
        radius = 1.0

    world = bpy.data.worlds.new("CharacterPreviewWorld")
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    if background is not None:
        background.inputs["Color"].default_value = (0.055, 0.055, 0.065, 1.0)
        background.inputs["Strength"].default_value = 0.22
    bpy.context.scene.world = world

    camera_data = bpy.data.cameras.new("CharacterPreviewCamera")
    camera = bpy.data.objects.new("CharacterPreviewCamera", camera_data)
    bpy.context.collection.objects.link(camera)
    # Nearly front-on, slightly elevated, so silhouette and feet remain readable.
    camera.location = center + Vector((radius * 0.10, -radius * 3.35, radius * 0.42))
    camera.data.lens = 58
    look_at(camera, center + Vector((0.0, 0.0, radius * 0.04)))
    bpy.context.scene.camera = camera

    add_area(
        "Key",
        center + Vector((-radius * 1.65, -radius * 1.8, radius * 2.1)),
        center,
        260,
        radius * 1.5,
    )
    add_area(
        "Fill",
        center + Vector((radius * 1.5, -radius * 1.0, radius * 1.0)),
        center,
        95,
        radius * 1.8,
    )
    add_area(
        "Rim",
        center + Vector((radius * 0.4, radius * 2.0, radius * 1.75)),
        center,
        155,
        radius * 1.3,
    )

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 768
    scene.render.resolution_y = 768
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.filepath = str(output)
    scene.render.film_transparent = False
    scene.view_settings.look = "AgX - Medium High Contrast"

    output.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.render.render(write_still=True)
    print(
        f"rendered clip={args.clip} frame={frame:.0f} action={action.name} -> {output}",
        flush=True,
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
