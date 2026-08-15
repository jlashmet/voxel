#!/usr/bin/env python3
from __future__ import annotations

import argparse
from pathlib import Path
import shutil
import subprocess

import numpy as np
from PIL import Image
import trimesh


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


def _foreground_pixels(image: Image.Image) -> np.ndarray | None:
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
        # Source-guided palette transfer is only safe when the input has a
        # deliberately simple background (the CI/automation path uses neutral gray).
        if float(np.percentile(border_delta, 90)) > 18.0:
            return None
        mask = np.max(np.abs(rgb - background), axis=2) > 12.0

    count = int(mask.sum())
    total = int(mask.size)
    if count < max(64, total // 200) or count > int(total * 0.92):
        return None
    return rgba[..., :3][mask].astype(np.float32)


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
        print("source palette harmonization skipped: foreground could not be isolated", flush=True)
        return False

    texture_image = Image.open(texture_path).convert("RGBA")
    texture = np.asarray(texture_image, dtype=np.uint8).copy()
    atlas_mask = texture[..., 3] > 8
    if int(atlas_mask.sum()) < 64:
        print("source palette harmonization skipped: baked atlas has no usable texels", flush=True)
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
        adjusted[..., channel] = (adjusted[..., channel] - atlas_mean) * scale + source_mean

    strength = float(np.clip(strength, 0.0, 1.0))
    recolored = rgb * (1.0 - strength) + adjusted * strength
    texture[..., :3] = np.clip(recolored, 0.0, 255.0).astype(np.uint8)
    Image.fromarray(texture, mode="RGBA").save(texture_path)

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


def export_textured_glb(obj_path: Path, texture_path: Path, output: Path) -> None:
    mesh = single_mesh(obj_path)
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
        export_textured_glb(produced, produced_texture, output)
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
