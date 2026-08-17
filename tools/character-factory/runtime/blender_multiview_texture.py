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


def _largest_connected_foreground(
    rows: tuple[tuple[tuple[int, int], ...], ...],
) -> tuple[tuple[tuple[int, int], ...], ...]:
    """Keep the largest 8-connected run component from the detected silhouette.

    JPEG compression adds tiny colored speckles to an otherwise neutral canvas. A
    chroma-only foreground test can classify those isolated pixels as character and
    later atlas padding can expand them into large neutral strips. The body/hair
    turnaround is one connected silhouette, so retain only its largest run component.
    """

    parent: list[int] = []
    weight: list[int] = []
    row_nodes: list[list[tuple[int, int, int]]] = []

    def make_node(pixel_count: int) -> int:
        node = len(parent)
        parent.append(node)
        weight.append(pixel_count)
        return node

    def find(node: int) -> int:
        while parent[node] != node:
            parent[node] = parent[parent[node]]
            node = parent[node]
        return node

    def union(a: int, b: int) -> None:
        root_a = find(a)
        root_b = find(b)
        if root_a == root_b:
            return
        if weight[root_a] < weight[root_b]:
            root_a, root_b = root_b, root_a
        parent[root_b] = root_a
        weight[root_a] += weight[root_b]

    previous: list[tuple[int, int, int]] = []
    for runs in rows:
        current: list[tuple[int, int, int]] = []
        previous_index = 0
        for start, end in runs:
            node = make_node(end - start + 1)
            while (
                previous_index < len(previous)
                and previous[previous_index][1] < start - 1
            ):
                previous_index += 1
            candidate = previous_index
            while candidate < len(previous) and previous[candidate][0] <= end + 1:
                union(node, previous[candidate][2])
                candidate += 1
            current.append((start, end, node))
        row_nodes.append(current)
        previous = current

    if not parent:
        return rows

    largest_root = max(
        {find(node) for node in range(len(parent))},
        key=lambda root: weight[root],
    )
    return tuple(
        tuple(
            (start, end)
            for start, end, node in row
            if find(node) == largest_root
        )
        for row in row_nodes
    )


def _load_subject_image(path: Path) -> ImageInfo:
    image = bpy.data.images.load(str(path.resolve()), check_existing=False)
    width = int(image.size[0])
    height = int(image.size[1])
    if width <= 1 or height <= 1:
        raise RuntimeError(f"multiview texture source has invalid dimensions: {path}")

    pixels = image.pixels[:]
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

    raw_rows: list[tuple[tuple[int, int], ...]] = []
    for y in range(height):
        row = y * width * 4
        runs: list[tuple[int, int]] = []
        run_start: int | None = None
        for x in range(width):
            index = row + x * 4
            r, g, b, a = pixels[index : index + 4]
            mean = (r + g + b) / 3.0
            chroma = max(r, g, b) - min(r, g, b)
            # The approved neutral canvas contains JPEG chroma noise around 0.025.
            # Require a clearer color separation or a meaningful luminance drop so
            # compression speckles cannot masquerade as the subject silhouette.
            foreground = a > 0.05 and (chroma > 0.05 or mean < background - 0.035)
            if foreground:
                if run_start is None:
                    run_start = x
            elif run_start is not None:
                runs.append((run_start, x - 1))
                run_start = None
        if run_start is not None:
            runs.append((run_start, width - 1))
        raw_rows.append(tuple(runs))

    raw_foreground = tuple(raw_rows)
    foreground_rows = _largest_connected_foreground(raw_foreground)
    raw_pixels = sum(end - start + 1 for row in raw_foreground for start, end in row)
    kept_pixels = sum(end - start + 1 for row in foreground_rows for start, end in row)

    min_x = width
    max_x = -1
    min_y = height
    max_y = -1
    for y, runs in enumerate(foreground_rows):
        for start, end in runs:
            min_x = min(min_x, start)
            max_x = max(max_x, end)
            min_y = min(min_y, y)
            max_y = max(max_y, y)

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
        foreground_runs=foreground_rows,
    )
    print(
        f"multiview source crop: {path.name} {width}x{height} "
        f"bbox=({info.x0},{info.y0})-({info.x1},{info.y1}) bg={background:.4f} "
        f"maskPixels={kept_pixels}/{raw_pixels} removed={raw_pixels - kept_pixels}",
        flush=True,
    )
    return info


