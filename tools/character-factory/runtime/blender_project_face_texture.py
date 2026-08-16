#!/usr/bin/env python3
from __future__ import annotations

import argparse
import sys
from pathlib import Path

import bpy


FACE_MATERIAL_NAME = "MadelineFaceProjected"
FACE_UV_NAME = "MadelineFaceUV"


def parse_args() -> argparse.Namespace:
    argv = sys.argv
    if "--" not in argv:
        raise SystemExit("Expected Blender arguments after '--'")
    argv = argv[argv.index("--") + 1 :]

    parser = argparse.ArgumentParser(
        description=(
            "Project an approved frontal face crop onto the Head-weighted, front-facing "
            "polygons of a Character Factory FBX. The canonical Character Factory rig "
            "faces Blender world -Y, so world X/Z are used for planar face UVs."
        )
    )
    parser.add_argument("--input", required=True)
    parser.add_argument("--face", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--head-group", default="Head")
    parser.add_argument("--head-weight", type=float, default=0.32)
    parser.add_argument("--front-normal", type=float, default=0.08)
    parser.add_argument("--u-margin", type=float, default=0.06)
    parser.add_argument("--v-margin", type=float, default=0.04)
    return parser.parse_args(argv)


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)


def import_fbx(path: Path) -> None:
    result = bpy.ops.import_scene.fbx(filepath=str(path))
    if "FINISHED" not in result:
        raise RuntimeError(f"Unable to import FBX: {path}")


def find_body(head_group: str) -> tuple[bpy.types.Object, bpy.types.VertexGroup]:
    candidates: list[tuple[int, bpy.types.Object, bpy.types.VertexGroup]] = []
    for obj in bpy.context.scene.objects:
        if obj.type != "MESH":
            continue
        group = obj.vertex_groups.get(head_group)
        if group is None:
            continue
        candidates.append((len(obj.data.vertices), obj, group))

    if not candidates:
        raise RuntimeError(
            f"No mesh contains canonical head vertex group '{head_group}'. "
            "Run the normal Character Factory rigging step before face projection."
        )

    candidates.sort(key=lambda item: item[0], reverse=True)
    _, body, group = candidates[0]
    return body, group


def vertex_group_weight(vertex: bpy.types.MeshVertex, group_index: int) -> float:
    for membership in vertex.groups:
        if membership.group == group_index:
            return float(membership.weight)
    return 0.0


def head_vertex_indices(
    body: bpy.types.Object,
    group: bpy.types.VertexGroup,
    minimum_weight: float,
) -> set[int]:
    result = {
        vertex.index
        for vertex in body.data.vertices
        if vertex_group_weight(vertex, group.index) >= minimum_weight
    }
    if len(result) < 12:
        raise RuntimeError(
            f"Head selection is unexpectedly small ({len(result)} vertices); "
            f"lower --head-weight only after inspecting the rig."
        )
    return result


def world_point(body: bpy.types.Object, vertex_index: int):
    return body.matrix_world @ body.data.vertices[vertex_index].co


def bounds_xz(body: bpy.types.Object, indices: set[int]) -> tuple[float, float, float, float]:
    points = [world_point(body, index) for index in indices]
    min_x = min(point.x for point in points)
    max_x = max(point.x for point in points)
    min_z = min(point.z for point in points)
    max_z = max(point.z for point in points)
    if max_x - min_x <= 1e-6 or max_z - min_z <= 1e-6:
        raise RuntimeError("Head bounds collapsed while creating face projection UVs")
    return min_x, max_x, min_z, max_z


def create_face_material(face_path: Path) -> bpy.types.Material:
    image = bpy.data.images.load(str(face_path), check_existing=False)

    # Blender can keep externally loaded images lazy in background mode. In that
    # state Image.has_data may still be False even though the file header decoded
    # successfully and the image is usable by a texture node. The build has already
    # round-tripped and validated the source with Pillow, so treat valid dimensions
    # as the loader contract here instead of rejecting a lazy image data-block.
    width, height = int(image.size[0]), int(image.size[1])
    if width <= 0 or height <= 0:
        try:
            image.reload()
        except Exception as exc:
            raise RuntimeError(f"Unable to reload face source image: {face_path}") from exc
        width, height = int(image.size[0]), int(image.size[1])
    if width <= 0 or height <= 0:
        raise RuntimeError(
            f"Unable to decode face source image dimensions: {face_path} "
            f"size={tuple(image.size)} hasData={image.has_data}"
        )

    print(
        f"face image loader: {face_path.name} {width}x{height} hasData={image.has_data}",
        flush=True,
    )
    image.name = "MadelineFaceSource"

    material = bpy.data.materials.get(FACE_MATERIAL_NAME)
    if material is None:
        material = bpy.data.materials.new(FACE_MATERIAL_NAME)
    material.use_nodes = True

    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()

    output = nodes.new("ShaderNodeOutputMaterial")
    output.location = (420, 0)

    principled = nodes.new("ShaderNodeBsdfPrincipled")
    principled.location = (140, 0)
    principled.inputs["Roughness"].default_value = 0.62
    principled.inputs["Metallic"].default_value = 0.0

    texture = nodes.new("ShaderNodeTexImage")
    texture.location = (-180, 40)
    texture.image = image
    texture.interpolation = "Linear"
    texture.extension = "EXTEND"

    uv = nodes.new("ShaderNodeUVMap")
    uv.location = (-390, 40)
    uv.uv_map = FACE_UV_NAME

    links.new(uv.outputs["UV"], texture.inputs["Vector"])
    links.new(texture.outputs["Color"], principled.inputs["Base Color"])
    links.new(texture.outputs["Alpha"], principled.inputs["Alpha"])
    links.new(principled.outputs["BSDF"], output.inputs["Surface"])
    return material


