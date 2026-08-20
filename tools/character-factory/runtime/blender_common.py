from __future__ import annotations

from pathlib import Path

import bpy


_WEIGHT_EPSILON = 1e-6
_FALLBACK_MASK_GROUP = "__CanonicalWeightFallbackMask"


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


def _has_positive_weight(vertex: bpy.types.MeshVertex) -> bool:
    return any(group.weight > _WEIGHT_EPSILON for group in vertex.groups)


def _apply_weight_transfer(
    mesh: bpy.types.Object,
    donor_body: bpy.types.Object,
    *,
    max_distance: float | None,
    vertex_group: str | None = None,
    name: str,
) -> None:
    modifier = mesh.modifiers.new(name=name, type="DATA_TRANSFER")
    modifier.object = donor_body
    modifier.use_vert_data = True
    modifier.data_types_verts = {"VGROUP_WEIGHTS"}
    modifier.vert_mapping = "POLYINTERP_NEAREST"
    modifier.layers_vgroup_select_src = "ALL"
    modifier.layers_vgroup_select_dst = "NAME"
    if vertex_group:
        modifier.vertex_group = vertex_group
    if max_distance is None:
        modifier.use_max_distance = False
    else:
        modifier.use_max_distance = True
        modifier.max_distance = max_distance

    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = mesh
    mesh.select_set(True)
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    mesh.select_set(False)


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

    # First preserve the existing bounded nearest-surface transfer. This gives the
    # highest-confidence correspondence for generated vertices close to the donor.
    _apply_weight_transfer(
        mesh,
        donor_body,
        max_distance=max_distance,
        name="CanonicalWeightTransfer",
    )

    primary_weighted = sum(
        1 for vertex in mesh.data.vertices if _has_positive_weight(vertex)
    )
    unweighted = [
        vertex.index for vertex in mesh.data.vertices if not _has_positive_weight(vertex)
    ]

    # Generated image-to-3D meshes can contain legitimate surface vertices farther
    # from the low-poly canonical donor than maxTransferDistance. Leaving those
    # vertices unweighted produces frozen patches when the skeleton animates. Use a
    # second nearest-surface transfer only on the vertices the bounded pass missed;
    # the mask means already-good weights are never overwritten by this fallback.
    fallback_filled = 0
    if unweighted:
        mask = mesh.vertex_groups.new(name=_FALLBACK_MASK_GROUP)
        mask.add(unweighted, 1.0, "REPLACE")
        _apply_weight_transfer(
            mesh,
            donor_body,
            max_distance=None,
            vertex_group=mask.name,
            name="CanonicalWeightFallback",
        )
        mesh.vertex_groups.remove(mask)
        fallback_filled = sum(
            1
            for index in unweighted
            if _has_positive_weight(mesh.data.vertices[index])
        )

    weighted_vertices = sum(
        1 for vertex in mesh.data.vertices if _has_positive_weight(vertex)
    )
    if weighted_vertices == 0:
        raise RuntimeError(
            f"Weight transfer assigned no vertices for mesh '{mesh.name}'"
        )

    armature_modifier = mesh.modifiers.new(name="CanonicalArmature", type="ARMATURE")
    armature_modifier.object = armature
    mesh.parent = armature

    print(
        f"canonical weights: mesh={mesh.name} groups={len(mesh.vertex_groups)} "
        f"primaryWeighted={primary_weighted}/{len(mesh.data.vertices)} "
        f"fallbackFilled={fallback_filled}/{len(unweighted)} "
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