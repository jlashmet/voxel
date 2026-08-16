#!/usr/bin/env python3
from __future__ import annotations

import argparse
from pathlib import Path
import sys

import bpy


EXPECTED = ("Idle", "Walk", "Run", "Cast", "StaffAttack")


def parse_args() -> argparse.Namespace:
    argv = sys.argv
    if "--" not in argv:
        raise SystemExit("Expected Blender arguments after '--'")
    argv = argv[argv.index("--") + 1 :]
    parser = argparse.ArgumentParser(
        description="Verify Character Factory FBX contains the built-in gameplay animation set"
    )
    parser.add_argument("--input", required=True)
    return parser.parse_args(argv)


def normalized_action_name(name: str) -> str:
    # Blender's FBX importer may prefix a take with the armature/object name.
    return name.rsplit("|", 1)[-1].rsplit("::", 1)[-1]


def main() -> int:
    args = parse_args()
    source = Path(args.input).resolve()
    if not source.is_file():
        raise RuntimeError(f"FBX does not exist: {source}")

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(source))

    armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    if not armatures:
        raise RuntimeError("Animated character FBX contains no armature")

    actions = list(bpy.data.actions)
    discovered = {normalized_action_name(action.name): action for action in actions}
    missing = [name for name in EXPECTED if name not in discovered]
    if missing:
        raise RuntimeError(
            "Animated character FBX is missing clips: "
            + ", ".join(missing)
            + "; discovered="
            + ", ".join(sorted(discovered))
        )

    for name in EXPECTED:
        action = discovered[name]
        start, end = action.frame_range
        if end - start < 2.0:
            raise RuntimeError(f"Animation clip {name} has no meaningful frame range")
        curves = getattr(action, "fcurves", None)
        if curves is not None and len(curves) == 0:
            raise RuntimeError(f"Animation clip {name} contains no animation curves")
        print(
            f"animation clip: {name} frames={start:.0f}-{end:.0f}",
            flush=True,
        )

    print(
        "verified gameplay animations: " + ", ".join(EXPECTED),
        flush=True,
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