def _copy_rgba(
    pixels: list[float],
    width: int,
    dst_x: int,
    dst_y: int,
    src_x: int,
    src_y: int,
) -> None:
    dst = (dst_y * width + dst_x) * 4
    src = (src_y * width + src_x) * 4
    pixels[dst : dst + 4] = pixels[src : src + 4]


def _safe_run(run: tuple[int, int], inset: int) -> tuple[int, int]:
    start, end = run
    safe_start = min(end, start + inset)
    safe_end = max(start, end - inset)
    if safe_start > safe_end:
        return start, end
    return safe_start, safe_end


def _edge_padded_pixels(source: ImageInfo) -> tuple[list[float], int]:
    """Fill turnaround canvas with inset local silhouette colors.

    UV vertices are constrained to detected foreground, but the interior of a UV
    triangle is linearly interpolated. When a triangle bridges an irregular hand,
    hair, or leg silhouette, that interpolation can still cross the neutral canvas.
    Build a deterministic padded copy while retaining the immutable subject mask for
    projection checks. Padding samples a few pixels inside each run so anti-aliased
    canvas colors at the silhouette edge are not expanded across the atlas.
    """

    width = source.width
    height = source.height
    padded = list(source.image.pixels[:])
    valid_rows: list[int] = []
    padded_pixels = 0
    inset = max(1, int(round(min(width, height) * 0.002)))

    for y, runs in enumerate(source.foreground_runs):
        if not runs:
            continue
        valid_rows.append(y)
        safe_runs = tuple(_safe_run(run, inset) for run in runs)

        first_start = safe_runs[0][0]
        for x in range(0, runs[0][0]):
            _copy_rgba(padded, width, x, y, first_start, y)
            padded_pixels += 1

        for index in range(len(runs) - 1):
            left_end = safe_runs[index][1]
            right_start = safe_runs[index + 1][0]
            gap_start = runs[index][1] + 1
            gap_end = runs[index + 1][0] - 1
            if gap_start > gap_end:
                continue
            midpoint = (gap_start + gap_end) * 0.5
            for x in range(gap_start, gap_end + 1):
                source_x = left_end if x <= midpoint else right_start
                _copy_rgba(padded, width, x, y, source_x, y)
                padded_pixels += 1

        last_end = safe_runs[-1][1]
        for x in range(runs[-1][1] + 1, width):
            _copy_rgba(padded, width, x, y, last_end, y)
            padded_pixels += 1

    if not valid_rows:
        raise RuntimeError("cannot edge-pad a multiview source without foreground rows")

    nearest_above: list[int | None] = [None] * height
    nearest_below: list[int | None] = [None] * height
    last_valid: int | None = None
    for y in range(height):
        if source.foreground_runs[y]:
            last_valid = y
        nearest_above[y] = last_valid
    last_valid = None
    for y in range(height - 1, -1, -1):
        if source.foreground_runs[y]:
            last_valid = y
        nearest_below[y] = last_valid

    row_width = width * 4
    for y, runs in enumerate(source.foreground_runs):
        if runs:
            continue
        above = nearest_above[y]
        below = nearest_below[y]
        if above is None:
            source_y = below
        elif below is None:
            source_y = above
        else:
            source_y = above if y - above <= below - y else below
        if source_y is None:
            raise RuntimeError("failed to locate a neighboring subject row for atlas padding")
        dst = y * row_width
        src = source_y * row_width
        padded[dst : dst + row_width] = padded[src : src + row_width]
        padded_pixels += width

    return padded, padded_pixels


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
        padded_pixels, padded_count = _edge_padded_pixels(source)
        padded_image = bpy.data.images.new(
            f"CharacterFactoryMultiviewPadded_{name}",
            width=source.width,
            height=source.height,
            alpha=True,
        )
        padded_image.pixels[:] = padded_pixels
        if source.width != tile or source.height != tile:
            padded_image.scale(tile, tile)
        pixels = padded_image.pixels[:]
        ox, oy = placements[name]
        for y in range(tile):
            src_row = y * tile * 4
            dst_row = ((oy + y) * atlas_size + ox) * 4
            target[dst_row : dst_row + tile * 4] = pixels[
                src_row : src_row + tile * 4
            ]
        print(
            f"multiview atlas padding: view={name} paddedPixels={padded_count}",
            flush=True,
        )

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
        return _normalized(point.x, lo.x, hi.x), _normalized(point.z, lo.z, hi.z)
    if name == "back":
        return 1.0 - _normalized(point.x, lo.x, hi.x), _normalized(point.z, lo.z, hi.z)
    if name == "left":
        return _normalized(point.y, lo.y, hi.y), _normalized(point.z, lo.z, hi.z)
    if name == "right":
        return 1.0 - _normalized(point.y, lo.y, hi.y), _normalized(point.z, lo.z, hi.z)
    raise ValueError(name)


