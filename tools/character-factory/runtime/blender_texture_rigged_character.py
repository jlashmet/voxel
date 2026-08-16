#!/usr/bin/env python3
from __future__ import annotations

import argparse
from pathlib import Path
import sys

SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

import bpy

from blender_common import clear_scene, export_fbx
from blender_multiview_texture import project_multiview_texture


def parse_args() -> argparse.Namespace:
    argv = sys.argv
    if "--" not in argv:
        raise SystemExit("Expected Blender arguments after '--'")
    argv = argv[argv.index("--") + 1 :]
    parser = argparse.ArgumentParser(
        description="Project four turnaround views onto an already rigged character FBX"
    )
    parser.add_argument("--input", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--front", required=True)
    parser.add_argument("--back", required=True)
    parser.add_argument("--left", required=True)
    parser.add_argument("--right", required=True)
    parser.add_argument("--atlas")
    return parser.parse_args(argv)


def main() -> int:
    args = parse_args()
    source = Path(args.input).resolve()
    output = Path(args.output).resolve()
    atlas = (
        Path(args.atlas).resolve()
        if args.atlas
        else output.with_suffix(".multiview_basecolor.png")
    )

    clear_scene()
    bpy.ops.import_scene.fbx(filepath=str(source))
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    if not meshes:
        raise RuntimeError(f"rigged character contains no meshes: {source}")
    if not armatures:
        raise RuntimeError(f"rigged character contains no armature: {source}")

    project_multiview_texture(
        meshes,
        front=Path(args.front),
        back=Path(args.back),
        left=Path(args.left),
        right=Path(args.right),
        output=atlas,
    )

    export_fbx(output, [*armatures, *meshes], bake_anim=True)
    print(output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
