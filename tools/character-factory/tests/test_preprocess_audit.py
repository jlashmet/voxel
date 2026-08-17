from __future__ import annotations

import json
import os
from pathlib import Path
import struct
import sys
import tempfile
import unittest
from types import SimpleNamespace
from unittest.mock import patch
import zlib

TOOL_ROOT = Path(__file__).resolve().parents[1]
if str(TOOL_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOL_ROOT))

from api.models import BuildSpec
from runtime.pipeline import CharacterFactoryRuntime
from runtime.preprocess import PREPROCESS_AUDIT_NAME, declared_preprocess_steps, prepare_spec_references


def _png_rgb(width: int = 16, height: int = 16) -> bytes:
    def chunk(kind: bytes, data: bytes) -> bytes:
        checksum = zlib.crc32(kind + data) & 0xFFFFFFFF
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", checksum)

    ihdr = struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0)
    row = b"\x00" + bytes((128, 128, 128)) * width
    pixels = row * height
    return (
        b"\x89PNG\r\n\x1a\n"
        + chunk(b"IHDR", ihdr)
        + chunk(b"IDAT", zlib.compress(pixels))
        + chunk(b"IEND", b"")
    )


class PreprocessAuditTests(unittest.TestCase):
    def test_successful_preprocess_writes_hashed_audit_and_manifest_link(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / "source.jpg"
            source.write_bytes(b"source-bytes")
            script = root / "asset_local.py"
            script.write_text("print('fixture')\n", encoding="utf-8")
            spec_path = root / "asset.json"
            spec_path.write_text(
                json.dumps(
                    {
                        "id": "audited_weapon",
                        "assetType": "weapon",
                        "preprocess": [
                            {
                                "strategy": "python-script",
                                "pythonProfile": "triposr-smoke-macos",
                                "script": "asset_local.py",
                                "inputs": ["source.jpg"],
                                "arguments": ["--output", "generated/front.png"],
                                "outputs": ["generated/front.png"],
                                "affects": ["geometry"],
                            }
                        ],
                        "views": {"front": "generated/front.png"},
                        "outputDir": "out",
                        "generator": {"python": sys.executable},
                        "rigid": {
                            "blender": "/Applications/Blender.app/Contents/MacOS/Blender"
                        },
                        "runtimePart": {
                            "slot": "MainHand",
                            "socketBoneName": "RightHand"
                        },
                    }
                ),
                encoding="utf-8",
            )

            with patch.dict(os.environ, {"CHARACTER_FACTORY_CACHE_ROOT": str(root / "cache")}):
                steps = declared_preprocess_steps(spec_path, TOOL_ROOT)
                managed_python = Path(steps[0].python)
                managed_python.parent.mkdir(parents=True, exist_ok=True)
                managed_python.write_bytes(b"python")

                def fake_preprocess(command, cwd, check):
                    output = root / "generated/front.png"
                    output.parent.mkdir(parents=True, exist_ok=True)
                    output.write_bytes(_png_rgb())
                    return SimpleNamespace(returncode=0)

                with patch("runtime.preprocess.subprocess.run", side_effect=fake_preprocess):
                    prepare_spec_references(spec_path, TOOL_ROOT, dry_run=False)

                audit_path = root / "out" / PREPROCESS_AUDIT_NAME
                audit = json.loads(audit_path.read_text(encoding="utf-8"))
                self.assertEqual(1, audit["schemaVersion"])
                self.assertEqual(1, len(audit["steps"]))
                self.assertEqual("python-script", audit["steps"][0]["strategy"])
                self.assertEqual(["geometry"], audit["steps"][0]["affects"])
                self.assertTrue(all(item["sha256"] for item in audit["steps"][0]["inputs"]))
                self.assertTrue(audit["steps"][0]["outputs"][0]["sha256"])

                spec = BuildSpec.load(spec_path, validate_paths=True)
                runtime = CharacterFactoryRuntime(TOOL_ROOT)

                def fake_execute(_pipeline, plan, dry_run=False):
                    plan.output.parent.mkdir(parents=True, exist_ok=True)
                    plan.output.write_bytes(b"fbx")
                    return plan

                with patch("runtime.pipeline.WeaponPipeline.execute", new=fake_execute):
                    manifest_path = runtime.build(
                        spec,
                        dry_run=False,
                        use_geometry_cache=False,
                    )

                manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
                self.assertEqual(str(audit_path), manifest["preprocessAudit"])


if __name__ == "__main__":
    unittest.main()
