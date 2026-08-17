#!/usr/bin/env python3
from __future__ import annotations

import argparse
from pathlib import Path
import sys

TOOL_ROOT = Path(__file__).resolve().parent
PROJECT_ROOT = TOOL_ROOT.parents[1]
if str(TOOL_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOL_ROOT))

from api import BuildSpec, CharacterFactoryError, backend_profiles
from runtime import CharacterFactoryRuntime
from runtime.production import ProductionRunner, discover_specs
from runtime.unity_staging import stage_manifest_for_unity


DEFAULT_UNITY_ASSETS_ROOT = Path("Assets/Generated/CharacterFactory")


def _add_unity_assets_root(parser: argparse.ArgumentParser) -> None:
    parser.add_argument(
        "--unity-assets-root",
        type=Path,
        help=(
            "after a successful build, copy the FBX and a portable import descriptor "
            "under this Unity Assets directory"
        ),
    )


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Headless local mesh pipeline for modular characters and equipment"
    )
    subparsers = parser.add_subparsers(dest="command", required=True)

    build = subparsers.add_parser("build", help="run the low-level pipeline for one JSON spec")
    build.add_argument("spec", type=Path)
    build.add_argument("--dry-run", action="store_true")
    _add_unity_assets_root(build)

    batch = subparsers.add_parser("batch", help="build all JSON specs in a directory")
    batch.add_argument("directory", type=Path)
    batch.add_argument("--dry-run", action="store_true")
    _add_unity_assets_root(batch)

    produce = subparsers.add_parser(
        "produce",
        help=(
            "run the standard production lifecycle for one image-driven asset: "
            "build, type-specific appearance/verification, proof render, and optional Unity staging"
        ),
    )
    produce.add_argument("spec", type=Path)
    produce.add_argument("--dry-run", action="store_true")
    _add_unity_assets_root(produce)

    produce_batch = subparsers.add_parser(
        "produce-batch",
        help="recursively discover and produce character/clothing/weapon/accessory specs",
    )
    produce_batch.add_argument("directory", type=Path)
    produce_batch.add_argument("--dry-run", action="store_true")
    produce_batch.add_argument(
        "--no-recursive",
        action="store_true",
        help="only inspect JSON specs directly inside the supplied directory",
    )
    _add_unity_assets_root(produce_batch)

    subparsers.add_parser(
        "profiles",
        help="list named generator backend profiles and their pinned source revisions",
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


def produce_one(
    runner: ProductionRunner,
    path: Path,
    dry_run: bool,
    unity_assets_root: Path | None,
) -> None:
    spec = BuildSpec.load(path, validate_paths=not dry_run)
    manifest = runner.produce(spec, dry_run=dry_run)
    print(f"produce {spec.asset_type.value}/{spec.asset_id}: {manifest}")
    if unity_assets_root is not None:
        if dry_run:
            raise CharacterFactoryError("--unity-assets-root cannot be used with --dry-run")
        stage_for_unity(manifest, unity_assets_root)


def main() -> int:
    args = parse_args()
    runtime = CharacterFactoryRuntime(TOOL_ROOT)
    production = ProductionRunner(TOOL_ROOT, runtime)

    try:
        if args.command == "profiles":
            for profile in backend_profiles():
                print(
                    f"{profile.name}\tbackend={profile.backend}\t"
                    f"revision={profile.source_revision}\tbootstrap={profile.bootstrap_script}"
                )
            return 0

        if args.command == "build":
            build_one(runtime, args.spec, args.dry_run, args.unity_assets_root)
            return 0

        if args.command == "produce":
            produce_one(production, args.spec, args.dry_run, args.unity_assets_root)
            return 0

        if args.command == "stage-unity":
            stage_for_unity(args.manifest, args.assets_root)
            return 0

        if args.command == "produce-batch":
            specs = discover_specs(args.directory, recursive=not args.no_recursive)
            if not specs:
                raise CharacterFactoryError(
                    f"No Character Factory production specs found in {args.directory}"
                )
            for spec_path in specs:
                produce_one(
                    production,
                    spec_path,
                    args.dry_run,
                    args.unity_assets_root,
                )
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
