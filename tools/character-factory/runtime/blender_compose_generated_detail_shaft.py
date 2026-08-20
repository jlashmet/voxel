#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
from pathlib import Path
import sys

SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

import bpy

from blender_assemble_ornamented_staff import (
    add_shaft,
    apply_uniform_scale,
    attachment_center,
    bounds as detail_bounds,
    choose_axis,
    world_vertices,
)
from blender_common import apply_mesh_transforms, clear_scene, export_fbx, generated_meshes, import_glb
from blender_prepare_rigid_part import (
    anchor_at_fraction,
    bounds,
    longest_axis,
    normalize_length,
    observed_origin_fraction,
    rotate_long_axis,
)


def parse_args() -> argparse.Namespace:
    argv = sys.argv
    if "--" not in argv:
        raise SystemExit("Expected Blender arguments after '--'")
    argv = argv[argv.index("--") + 1 :]

    parser = argparse.ArgumentParser(
        description=(
            "Compose a rigid asset from a generated detail mesh plus a procedural shaft, "
            "then apply the normal rigid canonicalization contract."
        )
    )
    parser.add_argument("--input-detail", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--part-kind", choices=("weapon", "accessory"), required=True)
    parser.add_argument("--total-length", type=float, required=True)
    parser.add_argument("--detail-length", type=float, required=True)
    parser.add_argument("--shaft-radius", type=float, required=True)
    parser.add_argument("--axis", choices=("auto", "x", "y", "z"), default="auto")
    parser.add_argument("--attachment-side", choices=("min", "max"), default="min")
    parser.add_argument("--overlap", type=float, default=0.025)
    parser.add_argument("--canonical-axis", choices=("x", "y", "z"))
    parser.add_argument("--target-length", type=float)
    parser.add_argument("--anchor-fraction", nargs=3, type=float, metavar=("X", "Y", "Z"))
    return parser.parse_args(argv)


def main() -> int:
    args = parse_args()
    output = Path(args.output).resolve()
    if output.suffix.lower() != ".fbx":
        raise RuntimeError("composed rigid output must use .fbx")
    if args.total_length <= 0.0 or args.detail_length <= 0.0 or args.shaft_radius <= 0.0:
        raise RuntimeError("composition dimensions must be positive")
    if args.total_length <= args.detail_length:
        raise RuntimeError("total length must exceed detail length")
    if args.overlap < 0.0:
        raise RuntimeError("overlap must be >= 0")
    if args.target_length is not None and args.target_length <= 0.0:
        raise RuntimeError("target length must be > 0")
    if args.anchor_fraction is not None and any(
        value < 0.0 or value > 1.0 for value in args.anchor_fraction
    ):
        raise RuntimeError("anchor fraction values must be between 0 and 1")

    clear_scene()
    detail_meshes = generated_meshes(
        import_glb(Path(args.input_detail).resolve()),
        "generated detail",
    )
    for index, mesh in enumerate(detail_meshes):
        mesh.name = "GeneratedDetail" if index == 0 else f"GeneratedDetail_{index}"

    points = world_vertices(detail_meshes)
    detail_axis = choose_axis(points, args.axis)
    lo, hi = detail_bounds(points)
    current_detail_length = hi[detail_axis] - lo[detail_axis]
    if current_detail_length <= 1e-8:
        raise RuntimeError("generated detail has zero extent")

    apply_uniform_scale(detail_meshes, args.detail_length / current_detail_length)
    points = world_vertices(detail_meshes)
    head_bounds = detail_bounds(points)
    attachment = attachment_center(points, detail_axis, args.attachment_side)
    shaft = add_shaft(
        attachment=attachment,
        axis=detail_axis,
        side=args.attachment_side,
        total_length=args.total_length,
        head_bounds=head_bounds,
        shaft_radius=args.shaft_radius,
        overlap=args.overlap,
    )
    shaft.name = "ProceduralShaft"

    meshes = [*detail_meshes, shaft]
    apply_mesh_transforms(meshes)
    source_axis, source_length = longest_axis(meshes)

    if args.canonical_axis is not None:
        rotate_long_axis(meshes, args.canonical_axis)
    if args.target_length is not None:
        normalize_length(meshes, args.target_length)
    if args.anchor_fraction is not None:
        anchor_at_fraction(meshes, tuple(args.anchor_fraction))

    final_axis, final_length = longest_axis(meshes)
    lo, hi = bounds(meshes)
    origin_fraction = observed_origin_fraction(lo, hi)
    contract_path = output.with_suffix(".rigid-contract.json")
    contract = {
        "schemaVersion": 1,
        "partKind": args.part_kind,
        "canonicalAxis": args.canonical_axis,
        "targetLength": args.target_length,
        "anchorFraction": args.anchor_fraction,
        "composition": {
            "strategy": "generated-detail-shaft",
            "totalLength": args.total_length,
            "detailLength": args.detail_length,
            "shaftRadius": args.shaft_radius,
            "axis": args.axis,
            "attachmentSide": args.attachment_side,
            "overlap": args.overlap,
        },
        "source": {
            "longAxis": source_axis,
            "length": source_length,
        },
        "prepared": {
            "longAxis": final_axis,
            "length": final_length,
            "boundsMin": list(lo),
            "boundsMax": list(hi),
            "originFraction": list(origin_fraction),
        },
    }
    contract_path.write_text(json.dumps(contract, indent=2) + "\n", encoding="utf-8")

    export_fbx(output, meshes)
    print(
        "generated-detail-shaft: "
        f"kind={args.part_kind} detailAxis={'xyz'[detail_axis]} "
        f"sourceAxis={source_axis} sourceLength={source_length:.5f} "
        f"finalAxis={final_axis} finalLength={final_length:.5f}",
        flush=True,
    )
    print(contract_path)
    print(output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
