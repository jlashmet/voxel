#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
from pathlib import Path
import subprocess
import sys

TOOL_ROOT = Path(__file__).resolve().parent
PROJECT_ROOT = TOOL_ROOT.parents[1]
if str(TOOL_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOL_ROOT))

from api import (
    AssetType,
    BuildSpec,
    CharacterFactoryError,
    backend_profile,
    backend_profiles,
    rig_profile,
    rig_profiles,
)
from runtime import CharacterFactoryRuntime
from runtime.catalogue import (
    CHANGE_KINDS,
    catalogue_payload,
    classify_changes,
    load_catalogue,
    load_catalogue_entries,
    select_changed_entries,
    select_entries,
    write_catalogue,
)
from runtime.preprocess import prepare_spec_references
from runtime.production import ProductionRunner
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


def _add_catalogue_filters(parser: argparse.ArgumentParser) -> None:
    parser.add_argument(
        "--type",
        dest="asset_types",
        action="append",
        choices=[item.value for item in AssetType],
        help="select one asset type; may be repeated",
    )
    parser.add_argument(
        "--id",
        dest="asset_ids",
        action="append",
        help="select one asset id; may be repeated",
    )
    parser.add_argument(
        "--tag",
        dest="tags",
        action="append",
        help="require this catalogue tag; repeated tags are ANDed",
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
            "preprocess references, build, type-specific appearance/verification, proof render, "
            "and optional Unity staging"
        ),
    )
    produce.add_argument("spec", type=Path)
    produce.add_argument("--dry-run", action="store_true")
    _add_unity_assets_root(produce)

    produce_batch = subparsers.add_parser(
        "produce-batch",
        help="recursively discover and selectively produce asset-library specs",
    )
    produce_batch.add_argument("directory", type=Path)
    produce_batch.add_argument("--dry-run", action="store_true")
    produce_batch.add_argument(
        "--no-recursive",
        action="store_true",
        help="only inspect JSON specs directly inside the supplied directory",
    )
    produce_batch.add_argument(
        "--changed-from",
        type=Path,
        help=(
            "produce only assets whose spec or reference fingerprints differ from "
            "this previous Character Factory catalogue"
        ),
    )
    produce_batch.add_argument(
        "--change-kind",
        action="append",
        choices=sorted(CHANGE_KINDS),
        help=(
            "with --changed-from, select only this input-change class "
            "(new/spec/geometry/appearance/details); may be repeated"
        ),
    )
    produce_batch.add_argument(
        "--catalogue-output",
        type=Path,
        help=(
            "after a successful batch (including a no-change batch), atomically write "
            "the current catalogue snapshot here for the next incremental run"
        ),
    )
    _add_catalogue_filters(produce_batch)
    _add_unity_assets_root(produce_batch)

    catalogue = subparsers.add_parser(
        "catalogue",
        help="index a production asset library with type/profile/reference fingerprints",
    )
    catalogue.add_argument("directory", type=Path)
    catalogue.add_argument("--output", type=Path)
    catalogue.add_argument("--no-recursive", action="store_true")
    catalogue.add_argument(
        "--validate-paths",
        action="store_true",
        help="require all referenced source files and rig inputs to exist while indexing",
    )

    subparsers.add_parser(
        "profiles",
        help="list named generator backend profiles and their pinned source revisions",
    )
    subparsers.add_parser(
        "rig-profiles",
        help="list named canonical rig profiles and their code-keyed donor revisions",
    )

    bootstrap = subparsers.add_parser(
        "bootstrap-profile",
        help="materialize one named generator backend profile and print its managed Python path",
    )
    bootstrap.add_argument("name")

    bootstrap_rig = subparsers.add_parser(
        "bootstrap-rig-profile",
        help="materialize one named canonical rig profile and print its donor GLB path",
    )
    bootstrap_rig.add_argument("name")

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


def bootstrap_backend_profile(name: str) -> None:
    profile = backend_profile(name)
    resolved = profile.resolved_defaults(TOOL_ROOT)
    script = Path(str(resolved["bootstrapScript"]))
    if not script.is_file():
        raise CharacterFactoryError(f"backend profile bootstrap script does not exist: {script}")

    command = ["bash", str(script)]
    print("+", " ".join(command), flush=True)
    completed = subprocess.run(command, cwd=PROJECT_ROOT, check=False)
    if completed.returncode != 0:
        raise CharacterFactoryError(
            f"backend profile bootstrap failed with exit code {completed.returncode}: {profile.name}"
        )

    python_path = Path(str(resolved["python"]))
    if not python_path.is_file():
        raise CharacterFactoryError(
            f"backend profile bootstrap did not create Python runtime: {python_path}"
        )
    print(python_path)


