import json
import subprocess
import sys
from pathlib import Path


def main() -> int:
    if len(sys.argv) != 2:
        print("usage: verify_pipeline_artifact.py <artifact-dir>", file=sys.stderr)
        return 2

    artifact_dir = Path(sys.argv[1]).resolve()
    raw_glb = artifact_dir / "sunlit_cleric_staff_ci.raw.glb"
    fbx = artifact_dir / "sunlit_cleric_staff_ci.fbx"
    manifest = artifact_dir / "manifest.json"

    for path in (raw_glb, fbx, manifest):
        if not path.is_file() or path.stat().st_size == 0:
            raise RuntimeError(f"missing or empty pipeline output: {path}")

    payload = json.loads(manifest.read_text(encoding="utf-8"))
    if payload.get("status") != "complete":
        raise RuntimeError(f"pipeline manifest status was {payload.get('status')!r}")
    if payload.get("pipeline") != "weapon":
        raise RuntimeError(f"expected weapon pipeline, got {payload.get('pipeline')!r}")

    # Blender can inspect the FBX headlessly without launching Unity. This proves the
    # post-processing leg emitted an importable scene rather than just a non-empty file.
    blender = Path("/Applications/Blender.app/Contents/MacOS/Blender")
    if blender.is_file():
        command = [
            str(blender),
            "--background",
            "--python-expr",
            (
                "import bpy,sys;"
                f"bpy.ops.import_scene.fbx(filepath={str(fbx)!r});"
                "meshes=[o for o in bpy.context.scene.objects if o.type=='MESH'];"
                "print('CI_FBX_MESH_COUNT',len(meshes));"
                "sys.exit(0 if meshes else 3)"
            ),
        ]
        subprocess.run(command, check=True)

    print(f"verified {fbx} ({fbx.stat().st_size} bytes)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
