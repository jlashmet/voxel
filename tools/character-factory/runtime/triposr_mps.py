#!/usr/bin/env python3
from __future__ import annotations

import argparse
from pathlib import Path
import shutil
import subprocess


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
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    source = Path(args.source).resolve()
    weights = Path(args.weights).resolve()
    output = Path(args.output).resolve()
    work = output.parent / f".{output.stem}.triposr"
    produced = work / "0" / "mesh.glb"

    shutil.rmtree(work, ignore_errors=True)
    (work / "0").mkdir(parents=True, exist_ok=True)
    output.parent.mkdir(parents=True, exist_ok=True)

    device = "mps" if args.device == "auto" else args.device
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
        "glb",
    ]
    if not args.remove_background:
        # Avoid an additional rembg model download in the smoke path. The smoke
        # fixture only needs to prove image -> mesh -> Blender -> FBX plumbing.
        command.append("--no-remove-bg")

    print("+", " ".join(command), flush=True)
    completed = subprocess.run(command, cwd=str(source), check=False)
    if completed.returncode != 0:
        return completed.returncode
    if not produced.is_file() or produced.stat().st_size == 0:
        raise RuntimeError(f"TripoSR did not produce {produced}")

    shutil.move(str(produced), str(output))
    shutil.rmtree(work, ignore_errors=True)
    print(output, flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
