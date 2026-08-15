#!/usr/bin/env python3
from __future__ import annotations

import argparse
from pathlib import Path


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Local Hunyuan3D-2mv mesh generator")
    parser.add_argument("--front", required=True)
    parser.add_argument("--back")
    parser.add_argument("--left")
    parser.add_argument("--right")
    parser.add_argument("--output", required=True)
    parser.add_argument("--model", default="tencent/Hunyuan3D-2mv")
    parser.add_argument("--subfolder", default="hunyuan3d-dit-v2-mv")
    parser.add_argument("--device", default="auto")
    parser.add_argument("--seed", type=int, default=12345)
    parser.add_argument("--steps", type=int, default=50)
    parser.add_argument("--octree-resolution", type=int, default=380)
    parser.add_argument("--num-chunks", type=int, default=20000)
    parser.add_argument("--remove-background", action="store_true")
    return parser.parse_args()


def resolve_device(torch, requested: str) -> str:
    if requested != "auto":
        return requested
    if torch.cuda.is_available():
        return "cuda"
    if hasattr(torch.backends, "mps") and torch.backends.mps.is_available():
        return "mps"
    return "cpu"


def main() -> int:
    args = parse_args()

    import torch
    from PIL import Image
    from hy3dgen.rembg import BackgroundRemover
    from hy3dgen.shapegen import Hunyuan3DDiTFlowMatchingPipeline

    device = resolve_device(torch, args.device)
    remover = BackgroundRemover() if args.remove_background else None

    images = {}
    for name in ("front", "back", "left", "right"):
        value = getattr(args, name)
        if not value:
            continue
        image = Image.open(value).convert("RGBA")
        if remover is not None:
            image = remover(image)
        images[name] = image

    pipeline = Hunyuan3DDiTFlowMatchingPipeline.from_pretrained(
        args.model,
        subfolder=args.subfolder,
        variant="fp16",
        device=device,
    )

    mesh = pipeline(
        image=images,
        num_inference_steps=args.steps,
        octree_resolution=args.octree_resolution,
        num_chunks=args.num_chunks,
        generator=torch.manual_seed(args.seed),
        output_type="trimesh",
    )[0]

    output = Path(args.output).resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    mesh.export(str(output))
    print(output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
