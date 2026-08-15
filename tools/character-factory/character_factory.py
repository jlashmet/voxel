#!/usr/bin/env python3
from __future__ import annotations

import argparse
from pathlib import Path
import sys

TOOL_ROOT = Path(__file__).resolve().parent
if str(TOOL_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOL_ROOT))

from api import BuildSpec, CharacterFactoryError
from runtime import CharacterFactoryRuntime


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Headless local mesh pipeline for modular characters and wearables"
    )
    subparsers = parser.add_subparsers(dest="command", required=True)

    build = subparsers.add_parser("build", help="build one JSON spec")
    build.add_argument("spec", type=Path)
    build.add_argument("--dry-run", action="store_true")

    batch = subparsers.add_parser("batch", help="build all JSON specs in a directory")
    batch.add_argument("directory", type=Path)
    batch.add_argument("--dry-run", action="store_true")
    return parser.parse_args()


def build_one(runtime: CharacterFactoryRuntime, path: Path, dry_run: bool) -> None:
    spec = BuildSpec.load(path, validate_paths=not dry_run)
    manifest = runtime.build(spec, dry_run=dry_run)
    print(f"{spec.asset_id}: {manifest}")


def main() -> int:
    args = parse_args()
    runtime = CharacterFactoryRuntime(TOOL_ROOT)

    try:
        if args.command == "build":
            build_one(runtime, args.spec, args.dry_run)
            return 0

        specs = sorted(args.directory.glob("*.json"))
        if not specs:
            raise CharacterFactoryError(f"No JSON specs found in {args.directory}")
        for spec_path in specs:
            build_one(runtime, spec_path, args.dry_run)
        return 0
    except (CharacterFactoryError, OSError, ValueError) as exc:
        print(f"character-factory: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
