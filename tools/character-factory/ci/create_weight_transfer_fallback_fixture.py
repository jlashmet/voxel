#!/usr/bin/env python3
from __future__ import annotations

import argparse
from pathlib import Path
import sys

import bpy

SCRIPT_DIR = Path(__file__).resolve().parent
RUNTIME_DIR = SCRIPT_DIR.parent / "runtime"
if str(RUNTIME_DIR) not in sys.path:
    sys.path.insert(0, str(RUNTIME_DIR))

from blender_common import (
    choose_object,
    clear_scene,
    export_fbx,
    import_glb,
    transfer_weights,
)


def parse_args() -> argparse.Namespace:
    argv = sys.argv
    if "--" not in argv:
        raise SystemExit("Expected Blender arguments after '--'")
    argv = argv[argv.index("--") + 1 :]
    parser = argparse.ArgumentParser(
        description="Exercise the unweighted-vertex fallback in canonical skin transfer"
    )
    parser.add_argument("--canonical", required=True)
    parser.add_argument("--output", required=True)
    return parser.parse_args(argv)


def main() -> int:
    args = parse_args()
    canonical = Path(args.canonical).resolve()
    output = Path(args.output).resolve()

    clear_scene()
    objects = import_glb(canonical)
    armature = choose_object(objects, "ARMATURE", "Armature", "canonical armature")
    donor = choose_object(objects, "MESH", "Body", "canonical body")

    generated = donor.copy()
    generated.data = donor.data.copy()
    generated.name = "FallbackGeneratedBody"
    generated.parent = None
    for modifier in list(generated.modifiers):
        generated.modifiers.remove(modifier)
    bpy.context.collection.objects.link(generated)

    # Force the generated surface far beyond the bounded confidence radius. The
    # original one-pass implementation assigns no weights in this setup; the
    # fallback must cover the displaced vertices using nearest donor surface data.
    offset = 1.5
    for vertex in generated.data.vertices:
        vertex.co.x += offset

    transfer_weights(
        generated,
        donor,
        armature,
        max_distance=0.01,
    )

    weighted = sum(
        1
        for vertex in generated.data.vertices
        if any(group.weight > 1e-6 for group in vertex.groups)
    )
    total = len(generated.data.vertices)
    if weighted != total:
        raise RuntimeError(
            f"fallback did not fully weight generated fixture: {weighted}/{total}"
        )

    # Restore the visual pose before export; the test is about transfer coverage,
    # not a deliberately displaced character silhouette.
    for vertex in generated.data.vertices:
        vertex.co.x -= offset

    export_fbx(output, [armature, generated], bake_anim=False)
    if not output.is_file() or output.stat().st_size == 0:
        raise RuntimeError(f"fallback fixture export failed: {output}")

    print(f"CI_WEIGHT_TRANSFER_FALLBACK_FIXTURE_OK weighted={weighted}/{total} output={output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
