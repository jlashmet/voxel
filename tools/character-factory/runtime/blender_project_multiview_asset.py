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
from blender_profiled_multiview_texture import (
    PROJECTION_PROFILES,
    project_profiled_multiview_texture,
)


def parse_args() -> argparse.Namespace:
    argv = sys.argv
    if "--" not in argv:
        raise SystemExit("Expected Blender arguments after '--'")
    argv = argv[argv.index("--") + 1 :]
    parser = argparse.ArgumentParser(
        description="Project four reference views onto a prepared Character Factory FBX"
    )
    parser.add_argument("--input", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--front", required=True)
    parser.add_argument("--back", required=True)
    parser.add_argument("--left", required=True)
    parser.add_argument("--right", required=True)
    parser.add_argument("--atlas", required=True)
    parser.add_argument("--profile", choices=PROJECTION_PROFILES, required=True)
    return parser.parse_args(argv)


def main() -> int:
    args = parse_args()
    source = Path(args.input).resolve()
    output = Path(args.output).resolve()
    atlas = Path(args.atlas).resolve()

    clear_scene()
    bpy.ops.import_scene.fbx(filepath=str(source))
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    if not meshes:
        raise RuntimeError(f"prepared asset contains no meshes: {source}")

    if args.profile in {"character", "garment"} and not armatures:
        raise RuntimeError(
            f"{args.profile} multiview appearance requires a prepared skinned FBX"
        )
    if args.profile == "rigid" and armatures:
        raise RuntimeError("rigid multiview appearance does not accept an armature")

    project_profiled_multiview_texture(
        meshes,
        front=Path(args.front),
        back=Path(args.back),
        left=Path(args.left),
        right=Path(args.right),
        output=atlas,
        profile=args.profile,
    )

    export_fbx(
        output,
        [*armatures, *meshes],
        bake_anim=(args.profile == "character"),
    )
    print(output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
