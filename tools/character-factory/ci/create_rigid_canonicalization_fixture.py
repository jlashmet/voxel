#!/usr/bin/env python3
from __future__ import annotations

import argparse
from pathlib import Path
import sys

import bpy


def parse_args() -> argparse.Namespace:
    argv = sys.argv
    if "--" not in argv:
        raise SystemExit("Expected Blender arguments after '--'")
    argv = argv[argv.index("--") + 1 :]
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True)
    return parser.parse_args(argv)


def main() -> int:
    args = parse_args()
    output = Path(args.output).resolve()
    output.parent.mkdir(parents=True, exist_ok=True)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    # Intentionally make X the long axis so the preparation stage must rotate it.
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0.35, -0.2, 0.5))
    mesh = bpy.context.object
    mesh.name = "RigidCanonicalizationFixture"
    mesh.scale = (1.8, 0.18, 0.11)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

    bpy.ops.object.select_all(action="DESELECT")
    mesh.select_set(True)
    bpy.context.view_layer.objects.active = mesh
    bpy.ops.export_scene.gltf(
        filepath=str(output),
        export_format="GLB",
        use_selection=True,
        export_animations=False,
    )

    if not output.is_file() or output.stat().st_size == 0:
        raise RuntimeError(f"failed to create rigid canonicalization fixture: {output}")
    print(output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
