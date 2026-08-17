from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
import statistics

import bpy
from mathutils import Vector


@dataclass(frozen=True)
class ImageInfo:
    image: bpy.types.Image
    x0: int
    y0: int
    x1: int
    y1: int
    source_width: int
    source_height: int
    foreground_runs: tuple[tuple[tuple[int, int], ...], ...]

    @property
    def width(self) -> int:
        return self.source_width

    @property
    def height(self) -> int:
        return self.source_height


def _load_subject_image(path: Path) -> ImageInfo:
    image = bpy.data.images.load(str(path.resolve()), check_existing=False)
    width = int(image.size[0])
    height = int(image.size[1])
    if width <= 1 or height <= 1:
        raise RuntimeError(f"multiview texture source has invalid dimensions: {path}")

    pixels = image.pixels[:]

    # Approved turnarounds use a light neutral gray canvas rather than pure white.
    # Estimate that canvas from the corners in Blender's own color space, then find
    # character pixels by either color/chroma separation or a meaningful drop in
    # luminance. This remains stable whether Blender exposes the image as sRGB-like
    # values or scene-linear floats.
    sample_radius = max(2, min(width, height) // 64)
    background_levels: list[float] = []
    for y0 in (0, height - sample_radius):
        for x0 in (0, width - sample_radius):
            for y in range(y0, min(height, y0 + sample_radius)):
                row = y * width * 4
                for x in range(x0, min(width, x0 + sample_radius)):
                    index = row + x * 4
                    r, g, b, _a = pixels[index : index + 4]
                    background_levels.append((r + g + b) / 3.0)
    background = statistics.median(background_levels)

    min_x = width
    max_x = -1
    min_y = height
    max_y = -1
    foreground_rows: list[tuple[tuple[int, int], ...]] = []

    for y in range(height):
        row = y * width * 4
        runs: list[tuple[int, int]] = []
        run_start: int | None = None
        for x in range(width):
            index = row + x * 4
            r, g, b, a = pixels[index : index + 4]
            mean = (r + g + b) / 3.0
            chroma = max(r, g, b) - min(r, g, b)
            foreground = a > 0.05 and (chroma > 0.025 or mean < background - 0.035)
            if foreground:
                min_x = min(min_x, x)
                max_x = max(max_x, x)
                min_y = min(min_y, y)
                max_y = max(max_y, y)
                if run_start is None:
                    run_start = x
            elif run_start is not None:
                runs.append((run_start, x - 1))
                run_start = None
        if run_start is not None:
            runs.append((run_start, width - 1))
        foreground_rows.append(tuple(runs))

    if max_x < min_x or max_y < min_y:
        raise RuntimeError(f"could not find subject against neutral background: {path}")

    pad_x = max(2, int(round(width * 0.01)))
    pad_y = max(2, int(round(height * 0.01)))
    info = ImageInfo(
        image=image,
        x0=max(0, min_x - pad_x),
        y0=max(0, min_y - pad_y),
        x1=min(width - 1, max_x + pad_x),
        y1=min(height - 1, max_y + pad_y),
        source_width=width,
        source_height=height,
        foreground_runs=tuple(foreground_rows),
    )
    print(
        f"multiview source crop: {path.name} {width}x{height} "
        f"bbox=({info.x0},{info.y0})-({info.x1},{info.y1}) bg={background:.4f}",
        flush=True,
    )
    return info


def _atlas_image(sources: dict[str, ImageInfo], output: Path) -> bpy.types.Image:
    tile = 512
    atlas_size = tile * 2
    atlas = bpy.data.images.new(
        "CharacterFactoryMultiviewBaseColor",
        width=atlas_size,
        height=atlas_size,
        alpha=True,
    )
    target = [1.0] * (atlas_size * atlas_size * 4)

    placements = {
        "front": (0, tile),
        "back": (tile, tile),
        "left": (0, 0),
        "right": (tile, 0),
    }

    for name, source in sources.items():
        image = source.image
        if int(image.size[0]) != tile or int(image.size[1]) != tile:
            image.scale(tile, tile)
        pixels = image.pixels[:]
        ox, oy = placements[name]
        for y in range(tile):
            src_row = y * tile * 4
            dst_row = ((oy + y) * atlas_size + ox) * 4
            target[dst_row : dst_row + tile * 4] = pixels[
                src_row : src_row + tile * 4
            ]

    atlas.pixels[:] = target
    atlas.filepath_raw = str(output.resolve())
    atlas.file_format = "PNG"
    output.parent.mkdir(parents=True, exist_ok=True)
    atlas.save()
    return atlas


def _bounds(meshes: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    points: list[Vector] = []
    for mesh in meshes:
        matrix = mesh.matrix_world
        points.extend(matrix @ vertex.co for vertex in mesh.data.vertices)
    if not points:
        raise RuntimeError("multiview texture projection found no vertices")
    lo = Vector(tuple(min(point[i] for point in points) for i in range(3)))
    hi = Vector(tuple(max(point[i] for point in points) for i in range(3)))
    return lo, hi


def _normalized(value: float, low: float, high: float) -> float:
    extent = high - low
    if abs(extent) < 1e-8:
        return 0.5
    return max(0.0, min(1.0, (value - low) / extent))


def _source_uv(
    name: str,
    point: Vector,
    lo: Vector,
    hi: Vector,
) -> tuple[float, float]:
    if name == "front":
        # Canonical character front faces -Y. Character-left is +X and appears
        # viewer-right in a front turnaround.
        return _normalized(point.x, lo.x, hi.x), _normalized(point.z, lo.z, hi.z)
    if name == "back":
        # Behind the character, left/right swap from the viewer's perspective.
        return 1.0 - _normalized(point.x, lo.x, hi.x), _normalized(point.z, lo.z, hi.z)
    if name == "left":
        # The approved left-side reference faces image-left: canonical front (-Y)
        # therefore maps to low U, while the back (+Y) maps to high U.
        return _normalized(point.y, lo.y, hi.y), _normalized(point.z, lo.z, hi.z)
    if name == "right":
        # The approved right-side reference faces image-right, the horizontal mirror
        # of the left reference: canonical front (-Y) maps to high U.
        return 1.0 - _normalized(point.y, lo.y, hi.y), _normalized(point.z, lo.z, hi.z)
    raise ValueError(name)


def _nearest_foreground_uv(source: ImageInfo, u: float, v: float) -> tuple[float, float]:
    """Keep a projected coordinate on the true nearest visible turnaround pixel.

    Global planar projection does not perfectly follow local silhouette bends. The
    previous implementation stopped at the first *row* containing any foreground,
    which could move a hand coordinate hundreds of pixels sideways to an upper-arm
    strip even when the actual hand was only a few rows away. Search in image-space
    Euclidean distance and stop only when unexplored rows cannot beat the best 2D
    candidate already found.
    """

    width = source.width
    height = source.height
    target_x = int(round(max(0.0, min(1.0, u)) * (width - 1)))
    target_y = int(round(max(0.0, min(1.0, v)) * (height - 1)))
    inset = max(1, int(round(min(width, height) * 0.002)))

    best: tuple[float, int, int] | None = None
    max_radius = max(height, width)
    for radius in range(max_radius + 1):
        rows = [target_y] if radius == 0 else [target_y - radius, target_y + radius]
        for y in rows:
            if y < 0 or y >= height:
                continue
            runs = source.foreground_runs[y]
            if not runs:
                continue
            for start, end in runs:
                safe_start = min(end, start + inset)
                safe_end = max(start, end - inset)
                if safe_start > safe_end:
                    safe_start, safe_end = start, end
                x = min(max(target_x, safe_start), safe_end)
                dx = x - target_x
                dy = y - target_y
                distance_sq = float(dx * dx + dy * dy)
                if best is None or distance_sq < best[0]:
                    best = (distance_sq, x, y)
                if safe_start <= target_x <= safe_end and y == target_y:
                    return (
                        target_x / max(1, width - 1),
                        target_y / max(1, height - 1),
                    )

        # Any row not yet visited is at least radius+1 pixels away vertically. If
        # radius alone already equals/exceeds the best complete 2D distance, later
        # rows cannot produce a closer pixel regardless of their horizontal match.
        if best is not None and float(radius * radius) >= best[0]:
            break

    if best is None:
        return u, v
    _, x, y = best
    return x / max(1, width - 1), y / max(1, height - 1)


def _subject_adjusted_uv(source: ImageInfo, u: float, v: float) -> tuple[float, float]:
    # Bounding boxes were measured before image.scale(). Keep the original source
    # dimensions stored in ImageInfo so later atlas resizing cannot corrupt the
    # crop normalization.
    x0 = source.x0 / max(1, source.width - 1)
    x1 = source.x1 / max(1, source.width - 1)
    y0 = source.y0 / max(1, source.height - 1)
    y1 = source.y1 / max(1, source.height - 1)
    adjusted_u = x0 + u * (x1 - x0)
    adjusted_v = y0 + v * (y1 - y0)
    return _nearest_foreground_uv(source, adjusted_u, adjusted_v)


def _atlas_uv(name: str, u: float, v: float) -> tuple[float, float]:
    tile = {
        "front": (0.0, 0.5),
        "back": (0.5, 0.5),
        "left": (0.0, 0.0),
        "right": (0.5, 0.0),
    }[name]
    # Keep a tiny inset so bilinear filtering never samples the adjacent view.
    inset = 1.0 / 1024.0
    return (
        tile[0] + inset + u * (0.5 - inset * 2.0),
        tile[1] + inset + v * (0.5 - inset * 2.0),
    )


def _projection_for_normal(normal: Vector) -> str:
    # Side projections win only when the surface is more lateral than frontal.
    # Up/down-facing cloth uses the nearest front/back source rather than a
    # stretched side texture.
    if abs(normal.x) > abs(normal.y) * 1.05:
        return "left" if normal.x >= 0.0 else "right"
    return "front" if normal.y <= 0.0 else "back"


def _material(atlas: bpy.types.Image) -> bpy.types.Material:
    material = bpy.data.materials.new("CharacterFactoryMultiviewMaterial")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    for node in list(nodes):
        nodes.remove(node)

    output = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = atlas
    texture.interpolation = "Linear"
    shader.inputs["Roughness"].default_value = 0.62
    shader.inputs["Metallic"].default_value = 0.0
    links.new(texture.outputs["Color"], shader.inputs["Base Color"])
    links.new(texture.outputs["Alpha"], shader.inputs["Alpha"])
    links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    return material


def project_multiview_texture(
    meshes: list[bpy.types.Object],
    *,
    front: Path,
    back: Path,
    left: Path,
    right: Path,
    output: Path,
) -> Path:
    """Project four clean orthographic turnaround views onto aligned geometry.

    UVs are selected per polygon from the most-facing camera, which preserves
    crisp source colors and avoids dependence on a learned texture generator.
    This is deliberately deterministic and headless; seams can later be blended
    as a quality refinement without changing the pipeline contract.
    """

    sources = {
        "front": _load_subject_image(front),
        "back": _load_subject_image(back),
        "left": _load_subject_image(left),
        "right": _load_subject_image(right),
    }
    atlas = _atlas_image(sources, output)
    material = _material(atlas)
    lo, hi = _bounds(meshes)

    for mesh in meshes:
        if mesh.type != "MESH":
            continue
        mesh.data.materials.clear()
        mesh.data.materials.append(material)
        uv_layer = mesh.data.uv_layers.get("CharacterFactoryMultiview")
        if uv_layer is None:
            uv_layer = mesh.data.uv_layers.new(name="CharacterFactoryMultiview")
        mesh.data.uv_layers.active = uv_layer

        # Normals transform with the inverse-transpose. On Madeline's current FBX
        # this selects the same views as the historical direct 3x3 transform, but
        # using the correct basis prevents future non-uniform object transforms from
        # silently changing source-view selection.
        normal_matrix = mesh.matrix_world.to_3x3().inverted().transposed()
        world_matrix = mesh.matrix_world
        for polygon in mesh.data.polygons:
            world_normal = (normal_matrix @ polygon.normal).normalized()
            view = _projection_for_normal(world_normal)
            source = sources[view]
            for loop_index in polygon.loop_indices:
                vertex_index = mesh.data.loops[loop_index].vertex_index
                point = world_matrix @ mesh.data.vertices[vertex_index].co
                u, v = _source_uv(view, point, lo, hi)
                u, v = _subject_adjusted_uv(source, u, v)
                uv_layer.data[loop_index].uv = _atlas_uv(view, u, v)
        mesh.data.update()

    print(
        "multiview texture projection: "
        f"front={front.name} back={back.name} left={left.name} right={right.name} "
        f"atlas={output}",
        flush=True,
    )
    return output
