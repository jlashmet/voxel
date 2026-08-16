#!/usr/bin/env python3
from __future__ import annotations

import argparse
from pathlib import Path
import sys

SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

from blender_alignment import align_generated_to_donor
from blender_common import (
    choose_object,
    clear_scene,
    export_fbx,
    generated_meshes,
    import_glb,
    transfer_weights,
)
from blender_gameplay_animation import add_gameplay_animation_set


def _axis_triplet(value: str) -> tuple[int, int, int]:
    try:
        result = tuple(int(part.strip()) for part in value.split(","))
    except ValueError as exc:
        raise argparse.ArgumentTypeError("axis triplet must contain integers") from exc
    if len(result) != 3 or sorted(result) != [0, 1, 2]:
        raise argparse.ArgumentTypeError(
            "axis mapping must be a permutation such as 2,1,0"
        )
    return result  # type: ignore[return-value]


def _flip_triplet(value: str) -> tuple[bool, bool, bool]:
    parts = [part.strip() for part in value.split(",")]
    if len(parts) != 3 or any(part not in {"0", "1"} for part in parts):
        raise argparse.ArgumentTypeError("axis flips must contain three 0/1 values")
    return tuple(part == "1" for part in parts)  # type: ignore[return-value]


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
    parser.add_argument(
        "--axis-mapping",
        type=_axis_triplet,
        help="Optional target canonical axis -> generated mesh axis mapping, e.g. 2,1,0.",
    )
    parser.add_argument(
        "--axis-flips",
        type=_flip_triplet,
        help="Optional target-axis flip flags paired with --axis-mapping, e.g. 0,0,0.",
    )
    parser.add_argument(
        "--no-auto-align",
        action="store_true",
        help="Skip global axis/scale/center alignment before weight transfer.",
    )
    parser.add_argument(
        "--no-gameplay-animations",
        action="store_true",
        help="Export only the skinned bind pose instead of the built-in preview/gameplay clips.",
    )
    return parser.parse_args(argv)


def main() -> int:
    args = parse_args()
    output = Path(args.output).resolve()
    if output.suffix.lower() != ".fbx":
        raise RuntimeError("character output must use .fbx for Unity import")
    if args.axis_flips is not None and args.axis_mapping is None:
        raise RuntimeError("--axis-flips requires --axis-mapping")

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
    if not args.no_auto_align:
        align_generated_to_donor(
            generated,
            donor_body,
            label="character",
            mapping_override=args.axis_mapping,
            flips_override=args.axis_flips,
        )

    for mesh in generated:
        transfer_weights(
            mesh,
            donor_body,
            armature,
            max_distance=args.max_transfer_distance,
        )

    donor_body.hide_render = True
    has_animations = not args.no_gameplay_animations
    if has_animations:
        add_gameplay_animation_set(armature)

    export_fbx(output, [armature, *generated], bake_anim=has_animations)
    print(output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
