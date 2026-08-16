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

    # Blender's Data Transfer modifier intentionally does not create destination
    # data layers. Mirror the canonical donor's group layout first, then transfer
    # every source group by name. Without this step the modifier can apply cleanly
    # while transferring zero weights to a newly generated mesh.
    donor_groups = [group.name for group in donor_body.vertex_groups]
    if not donor_groups:
        raise RuntimeError(
            f"Canonical donor body '{donor_body.name}' contains no vertex groups"
        )
    for name in donor_groups:
        mesh.vertex_groups.new(name=name)

    modifier = mesh.modifiers.new(name="CanonicalWeightTransfer", type="DATA_TRANSFER")
    modifier.object = donor_body
    modifier.use_vert_data = True
    modifier.data_types_verts = {"VGROUP_WEIGHTS"}
    modifier.vert_mapping = "POLYINTERP_NEAREST"
    modifier.layers_vgroup_select_src = "ALL"
    modifier.layers_vgroup_select_dst = "NAME"
    modifier.use_max_distance = True
    modifier.max_distance = max_distance

    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = mesh
    mesh.select_set(True)
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    mesh.select_set(False)

    weighted_vertices = sum(1 for vertex in mesh.data.vertices if vertex.groups)
    if weighted_vertices == 0:
        raise RuntimeError(
            f"Weight transfer assigned no vertices for mesh '{mesh.name}'"
        )

    armature_modifier = mesh.modifiers.new(name="CanonicalArmature", type="ARMATURE")
    armature_modifier.object = armature
    mesh.parent = armature

    print(
        f"canonical weights: mesh={mesh.name} groups={len(mesh.vertex_groups)} "
        f"weightedVertices={weighted_vertices}/{len(mesh.data.vertices)}",
        flush=True,
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
    bake_anim: bool = False,
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
        bake_anim=bake_anim,
        bake_anim_use_all_actions=bake_anim,
        bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=bake_anim,
        bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0,
        path_mode="COPY",
        embed_textures=True,
    )