def _nearest_foreground_uv(source: ImageInfo, u: float, v: float) -> tuple[float, float]:
    """Keep a projected coordinate on the true nearest visible turnaround pixel."""

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
                safe_start, safe_end = _safe_run((start, end), inset)
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

        if best is not None and float(radius * radius) >= best[0]:
            break

    if best is None:
        return u, v
    _, x, y = best
    return x / max(1, width - 1), y / max(1, height - 1)


def _subject_adjusted_uv(source: ImageInfo, u: float, v: float) -> tuple[float, float]:
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
    inset = 1.0 / 1024.0
    return (
        tile[0] + inset + u * (0.5 - inset * 2.0),
        tile[1] + inset + v * (0.5 - inset * 2.0),
    )


def _projection_for_normal(normal: Vector) -> str:
    if abs(normal.x) > abs(normal.y) * 1.05:
        return "left" if normal.x >= 0.0 else "right"
    return "front" if normal.y <= 0.0 else "back"


def _projection_for_polygon(
    normal: Vector,
    centroid: Vector,
    center: Vector,
    x_half_span: float,
    *,
    outer_span_fraction: float = 0.45,
) -> tuple[str, bool]:
    """Select a view without collapsing T-pose arm length into a side profile.

    Side orthographic views discard world X. For polygons near the outer X extent,
    that removes the axis along the arm and maps many distant arm vertices onto the
    same narrow shoulder/hand profile strip. Redirect only those polygons to the
    front/back image matching their local circumference.
    """

    view = _projection_for_normal(normal)
    outer_span = abs(float(centroid.x - center.x)) / max(x_half_span, 1e-8)
    if view not in {"left", "right"} or outer_span < outer_span_fraction:
        return view, False

    if abs(float(normal.y)) >= 0.05:
        return ("front" if normal.y <= 0.0 else "back"), True
    return ("front" if centroid.y <= center.y else "back"), True


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
    """Project four clean orthographic turnaround views onto aligned geometry."""

    sources = {
        "front": _load_subject_image(front),
        "back": _load_subject_image(back),
        "left": _load_subject_image(left),
        "right": _load_subject_image(right),
    }
    atlas = _atlas_image(sources, output)
    material = _material(atlas)
    lo, hi = _bounds(meshes)
    center = (lo + hi) * 0.5
    x_half_span = max(abs(hi.x - lo.x) * 0.5, 1e-8)
    outer_side_redirects = 0

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
            view, redirected = _projection_for_polygon(
                world_normal,
                centroid,
                center,
                x_half_span,
            )
            if redirected:
                outer_side_redirects += 1
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
        f"outerSideRedirects={outer_side_redirects} atlas={output}",
        flush=True,
    )
    return output
