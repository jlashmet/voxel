#!/usr/bin/env python3
from __future__ import annotations

import argparse
from pathlib import Path
import sys

TOOL_ROOT = Path(__file__).resolve().parent
PROJECT_ROOT = TOOL_ROOT.parents[1]
if str(TOOL_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOL_ROOT))

from api import BuildSpec, CharacterFactoryError
from runtime import CharacterFactoryRuntime
from runtime.unity_staging import stage_manifest_for_unity


DEFAULT_UNITY_ASSETS_ROOT = Path("Assets/Generated/CharacterFactory")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Headless local mesh pipeline for modular characters and equipment"
    )
    subparsers = parser.add_subparsers(dest="command", required=True)

    build = subparsers.add_parser("build", help="build one JSON spec")
    build.add_argument("spec", type=Path)
    build.add_argument("--dry-run", action="store_true")
    build.add_argument(
        "--unity-assets-root",
        type=Path,
        help=(
            "after a successful build, copy the FBX and a portable import descriptor "
            "under this Unity Assets directory"
        ),
    )

    batch = subparsers.add_parser("batch", help="build all JSON specs in a directory")
    batch.add_argument("directory", type=Path)
    batch.add_argument("--dry-run", action="store_true")
    batch.add_argument(
        "--unity-assets-root",
        type=Path,
        help="stage every successful batch result into this Unity Assets directory",
    )

    stage = subparsers.add_parser(
        "stage-unity",
        help="stage an existing completed manifest for automatic Unity import",
    )
    stage.add_argument("manifest", type=Path)
    stage.add_argument(
        "--assets-root",
        type=Path,
        default=DEFAULT_UNITY_ASSETS_ROOT,
    )
    return parser.parse_args()


def stage_for_unity(manifest: Path, assets_root: Path) -> None:
    result = stage_manifest_for_unity(
        manifest,
        assets_root,
        project_root=PROJECT_ROOT,
    )
    print(
        f"unity-stage: {result.asset_id} -> {result.descriptor.relative_to(PROJECT_ROOT)}"
    )


def build_one(
    runtime: CharacterFactoryRuntime,
    path: Path,
    dry_run: bool,
    unity_assets_root: Path | None,
) -> None:
    spec = BuildSpec.load(path, validate_paths=not dry_run)
    manifest = runtime.build(spec, dry_run=dry_run)
    print(f"{spec.asset_id}: {manifest}")
    if unity_assets_root is not None:
        if dry_run:
            raise CharacterFactoryError("--unity-assets-root cannot be used with --dry-run")
        stage_for_unity(manifest, unity_assets_root)


def main() -> int:
    args = parse_args()
    runtime = CharacterFactoryRuntime(TOOL_ROOT)

    try:
        if args.command == "build":
            build_one(runtime, args.spec, args.dry_run, args.unity_assets_root)
            return 0

        if args.command == "stage-unity":
            stage_for_unity(args.manifest, args.assets_root)
            return 0

        specs = sorted(args.directory.glob("*.json"))
        if not specs:
            raise CharacterFactoryError(f"No JSON specs found in {args.directory}")
        for spec_path in specs:
            build_one(
                runtime,
                spec_path,
                args.dry_run,
                args.unity_assets_root,
            )
        return 0
    except (CharacterFactoryError, OSError, ValueError) as exc:
        print(f"character-factory: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
