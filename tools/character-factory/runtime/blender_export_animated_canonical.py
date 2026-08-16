#!/usr/bin/env python3
from __future__ import annotations

import argparse
from pathlib import Path
import sys

SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

from blender_common import choose_object, clear_scene, export_fbx, import_glb
from blender_gameplay_animation import add_gameplay_animation_set


def parse_args() -> argparse.Namespace:
    argv = sys.argv
    if "--" not in argv:
        raise SystemExit("Expected Blender arguments after '--'")
    argv = argv[argv.index("--") + 1 :]
    parser = argparse.ArgumentParser(
        description="Export the canonical Character Factory body as an animated base character"
    )
    parser.add_argument("--input", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--body-object", default="Body")
    parser.add_argument("--armature-object", default="Armature")
    return parser.parse_args(argv)


def main() -> int:
    args = parse_args()
    source = Path(args.input).resolve()
    output = Path(args.output).resolve()

    clear_scene()
    objects = import_glb(source)
    armature = choose_object(objects, "ARMATURE", args.armature_object, "canonical armature")
    body = choose_object(objects, "MESH", args.body_object, "canonical body")
    add_gameplay_animation_set(armature)
    export_fbx(output, [armature, body], bake_anim=True)
    print(output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
