#!/usr/bin/env python3
from __future__ import annotations

import argparse
from pathlib import Path
import sys

SCRIPT_DIR = Path(__file__).resolve().parent
RUNTIME_DIR = SCRIPT_DIR.parent / "runtime"
if str(RUNTIME_DIR) not in sys.path:
    sys.path.insert(0, str(RUNTIME_DIR))

import bpy

from blender_common import clear_scene, export_fbx


def parse_args() -> argparse.Namespace:
    argv = sys.argv
    if "--" not in argv:
        raise SystemExit("Expected Blender arguments after '--'")
    argv = argv[argv.index("--") + 1 :]
    parser = argparse.ArgumentParser(
        description="Create prepared FBX fixtures for Character Factory appearance strategies"
    )
    parser.add_argument("--canonical", required=True)
    parser.add_argument("--character", required=True)
    parser.add_argument("--garment", required=True)
    parser.add_argument("--rigid", required=True)
    return parser.parse_args(argv)


def import_canonical(path: Path):
    clear_scene()
    bpy.ops.import_scene.gltf(filepath=str(path))
    armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    if len(armatures) != 1:
        raise RuntimeError(f"expected one canonical armature, found {len(armatures)}")
    return armatures[0]


def export_skinned_fixture(canonical: Path, mesh_name: str, output: Path) -> None:
    armature = import_canonical(canonical)
    mesh = bpy.data.objects.get(mesh_name)
    if mesh is None or mesh.type != "MESH":
        raise RuntimeError(f"canonical fixture missing mesh {mesh_name!r}")
    export_fbx(output, [armature, mesh], bake_anim=(mesh_name == "Body"))


def export_rigid_fixture(output: Path) -> None:
    clear_scene()
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0.0, 0.0, 0.0))
    mesh = bpy.context.object
    mesh.name = "RigidAppearanceFixture"
    mesh.scale = (0.28, 0.12, 1.1)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    export_fbx(output, [mesh], bake_anim=False)


def main() -> int:
    args = parse_args()
    canonical = Path(args.canonical).resolve()
    character = Path(args.character).resolve()
    garment = Path(args.garment).resolve()
    rigid = Path(args.rigid).resolve()

    export_skinned_fixture(canonical, "Body", character)
    export_skinned_fixture(canonical, "GarmentDonor", garment)
    export_rigid_fixture(rigid)

    for path in (character, garment, rigid):
        if not path.is_file() or path.stat().st_size == 0:
            raise RuntimeError(f"appearance fixture export failed: {path}")
        print(path)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