def bootstrap_canonical_rig_profile(name: str) -> None:
    profile = rig_profile(name)
    resolved = profile.resolved_defaults(TOOL_ROOT, asset_type="character")
    script = Path(str(resolved["bootstrapScript"]))
    canonical = Path(str(resolved["canonicalBody"]))
    blender = str(resolved["blender"])
    if not script.is_file():
        raise CharacterFactoryError(f"rig profile bootstrap script does not exist: {script}")

    command = [sys.executable, str(script), "--blender", blender]
    print("+", " ".join(command), flush=True)
    completed = subprocess.run(command, cwd=PROJECT_ROOT, check=False)
    if completed.returncode != 0:
        raise CharacterFactoryError(
            f"rig profile bootstrap failed with exit code {completed.returncode}: {profile.name}"
        )
    if not canonical.is_file() or canonical.stat().st_size <= 0:
        raise CharacterFactoryError(
            f"rig profile bootstrap did not create canonical donor: {canonical}"
        )
    print(canonical)


def _load_prepared_spec(path: Path, *, dry_run: bool) -> BuildSpec:
    prepare_spec_references(path, TOOL_ROOT, dry_run=dry_run)
    return BuildSpec.load(path, validate_paths=not dry_run)


def build_one(
    runtime: CharacterFactoryRuntime,
    path: Path,
    dry_run: bool,
    unity_assets_root: Path | None,
) -> None:
    spec = _load_prepared_spec(path, dry_run=dry_run)
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
    spec = _load_prepared_spec(path, dry_run=dry_run)
    manifest = runner.produce(spec, dry_run=dry_run)
    print(f"produce {spec.asset_type.value}/{spec.asset_id}: {manifest}")
    if unity_assets_root is not None:
        if dry_run:
            raise CharacterFactoryError("--unity-assets-root cannot be used with --dry-run")
        stage_for_unity(manifest, unity_assets_root)


def write_batch_catalogue(args: argparse.Namespace) -> None:
    if args.catalogue_output is None:
        return
    output = write_catalogue(
        args.directory,
        args.catalogue_output,
        recursive=not args.no_recursive,
        validate_paths=False,
    )
    print(f"catalogue-snapshot: {output}", flush=True)


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

        if args.command == "rig-profiles":
            for profile in rig_profiles():
                resolved = profile.resolved_defaults(TOOL_ROOT, asset_type="character")
                print(
                    f"{profile.name}\trevision={resolved['sourceRevision']}\t"
                    f"canonical={resolved['canonicalBody']}\tbootstrap={profile.bootstrap_script}"
                )
            return 0

        if args.command == "bootstrap-profile":
            bootstrap_backend_profile(args.name)
            return 0

        if args.command == "bootstrap-rig-profile":
            bootstrap_canonical_rig_profile(args.name)
            return 0

        if args.command == "catalogue":
            if args.output is not None:
                output = write_catalogue(
                    args.directory,
                    args.output,
                    recursive=not args.no_recursive,
                    validate_paths=args.validate_paths,
                )
                print(output)
            else:
                payload = catalogue_payload(
                    args.directory,
                    recursive=not args.no_recursive,
                    validate_paths=args.validate_paths,
                )
                print(json.dumps(payload, indent=2))
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
            if args.change_kind and args.changed_from is None:
                raise CharacterFactoryError("--change-kind requires --changed-from")

            entries = load_catalogue_entries(
                args.directory,
                recursive=not args.no_recursive,
                validate_paths=False,
            )

            if args.changed_from is not None:
                previous = load_catalogue(args.changed_from)
                changes, removed = classify_changes(entries, previous)
                for change in changes:
                    print(
                        f"catalogue-change: {change.key} "
                        f"kinds={','.join(sorted(change.kinds))}",
                        flush=True,
                    )
                for key in removed:
                    print(f"catalogue-removed: {key}", flush=True)
                entries = select_changed_entries(
                    changes,
                    change_kinds=(None if not args.change_kind else set(args.change_kind)),
                )

            asset_types = (
                None
                if not args.asset_types
                else {AssetType(value) for value in args.asset_types}
            )
            asset_ids = None if not args.asset_ids else set(args.asset_ids)
            tags = None if not args.tags else set(args.tags)
            selected = select_entries(
                entries,
                asset_types=asset_types,
                asset_ids=asset_ids,
                tags=tags,
            )
            if not selected:
                if args.changed_from is not None:
                    print("produce-batch: no changed assets selected", flush=True)
                    write_batch_catalogue(args)
                    return 0
                filters = []
                if asset_types:
                    filters.append("types=" + ",".join(sorted(item.value for item in asset_types)))
                if asset_ids:
                    filters.append("ids=" + ",".join(sorted(asset_ids)))
                if tags:
                    filters.append("tags=" + ",".join(sorted(tag.lower() for tag in tags)))
                suffix = " (" + " ".join(filters) + ")" if filters else ""
                raise CharacterFactoryError(
                    f"No Character Factory production specs selected in {args.directory}{suffix}"
                )
            for entry in selected:
                produce_one(
                    production,
                    entry.spec_path,
                    args.dry_run,
                    args.unity_assets_root,
                )
            write_batch_catalogue(args)
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
