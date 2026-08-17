#!/usr/bin/env python3
from __future__ import annotations

import argparse
from pathlib import Path
import subprocess
import sys

TOOL_ROOT = Path(__file__).resolve().parent
PROJECT_ROOT = TOOL_ROOT.parents[1]
if str(TOOL_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOL_ROOT))

from api.rig_profiles import DEFAULT_BLENDER, canonical_donor_state


FIXTURE_SCRIPT = TOOL_ROOT / "ci" / "create_canonical_character_fixture.py"


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Create/reuse the shared Character Factory canonical skeleton donor "
            "containing Body and GarmentDonor meshes"
        )
    )
    parser.add_argument("--blender", default=DEFAULT_BLENDER)
    parser.add_argument(
        "--force",
        action="store_true",
        help="rebuild even if the code-keyed donor already exists",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    blender = Path(args.blender).expanduser().resolve()
    if not blender.is_file():
        print(f"canonical-bootstrap: Blender does not exist: {blender}", file=sys.stderr)
        return 1

    try:
        revision, canonical = canonical_donor_state(TOOL_ROOT)
    except ValueError as exc:
        print(f"canonical-bootstrap: {exc}", file=sys.stderr)
        return 1

    version_dir = canonical.parent
    body_preview = version_dir / "canonical_body.png"
    garment_preview = version_dir / "canonical_garment.png"
    metadata = version_dir / "source.sha256"

    if (
        not args.force
        and canonical.is_file()
        and canonical.stat().st_size > 0
        and metadata.is_file()
        and metadata.read_text(encoding="utf-8").strip() == revision
    ):
        print(canonical)
        return 0

    version_dir.mkdir(parents=True, exist_ok=True)
    command = [
        str(blender),
        "--background",
        "--python-exit-code",
        "1",
        "--python",
        str(FIXTURE_SCRIPT),
        "--",
        "--canonical",
        str(canonical),
        "--input",
        str(body_preview),
        "--garment-input",
        str(garment_preview),
    ]
    print("+ " + " ".join(command), file=sys.stderr, flush=True)
    completed = subprocess.run(
        command,
        cwd=PROJECT_ROOT,
        stdout=sys.stderr,
        stderr=sys.stderr,
        check=False,
    )
    if completed.returncode != 0:
        print(
            f"canonical-bootstrap: Blender fixture generation failed with exit code {completed.returncode}",
            file=sys.stderr,
        )
        return completed.returncode or 1
    if not canonical.is_file() or canonical.stat().st_size <= 0:
        print(
            f"canonical-bootstrap: fixture generation produced no canonical GLB: {canonical}",
            file=sys.stderr,
        )
        return 1

    metadata.write_text(revision + "\n", encoding="utf-8")
    print(canonical)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
