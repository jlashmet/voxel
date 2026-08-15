#!/usr/bin/env python3
"""
Blender background-mode postprocessor for a wearable mesh.

The raw generated garment is expected to already be aligned to the canonical body.
This pass transfers canonical body weights to the garment and exports the garment
with the canonical armature as FBX for Unity's built-in model importer.
Silhouette fitting/conforming is intentionally a separate future pipeline stage
rather than hidden manual work.
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

import bpy


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
    return parser.parse_args(argv)


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)


def import_glb(path: Path) -> list[bpy.types.Object]:
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(path))
    return [obj for obj in bpy.data.objects if obj not in before]


def choose_object(
    objects: list[bpy.types.Object],
    object_type: str,
    requested_name: str | None,
    label: str,
) -> bpy.types.Object:
    if requested_name:
        obj = bpy.data.objects.get(requested_name)
        if obj is None or obj.type != object_type:
            raise RuntimeError(
                f"{label} '{requested_name}' was not found as a {object_type} object"
            )
        return obj

    matches = [obj for obj in objects if obj.type == object_type]
    if not matches:
        raise RuntimeError(f"No {object_type} object found for {label}")
    if len(matches) > 1:
        names = ", ".join(obj.name for obj in matches)
        raise RuntimeError(
            f"Multiple {object_type} objects found for {label}: {names}. "
            f"Set the object name explicitly in the build spec."
        )
    return matches[0]


def transfer_weights(
    garment: bpy.types.Object,
    body: bpy.types.Object,
    armature: bpy.types.Object,
    max_distance: float,
) -> None:
    if garment.type != "MESH":
        return

    garment.vertex_groups.clear()

    modifier = garment.modifiers.new(name="CanonicalWeightTransfer", type="DATA_TRANSFER")
    modifier.object = body
    modifier.use_vert_data = True
    modifier.data_types_verts = {"VGROUP_WEIGHTS"}
    modifier.vert_mapping = "POLYINTERP_NEAREST"
    modifier.use_max_distance = True
    modifier.max_distance = max_distance

    bpy.context.view_layer.objects.active = garment
    garment.select_set(True)
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    garment.select_set(False)

    armature_modifier = garment.modifiers.new(name="CanonicalArmature", type="ARMATURE")
    armature_modifier.object = armature
    garment.parent = armature

    if not garment.vertex_groups:
        raise RuntimeError(
            f"Weight transfer produced no vertex groups for garment '{garment.name}'"
        )


def export_wearable(
    output: Path,
    garment_objects: list[bpy.types.Object],
    armature: bpy.types.Object,
) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    armature.select_set(True)
    for obj in garment_objects:
        obj.select_set(True)

    output.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=str(output),
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="COPY",
        embed_textures=True,
    )


def main() -> int:
    args = parse_args()
    input_path = Path(args.input).resolve()
    canonical_path = Path(args.canonical).resolve()
    output_path = Path(args.output).resolve()

    if output_path.suffix.lower() != ".fbx":
        raise RuntimeError("wearable output must use .fbx for Unity import")

    clear_scene()
    canonical_objects = import_glb(canonical_path)
    armature = choose_object(
        canonical_objects, "ARMATURE", args.armature_object, "canonical armature"
    )
    body = choose_object(canonical_objects, "MESH", args.body_object, "canonical body")

    garment_import = import_glb(input_path)
    garment_objects = [obj for obj in garment_import if obj.type == "MESH"]
    if not garment_objects:
        raise RuntimeError("Generated wearable contains no mesh objects")

    for garment in garment_objects:
        transfer_weights(
            garment,
            body,
            armature,
            max_distance=args.max_transfer_distance,
        )

    body.hide_render = True
    export_wearable(output_path, garment_objects, armature)
    print(output_path)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
