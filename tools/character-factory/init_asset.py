#!/usr/bin/env python3
from __future__ import annotations

import argparse
from pathlib import Path
import sys

TOOL_ROOT = Path(__file__).resolve().parent
PROJECT_ROOT = TOOL_ROOT.parents[1]
if str(TOOL_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOL_ROOT))

from api import AssetType, CharacterFactoryError, backend_profile, rig_profile
from api.appearance_profiles import (
    AppearanceProfileError,
    resolve_appearance_strategy,
)
from runtime.scaffold import DEFAULT_RIG_PROFILE, scaffold_asset


DEFAULT_LIBRARY_ROOT = TOOL_ROOT / "production-assets"
DEFAULT_BLENDER = "/Applications/Blender.app/Contents/MacOS/Blender"


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Create a convention-driven Character Factory production asset"
    )
    parser.add_argument(
        "asset_type",
        choices=[item.value for item in AssetType],
    )
    parser.add_argument("asset_id")
    parser.add_argument(
        "--library-root",
        type=Path,
        default=DEFAULT_LIBRARY_ROOT,
    )
    parser.add_argument(
        "--profile",
        default="hunyuan-quality-macos",
        help="named generator backend profile",
    )
    parser.add_argument(
        "--rig-profile",
        default=DEFAULT_RIG_PROFILE,
        help=(
            "named character/clothing canonical-rig profile; ignored when the legacy "
            "--canonical-body override is supplied"
        ),
    )
    parser.add_argument(
        "--appearance-strategy",
        help=(
            "override the asset-type default appearance strategy; defaults are "
            "character-multiview, garment-multiview, and preserve-generator for rigid parts"
        ),
    )
    parser.add_argument("--tag", action="append", default=[])
    parser.add_argument("--blender", default=DEFAULT_BLENDER)
    parser.add_argument(
        "--canonical-body",
        type=Path,
        help=(
            "legacy explicit character/clothing canonical GLB override; normally omit "
            "this and let --rig-profile manage the donor"
        ),
    )
    parser.add_argument("--slot")
    parser.add_argument("--socket-bone")
    parser.add_argument(
        "--force",
        action="store_true",
        help="replace asset.json if it exists; reference files are never deleted",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    asset_type = AssetType(args.asset_type)

    try:
        backend_profile(args.profile)
        try:
            resolve_appearance_strategy(
                (
                    None
                    if args.appearance_strategy is None
                    else {"strategy": args.appearance_strategy}
                ),
                asset_type=asset_type.value,
            )
        except AppearanceProfileError as exc:
            raise CharacterFactoryError(str(exc)) from exc

        canonical_body = args.canonical_body
        selected_rig_profile: str | None = args.rig_profile
        if asset_type in {AssetType.CHARACTER, AssetType.CLOTHING}:
            if canonical_body is not None:
                canonical_body = canonical_body.resolve()
                if not canonical_body.is_file():
                    raise CharacterFactoryError(
                        f"canonical body does not exist: {canonical_body}"
                    )
                selected_rig_profile = None
            else:
                try:
                    rig_profile(args.rig_profile)
                except ValueError as exc:
                    raise CharacterFactoryError(str(exc)) from exc

        result = scaffold_asset(
            project_root=PROJECT_ROOT,
            library_root=args.library_root,
            asset_type=asset_type,
            asset_id=args.asset_id,
            backend_profile=args.profile,
            appearance_strategy=args.appearance_strategy,
            tags=args.tag,
            blender=args.blender,
            canonical_body=canonical_body,
            rig_profile=selected_rig_profile,
            slot=args.slot,
            socket_bone_name=args.socket_bone,
            force=args.force,
        )
    except (CharacterFactoryError, OSError, ValueError) as exc:
        print(f"character-factory-init: {exc}", file=sys.stderr)
        return 1

    print(f"asset-directory: {result.directory}")
    print(f"asset-spec: {result.spec}")
    print(f"geometry-references: {result.geometry}")
    if result.appearance is not None:
        print(f"appearance-references: {result.appearance}")
    print(f"detail-references: {result.details}")
    print("next: add canonical front/back/left/right images, then run character_factory.py produce")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
