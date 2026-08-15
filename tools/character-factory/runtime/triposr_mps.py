#!/usr/bin/env python3
from __future__ import annotations

import argparse
from pathlib import Path
import shutil
import subprocess

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
        args.front,
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
