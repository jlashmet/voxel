from __future__ import annotations

from pathlib import Path

import bpy

from blender_multiview_texture import (
    _atlas_image,
    _atlas_uv,
    _bounds,
    _load_subject_image,
    _material,
    _projection_for_normal,
    _projection_for_polygon,
    _source_uv,
    _subject_adjusted_uv,
)
from blender_rigid_reference import load_rigid_subject_image


PROJECTION_PROFILES = ("character", "garment", "rigid")


def _projection_for_profile(
    profile: str,
    *,
    normal,
    centroid,
    center,
    x_half_span: float,
) -> tuple[str, bool]:
    if profile == "character":
        return _projection_for_polygon(
            normal,
            centroid,
            center,
            x_half_span,
        )
    if profile in {"garment", "rigid"}:
        # Garments and rigid equipment must not inherit the character/T-pose
        # outer-arm redirect. Their view is selected only from the local surface
        # orientation. This deliberately keeps shared atlas/UV mechanics while
        # making object-type projection policy independent.
        return _projection_for_normal(normal), False
    raise ValueError(f"unknown multiview projection profile: {profile}")


def project_profiled_multiview_texture(
    meshes: list[bpy.types.Object],
    *,
    front: Path,
    back: Path,
    left: Path,
    right: Path,
    output: Path,
    profile: str,
) -> Path:
    if profile not in PROJECTION_PROFILES:
        raise ValueError(
            f"profile must be one of: {', '.join(PROJECTION_PROFILES)}"
        )

    # Character/garment references are one connected silhouette. Rigid equipment
    # can contain legitimate detached ornaments or multipart geometry, so its
    # reference loader keeps every substantial foreground component while still
    # rejecting tiny compression speckles.
    loader = load_rigid_subject_image if profile == "rigid" else _load_subject_image
    sources = {
        "front": loader(front),
        "back": loader(back),
        "left": loader(left),
        "right": loader(right),
    }
    atlas = _atlas_image(sources, output)
    material = _material(atlas)
    lo, hi = _bounds(meshes)
    center = (lo + hi) * 0.5
    x_half_span = max(abs(hi.x - lo.x) * 0.5, 1e-8)
    redirected_polygons = 0
    projected_polygons = 0

    for mesh in meshes:
        if mesh.type != "MESH":
            continue
        mesh.data.materials.clear()
        mesh.data.materials.append(material)
        uv_layer = mesh.data.uv_layers.get("CharacterFactoryMultiview")
        if uv_layer is None:
            uv_layer = mesh.data.uv_layers.new(name="CharacterFactoryMultiview")
        mesh.data.uv_layers.active = uv_layer

        normal_matrix = mesh.matrix_world.to_3x3().inverted().transposed()
        world_matrix = mesh.matrix_world
        for polygon in mesh.data.polygons:
            world_normal = (normal_matrix @ polygon.normal).normalized()
            centroid = world_matrix @ polygon.center
            view, redirected = _projection_for_profile(
                profile,
                normal=world_normal,
                centroid=centroid,
                center=center,
                x_half_span=x_half_span,
            )
            projected_polygons += 1
            if redirected:
                redirected_polygons += 1
            source = sources[view]
            for loop_index in polygon.loop_indices:
                vertex_index = mesh.data.loops[loop_index].vertex_index
                point = world_matrix @ mesh.data.vertices[vertex_index].co
                u, v = _source_uv(view, point, lo, hi)
                u, v = _subject_adjusted_uv(source, u, v)
                uv_layer.data[loop_index].uv = _atlas_uv(view, u, v)
        mesh.data.update()

    print(
        "profiled multiview texture projection: "
        f"profile={profile} polygons={projected_polygons} "
        f"redirectedPolygons={redirected_polygons} atlas={output}",
        flush=True,
    )
    return output
