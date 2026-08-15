from __future__ import annotations

from pathlib import Path

import bpy


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


def generated_meshes(objects: list[bpy.types.Object], label: str) -> list[bpy.types.Object]:
    meshes = [obj for obj in objects if obj.type == "MESH"]
    if not meshes:
        raise RuntimeError(f"Generated {label} contains no mesh objects")
    return meshes


def transfer_weights(
    mesh: bpy.types.Object,
    donor_body: bpy.types.Object,
    armature: bpy.types.Object,
    max_distance: float,
) -> None:
    mesh.vertex_groups.clear()

    modifier = mesh.modifiers.new(name="CanonicalWeightTransfer", type="DATA_TRANSFER")
    modifier.object = donor_body
    modifier.use_vert_data = True
    modifier.data_types_verts = {"VGROUP_WEIGHTS"}
    modifier.vert_mapping = "POLYINTERP_NEAREST"
    modifier.use_max_distance = True
    modifier.max_distance = max_distance

    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = mesh
    mesh.select_set(True)
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    mesh.select_set(False)

    armature_modifier = mesh.modifiers.new(name="CanonicalArmature", type="ARMATURE")
    armature_modifier.object = armature
    mesh.parent = armature

    if not mesh.vertex_groups:
        raise RuntimeError(
            f"Weight transfer produced no vertex groups for mesh '{mesh.name}'"
        )


def apply_mesh_transforms(meshes: list[bpy.types.Object]) -> None:
    for mesh in meshes:
        bpy.ops.object.select_all(action="DESELECT")
        mesh.select_set(True)
        bpy.context.view_layer.objects.active = mesh
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
        mesh.select_set(False)


def export_fbx(
    output: Path,
    objects: list[bpy.types.Object],
) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)

    output.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=str(output),
        use_selection=True,
        object_types={"ARMATURE", "MESH", "EMPTY"},
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="COPY",
        embed_textures=True,
    )
