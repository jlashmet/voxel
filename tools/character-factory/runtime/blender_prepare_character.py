#!/usr/bin/env python3
from __future__ import annotations

import argparse
from pathlib import Path
import sys

SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

from blender_common import (
    choose_object,
    clear_scene,
    export_fbx,
    generated_meshes,
    import_glb,
    transfer_weights,
)


def parse_args() -> argparse.Namespace:
    argv = sys.argv
    if "--" not in argv:
        raise SystemExit("Expected Blender arguments after '--'")
    argv = argv[argv.index("--") + 1 :]

    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True)
    parser.add_argument("--canonical", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--body-object")
    parser.add_argument("--armature-object")
    parser.add_argument("--max-transfer-distance", type=float, default=0.25)
    return parser.parse_args(argv)


def main() -> int:
    args = parse_args()
    output = Path(args.output).resolve()
    if output.suffix.lower() != ".fbx":
        raise RuntimeError("character output must use .fbx for Unity import")

    clear_scene()
    canonical_objects = import_glb(Path(args.canonical).resolve())
    armature = choose_object(
        canonical_objects, "ARMATURE", args.armature_object, "canonical armature"
    )
    donor_body = choose_object(
        canonical_objects, "MESH", args.body_object, "canonical body"
    )

    generated = generated_meshes(
        import_glb(Path(args.input).resolve()),
        "character",
    )
    for mesh in generated:
        transfer_weights(
            mesh,
            donor_body,
            armature,
            max_distance=args.max_transfer_distance,
        )

    donor_body.hide_render = True
    export_fbx(output, [armature, *generated])
    print(output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