def ensure_face_uv(body: bpy.types.Object) -> bpy.types.MeshUVLoopLayer:
    uv = body.data.uv_layers.get(FACE_UV_NAME)
    if uv is None:
        uv = body.data.uv_layers.new(name=FACE_UV_NAME, do_init=True)
    return uv


def material_slot(body: bpy.types.Object, material: bpy.types.Material) -> int:
    for index, existing in enumerate(body.data.materials):
        if existing == material:
            return index
    body.data.materials.append(material)
    return len(body.data.materials) - 1


def project_face(
    body: bpy.types.Object,
    head_indices: set[int],
    face_material: bpy.types.Material,
    front_normal_threshold: float,
    u_margin: float,
    v_margin: float,
) -> int:
    if not 0.0 <= u_margin < 0.45 or not 0.0 <= v_margin < 0.45:
        raise RuntimeError("UV margins must be in [0, 0.45)")

    min_x, max_x, min_z, max_z = bounds_xz(body, head_indices)
    span_x = max_x - min_x
    span_z = max_z - min_z

    uv_layer = ensure_face_uv(body)
    slot = material_slot(body, face_material)
    projected = 0
    candidate_world_normal_y: list[float] = []

    # FBX import can preserve the canonical -Y facing direction through an object
    # transform instead of baking it into mesh-local coordinates. Transform normals
    # with the inverse-transpose and evaluate the face gate in canonical world space.
    normal_matrix = body.matrix_world.to_3x3().inverted().transposed()

    for polygon in body.data.polygons:
        head_count = sum(1 for vertex_index in polygon.vertices if vertex_index in head_indices)
        if head_count < max(1, (len(polygon.vertices) + 1) // 2):
            continue

        world_normal = normal_matrix @ polygon.normal
        if world_normal.length_squared > 0.0:
            world_normal.normalize()
        candidate_world_normal_y.append(float(world_normal.y))

        # The Character Factory canonical mannequin faces world -Y. Requiring a
        # negative world-space Y normal keeps this source photograph on the face
        # instead of wrapping it over the rear of the skull. Cheeks remain eligible
        # at low angles through the intentionally small threshold.
        if world_normal.y >= -abs(front_normal_threshold):
            continue

        polygon.material_index = slot
        projected += 1

        for loop_index in polygon.loop_indices:
            vertex_index = body.data.loops[loop_index].vertex_index
            point = world_point(body, vertex_index)
            u = (point.x - min_x) / span_x
            v = (point.z - min_z) / span_z

            # Preserve a little source-image border around ears/chin/forehead so
            # bilinear sampling never pulls unrelated pixels into the face.
            u = u_margin + u * (1.0 - 2.0 * u_margin)
            v = v_margin + v * (1.0 - 2.0 * v_margin)
            uv_layer.data[loop_index].uv = (u, v)

    if projected == 0:
        normal_range = "unavailable"
        if candidate_world_normal_y:
            normal_range = (
                f"{min(candidate_world_normal_y):.4f}.."
                f"{max(candidate_world_normal_y):.4f}"
            )
        raise RuntimeError(
            "No front-facing Head polygons were selected in canonical world space. "
            f"headCandidateNormalY={normal_range} threshold={front_normal_threshold:.4f} "
            f"bodyMatrix={tuple(round(value, 5) for row in body.matrix_world for value in row)}"
        )

    return projected


def export_fbx(path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    result = bpy.ops.export_scene.fbx(
        filepath=str(path),
        use_selection=False,
        apply_unit_scale=True,
        bake_space_transform=False,
        add_leaf_bones=False,
        path_mode="COPY",
        embed_textures=True,
    )
    if "FINISHED" not in result:
        raise RuntimeError(f"Unable to export FBX: {path}")


def main() -> int:
    args = parse_args()
    input_path = Path(args.input).resolve()
    face_path = Path(args.face).resolve()
    output_path = Path(args.output).resolve()

    if not input_path.is_file():
        raise SystemExit(f"input FBX does not exist: {input_path}")
    if not face_path.is_file():
        raise SystemExit(f"face source does not exist: {face_path}")

    clear_scene()
    import_fbx(input_path)
    body, head_group = find_body(args.head_group)
    head_indices = head_vertex_indices(body, head_group, args.head_weight)
    face_material = create_face_material(face_path)
    projected = project_face(
        body,
        head_indices,
        face_material,
        args.front_normal,
        args.u_margin,
        args.v_margin,
    )
    export_fbx(output_path)

    print(
        f"Madeline face projection: body={body.name} "
        f"headVertices={len(head_indices)} polygons={projected} output={output_path}",
        flush=True,
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
