#!/usr/bin/env python3
from __future__ import annotations

import argparse
from pathlib import Path
import shutil
import subprocess

import numpy as np
from PIL import Image
import trimesh


PROJECTION_SCORE_THRESHOLD = 0.80
PROJECTION_NORMAL_THRESHOLD = 0.55


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="TripoSR Apple-MPS mesh adapter")
    parser.add_argument("--source", required=True)
    parser.add_argument("--weights", required=True)
    parser.add_argument("--front", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--device", default="auto")
    parser.add_argument("--chunk-size", type=int, default=8192)
    parser.add_argument("--mc-resolution", type=int, default=64)
    parser.add_argument("--remove-background", action="store_true")
    parser.add_argument("--texture-resolution", type=int, default=1024)
    parser.add_argument(
        "--no-bake-texture",
        action="store_true",
        help="Keep TripoSR vertex colors instead of baking a UV texture atlas.",
    )
    return parser.parse_args()


def patch_mps_texture_baker(source: Path) -> None:
    """Patch the pinned Mac TripoSR fork's CPU-only texture query for MPS.

    Geometry inference already works on MPS. The pinned texture baker creates its
    sampling positions on CPU and then calls numpy() directly on the renderer
    result, which breaks once the triplane is on MPS. Keep this small, guarded
    compatibility patch beside our adapter rather than maintaining a fork.
    """

    path = source / "tsr" / "bake_texture.py"
    text = path.read_text(encoding="utf-8")

    old_positions = "positions = torch.tensor(positions_texture.reshape(-1, 4)[:, :-1])"
    new_positions = (
        "positions = torch.tensor(positions_texture.reshape(-1, 4)[:, :-1], "
        "device=scene_code.device, dtype=scene_code.dtype)"
    )
    old_numpy = 'rgb_f = queried_grid["color"].numpy().reshape(-1, 3)'
    new_numpy = 'rgb_f = queried_grid["color"].detach().cpu().numpy().reshape(-1, 3)'

    changed = False
    if old_positions in text:
        text = text.replace(old_positions, new_positions, 1)
        changed = True
    elif new_positions not in text:
        raise RuntimeError("pinned TripoSR texture position query changed unexpectedly")

    if old_numpy in text:
        text = text.replace(old_numpy, new_numpy, 1)
        changed = True
    elif new_numpy not in text:
        raise RuntimeError("pinned TripoSR texture result conversion changed unexpectedly")

    if changed:
        path.write_text(text, encoding="utf-8")
        print(f"patched MPS-safe TripoSR texture baker: {path}", flush=True)


def _foreground_data(
    image: Image.Image,
) -> tuple[np.ndarray, np.ndarray, tuple[int, int, int, int]] | None:
    rgba = np.asarray(image.convert("RGBA"), dtype=np.uint8)
    alpha = rgba[..., 3]
    if alpha.min() < 250:
        mask = alpha > 16
    else:
        rgb = rgba[..., :3].astype(np.float32)
        border = np.concatenate(
            [rgb[0, :, :], rgb[-1, :, :], rgb[:, 0, :], rgb[:, -1, :]],
            axis=0,
        )
        background = np.median(border, axis=0)
        border_delta = np.max(np.abs(border - background), axis=1)
        # Source-guided texture transfer is only safe when the input has a
        # deliberately simple background (the CI/automation path uses neutral gray).
        if float(np.percentile(border_delta, 90)) > 18.0:
            return None
        mask = np.max(np.abs(rgb - background), axis=2) > 12.0

    count = int(mask.sum())
    total = int(mask.size)
    if count < max(64, total // 200) or count > int(total * 0.92):
        return None

    ys, xs = np.nonzero(mask)
    bbox = (
        int(xs.min()),
        int(ys.min()),
        int(xs.max()),
        int(ys.max()),
    )
    return rgba[..., :3].copy(), mask, bbox


def _foreground_pixels(image: Image.Image) -> np.ndarray | None:
    data = _foreground_data(image)
    if data is None:
        return None
    rgb, mask, _ = data
    return rgb[mask].astype(np.float32)


def harmonize_source_palette(
    texture_path: Path,
    source_image_path: Path,
    strength: float = 0.85,
) -> bool:
    """Pull TripoSR's baked atlas toward the source asset's actual color palette.

    TripoSR is excellent for fast geometry smoke tests on Apple Silicon, but its
    inferred colors can be heavily desaturated. Matching masked RGB statistics
    keeps the learned spatial texture/detail while restoring the reference's
    dominant material palette. Hidden/back surfaces stay learned rather than
    receiving a mirrored front-view projection.
    """

    source_pixels = _foreground_pixels(Image.open(source_image_path))
    if source_pixels is None:
        print(
            "source palette harmonization skipped: foreground could not be isolated",
            flush=True,
        )
        return False

    texture_image = Image.open(texture_path).convert("RGBA")
    texture = np.asarray(texture_image, dtype=np.uint8).copy()
    atlas_mask = texture[..., 3] > 8
    if int(atlas_mask.sum()) < 64:
        print(
            "source palette harmonization skipped: baked atlas has no usable texels",
            flush=True,
        )
        return False

    rgb = texture[..., :3].astype(np.float32)
    atlas_pixels = rgb[atlas_mask]
    adjusted = rgb.copy()
    for channel in range(3):
        source_mean = float(source_pixels[:, channel].mean())
        source_std = float(source_pixels[:, channel].std())
        atlas_mean = float(atlas_pixels[:, channel].mean())
        atlas_std = float(atlas_pixels[:, channel].std())
        scale = np.clip(source_std / max(atlas_std, 1e-6), 0.55, 1.80)
        adjusted[..., channel] = (
            (adjusted[..., channel] - atlas_mean) * scale + source_mean
        )

    strength = float(np.clip(strength, 0.0, 1.0))
    recolored = rgb * (1.0 - strength) + adjusted * strength
    texture[..., :3] = np.clip(recolored, 0.0, 255.0).astype(np.uint8)
    Image.fromarray(texture).save(texture_path)

    before = atlas_pixels.mean(axis=0)
    after = texture[..., :3][atlas_mask].astype(np.float32).mean(axis=0)
    source_mean = source_pixels.mean(axis=0)
    print(
        "source palette harmonized: "
        f"source={source_mean.round(1).tolist()} "
        f"before={before.round(1).tolist()} "
        f"after={after.round(1).tolist()}",
        flush=True,
    )
    return True


def single_mesh(path: Path) -> trimesh.Trimesh:
    loaded = trimesh.load(str(path), process=False, maintain_order=True)
    if isinstance(loaded, trimesh.Scene):
        geometry = list(loaded.geometry.values())
        if len(geometry) != 1:
            raise RuntimeError(
                f"expected one TripoSR mesh in {path}, found {len(geometry)}"
            )
        loaded = geometry[0]
    if not isinstance(loaded, trimesh.Trimesh):
        raise RuntimeError(f"TripoSR texture output was not a mesh: {path}")
    return loaded


def _projection_uv(
    points: np.ndarray,
    mapping: dict[str, object],
    source_shape: tuple[int, int],
) -> np.ndarray:
    points = np.asarray(points, dtype=np.float64)
    h_axis = int(mapping["hAxis"])
    v_axis = int(mapping["vAxis"])
    mins = np.asarray(mapping["mins"], dtype=np.float64)
    maxs = np.asarray(mapping["maxs"], dtype=np.float64)
    h_flip = bool(mapping["hFlip"])
    v_flip = bool(mapping["vFlip"])
    x0, y0, x1, y1 = tuple(int(value) for value in mapping["bbox"])

    h_span = max(float(maxs[h_axis] - mins[h_axis]), 1e-8)
    v_span = max(float(maxs[v_axis] - mins[v_axis]), 1e-8)
    u = (points[:, h_axis] - mins[h_axis]) / h_span
    v = (points[:, v_axis] - mins[v_axis]) / v_span
    if h_flip:
        u = 1.0 - u
    if v_flip:
        v = 1.0 - v

    height, width = source_shape
    px = x0 + np.clip(u, 0.0, 1.0) * (x1 - x0)
    py = y0 + np.clip(v, 0.0, 1.0) * (y1 - y0)
    uv = np.column_stack(
        [
            px / max(width - 1, 1),
            1.0 - py / max(height - 1, 1),
        ]
    )
    return np.clip(uv, 0.0, 1.0)


def infer_source_projection(
    mesh: trimesh.Trimesh,
    source_mask: np.ndarray,
    bbox: tuple[int, int, int, int],
) -> dict[str, object] | None:
    """Infer which mesh axes best align to the isolated source silhouette.

    TripoSR's generated orientation is deterministic enough for a silhouette
    search but not something the factory should hard-code. Try every ordered
    pair of mesh axes and both flips, then accept projection only when most mesh
    vertices land on foreground pixels. Low-confidence assets retain the learned
    TripoSR texture instead.
    """

    vertices = np.asarray(mesh.vertices, dtype=np.float64)
    if len(vertices) == 0:
        return None

    mins = vertices.min(axis=0)
    maxs = vertices.max(axis=0)
    spans = maxs - mins
    if int(np.count_nonzero(spans > 1e-8)) < 2:
        return None

    x0, y0, x1, y1 = bbox
    height, width = source_mask.shape
    best: dict[str, object] | None = None

    for h_axis in range(3):
        if spans[h_axis] <= 1e-8:
            continue
        for v_axis in range(3):
            if h_axis == v_axis or spans[v_axis] <= 1e-8:
                continue
            for h_flip in (False, True):
                for v_flip in (False, True):
                    u = (vertices[:, h_axis] - mins[h_axis]) / spans[h_axis]
                    v = (vertices[:, v_axis] - mins[v_axis]) / spans[v_axis]
                    if h_flip:
                        u = 1.0 - u
                    if v_flip:
                        v = 1.0 - v

                    px = np.rint(x0 + u * (x1 - x0)).astype(np.int64)
                    py = np.rint(y0 + v * (y1 - y0)).astype(np.int64)
                    inside = (
                        (px >= 0)
                        & (px < width)
                        & (py >= 0)
                        & (py < height)
                    )
                    hits = np.zeros(len(vertices), dtype=bool)
                    hits[inside] = source_mask[py[inside], px[inside]]
                    score = float(hits.mean())

                    if best is None or score > float(best["score"]):
                        depth_axis = next(
                            axis
                            for axis in range(3)
                            if axis not in (h_axis, v_axis)
                        )
                        best = {
                            "score": score,
                            "hAxis": h_axis,
                            "vAxis": v_axis,
                            "depthAxis": depth_axis,
                            "hFlip": h_flip,
                            "vFlip": v_flip,
                            "mins": mins,
                            "maxs": maxs,
                            "bbox": bbox,
                        }

    if best is None or float(best["score"]) < PROJECTION_SCORE_THRESHOLD:
        return None
    return best


def _source_projection_face_mask(
    mesh: trimesh.Trimesh,
    mapping: dict[str, object],
    source_mask: np.ndarray,
) -> np.ndarray:
    depth_axis = int(mapping["depthAxis"])
    normals = np.asarray(mesh.face_normals, dtype=np.float64)
    centroids = np.asarray(mesh.triangles_center, dtype=np.float64)
    candidate = np.abs(normals[:, depth_axis]) >= PROJECTION_NORMAL_THRESHOLD

    centroid_uv = _projection_uv(centroids, mapping, source_mask.shape)
    height, width = source_mask.shape
    px = np.rint(centroid_uv[:, 0] * max(width - 1, 1)).astype(np.int64)
    py = np.rint((1.0 - centroid_uv[:, 1]) * max(height - 1, 1)).astype(np.int64)
    inside = (
        (px >= 0)
        & (px < width)
        & (py >= 0)
        & (py < height)
    )
    foreground = np.zeros(len(centroids), dtype=bool)
    foreground[inside] = source_mask[py[inside], px[inside]]
    return candidate & foreground


def _export_source_projected_glb(
    mesh: trimesh.Trimesh,
    source_image_path: Path,
    learned_texture_path: Path,
    output: Path,
) -> bool:
    source_image = Image.open(source_image_path).convert("RGB")
    source_data = _foreground_data(source_image)
    if source_data is None:
        print(
            "source projection skipped: foreground could not be isolated",
            flush=True,
        )
        return False

    _, source_mask, bbox = source_data
    mapping = infer_source_projection(mesh, source_mask, bbox)
    if mapping is None:
        print(
            "source projection skipped: silhouette alignment confidence below "
            f"{PROJECTION_SCORE_THRESHOLD:.2f}",
            flush=True,
        )
        return False

    projected_faces = _source_projection_face_mask(mesh, mapping, source_mask)
    projected_indices = np.flatnonzero(projected_faces)
    side_indices = np.flatnonzero(~projected_faces)
    if len(projected_indices) == 0 or len(side_indices) == 0:
        print(
            "source projection skipped: projection did not produce both surface regions",
            flush=True,
        )
        return False

    projected = mesh.submesh([projected_indices], append=True, repair=False)
    sides = mesh.submesh([side_indices], append=True, repair=False)
    if not isinstance(projected, trimesh.Trimesh) or not isinstance(
        sides, trimesh.Trimesh
    ):
        print("source projection skipped: mesh split failed", flush=True)
        return False

    projected_uv = _projection_uv(
        np.asarray(projected.vertices),
        mapping,
        source_mask.shape,
    )
    projected_material = trimesh.visual.material.PBRMaterial(
        name="SourceProjection",
        baseColorTexture=source_image,
        metallicFactor=0.0,
        roughnessFactor=0.58,
    )
    projected.visual = trimesh.visual.texture.TextureVisuals(
        uv=projected_uv,
        material=projected_material,
    )

    side_uv = getattr(sides.visual, "uv", None)
    if side_uv is None or len(side_uv) != len(sides.vertices):
        print(
            "source projection skipped: generated side surface lost baked UVs",
            flush=True,
        )
        return False

    learned_image = Image.open(learned_texture_path).convert("RGB")
    side_material = trimesh.visual.material.PBRMaterial(
        name="TripoSR_Baked_Sides",
        baseColorTexture=learned_image,
        metallicFactor=0.0,
        roughnessFactor=0.58,
    )
    sides.visual = trimesh.visual.texture.TextureVisuals(
        uv=side_uv,
        material=side_material,
    )

    scene = trimesh.Scene()
    scene.add_geometry(projected, geom_name="SourceProjectedSurface")
    scene.add_geometry(sides, geom_name="GeneratedSides")
    payload = scene.export(file_type="glb")
    if not isinstance(payload, (bytes, bytearray)) or not payload:
        print("source projection skipped: failed to package split GLB", flush=True)
        return False
    output.write_bytes(payload)

    print(
        "source projection applied: "
        f"score={float(mapping['score']):.3f} "
        f"axes=(h={int(mapping['hAxis'])},v={int(mapping['vAxis'])},"
        f"d={int(mapping['depthAxis'])}) "
        f"flips=({int(bool(mapping['hFlip']))},{int(bool(mapping['vFlip']))}) "
        f"faces=({len(projected_indices)} projected/{len(side_indices)} generated)",
        flush=True,
    )
    return True


def export_textured_glb(
    obj_path: Path,
    texture_path: Path,
    source_image_path: Path,
    output: Path,
) -> None:
    mesh = single_mesh(obj_path)
    if _export_source_projected_glb(
        mesh,
        source_image_path,
        texture_path,
        output,
    ):
        return

    uv = getattr(mesh.visual, "uv", None)
    if uv is None or len(uv) != len(mesh.vertices):
        raise RuntimeError("TripoSR baked mesh did not contain one UV coordinate per vertex")

    image = Image.open(texture_path).convert("RGB")
    material = trimesh.visual.material.PBRMaterial(
        name="TripoSR_Baked",
        baseColorTexture=image,
        metallicFactor=0.0,
        roughnessFactor=0.58,
    )
    mesh.visual = trimesh.visual.texture.TextureVisuals(uv=uv, material=material)
    payload = trimesh.Scene(mesh).export(file_type="glb")
    if not isinstance(payload, (bytes, bytearray)) or not payload:
        raise RuntimeError("failed to package TripoSR UV texture into GLB")
    output.write_bytes(payload)


def main() -> int:
    args = parse_args()
    source = Path(args.source).resolve()
    weights = Path(args.weights).resolve()
    front = Path(args.front).resolve()
    output = Path(args.output).resolve()
    work = output.parent / f".{output.stem}.triposr"
    bake_texture = not args.no_bake_texture
    produced = work / "0" / ("mesh.obj" if bake_texture else "mesh.glb")
    produced_texture = work / "0" / "texture.png"

    shutil.rmtree(work, ignore_errors=True)
    (work / "0").mkdir(parents=True, exist_ok=True)
    output.parent.mkdir(parents=True, exist_ok=True)

    device = "mps" if args.device == "auto" else args.device
    if bake_texture and device.startswith("mps"):
        patch_mps_texture_baker(source)

    command = [
        __import__("sys").executable,
        str(source / "run.py"),
        str(front),
        "--device",
        device,
        "--pretrained-model-name-or-path",
        str(weights),
        "--chunk-size",
        str(args.chunk_size),
        "--mc-resolution",
        str(args.mc_resolution),
        "--output-dir",
        str(work),
        "--model-save-format",
        "obj" if bake_texture else "glb",
    ]
    if bake_texture:
        command.extend(
            [
                "--bake-texture",
                "--texture-resolution",
                str(args.texture_resolution),
            ]
        )
    if not args.remove_background:
        # CI/preprocessed inputs already use TripoSR's expected neutral-gray
        # background, so avoid a second segmentation-model dependency.
        command.append("--no-remove-bg")

    print("+", " ".join(command), flush=True)
    completed = subprocess.run(command, cwd=str(source), check=False)
    if completed.returncode != 0:
        return completed.returncode
    if not produced.is_file() or produced.stat().st_size == 0:
        raise RuntimeError(f"TripoSR did not produce {produced}")

    if bake_texture:
        if not produced_texture.is_file() or produced_texture.stat().st_size == 0:
            raise RuntimeError(f"TripoSR did not produce {produced_texture}")
        harmonize_source_palette(produced_texture, front)
        export_textured_glb(produced, produced_texture, front, output)
        sidecar = output.with_name(f"{output.stem}.basecolor.png")
        shutil.copy2(produced_texture, sidecar)
        print(sidecar, flush=True)
    else:
        shutil.move(str(produced), str(output))

    shutil.rmtree(work, ignore_errors=True)
    print(output, flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
