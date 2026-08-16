#!/usr/bin/env python3
from __future__ import annotations

import argparse
from pathlib import Path
import sys

SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

from blender_common import (
    apply_mesh_transforms,
    clear_scene,
    export_fbx,
    generated_meshes,
    import_glb,
)


def parse_args() -> argparse.Namespace:
    argv = sys.argv
    if "--" not in argv:
        raise SystemExit("Expected Blender arguments after '--'")
    argv = argv[argv.index("--") + 1 :]

    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--part-kind", choices=("weapon", "accessory"), required=True)
    return parser.parse_args(argv)


def main() -> int:
    args = parse_args()
    output = Path(args.output).resolve()
    if output.suffix.lower() != ".fbx":
        raise RuntimeError(f"{args.part_kind} output must use .fbx for Unity import")

    clear_scene()
    meshes = generated_meshes(
        import_glb(Path(args.input).resolve()),
        args.part_kind,
    )
    apply_mesh_transforms(meshes)
    export_fbx(output, meshes)
    print(output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
