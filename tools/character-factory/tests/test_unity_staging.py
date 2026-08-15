from __future__ import annotations

import json
from pathlib import Path
import sys
import tempfile
import unittest

TOOL_ROOT = Path(__file__).resolve().parents[1]
if str(TOOL_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOL_ROOT))

from api import CharacterFactoryError
from runtime.unity_staging import stage_manifest_for_unity


class UnityStagingTests(unittest.TestCase):
    def test_completed_part_stages_portable_descriptor_and_fbx(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            project = Path(directory)
            build = project / "build"
            build.mkdir()
            fbx = build / "robe.fbx"
            fbx.write_bytes(b"fake-fbx")
            manifest = build / "manifest.json"
            manifest.write_text(
                json.dumps(
                    {
                        "id": "cleric_robe",
                        "assetType": "clothing",
                        "status": "complete",
                        "output": str(fbx),
                        "generator": {
                            "backend": "triposr-mps",
                            "preset": "smoke",
                            "device": "auto",
                            "mcResolution": 192,
                        },
                        "runtimePart": {
                            "partKind": "clothing",
                            "slot": "Torso",
                            "mountMode": "SkinnedToCharacterSkeleton",
                            "socketBoneName": None,
                            "socketLocalPosition": [0, 0, 0],
                            "socketLocalEulerAngles": [0, 0, 0],
                            "socketLocalScale": [1, 1, 1],
                        },
                    }
                ),
                encoding="utf-8",
            )

            result = stage_manifest_for_unity(
                manifest,
                Path("Assets/Generated/CharacterFactory"),
                project_root=project,
            )

            self.assertEqual(b"fake-fbx", result.fbx.read_bytes())
            self.assertEqual(
                project
                / "Assets/Generated/CharacterFactory/clothing/cleric_robe/cleric_robe.fbx",
                result.fbx,
            )
            descriptor = json.loads(result.descriptor.read_text(encoding="utf-8"))
            self.assertEqual(1, descriptor["schemaVersion"])
            self.assertEqual("cleric_robe", descriptor["id"])
            self.assertEqual("clothing", descriptor["assetType"])
            self.assertEqual("cleric_robe.fbx", descriptor["fbx"])
            self.assertEqual(
                "Assets/Generated/CharacterFactory/CharacterPartCatalogue.asset",
                descriptor["catalogueAsset"],
            )
            self.assertEqual("Torso", descriptor["runtimePart"]["slot"])
            self.assertEqual("triposr-mps", descriptor["generator"]["backend"])
            self.assertNotIn("output", descriptor)
            self.assertNotIn("commands", descriptor)

    def test_character_can_be_staged_without_runtime_part(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            project = Path(directory)
            build = project / "build"
            build.mkdir()
            fbx = build / "hero.fbx"
            fbx.write_bytes(b"hero")
            manifest = build / "manifest.json"
            manifest.write_text(
                json.dumps(
                    {
                        "id": "hero",
                        "assetType": "character",
                        "status": "complete",
                        "output": str(fbx),
                        "generator": {"backend": "hunyuan-pytorch"},
                        "runtimePart": None,
                    }
                ),
                encoding="utf-8",
            )

            result = stage_manifest_for_unity(
                manifest,
                Path("Assets/Generated/CharacterFactory"),
                project_root=project,
            )
            descriptor = json.loads(result.descriptor.read_text(encoding="utf-8"))
            self.assertIsNone(descriptor["runtimePart"])
            self.assertEqual("character", result.asset_type.value)

    def test_dry_run_manifest_is_not_staged(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            project = Path(directory)
            build = project / "build"
            build.mkdir()
            fbx = build / "robe.fbx"
            fbx.write_bytes(b"fake")
            manifest = build / "manifest.json"
            manifest.write_text(
                json.dumps(
                    {
                        "id": "robe",
                        "assetType": "clothing",
                        "status": "dry-run",
                        "output": str(fbx),
                    }
                ),
                encoding="utf-8",
            )

            with self.assertRaises(CharacterFactoryError):
                stage_manifest_for_unity(
                    manifest,
                    Path("Assets/Generated/CharacterFactory"),
                    project_root=project,
                )

    def test_assets_root_must_live_under_project_assets(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            project = Path(directory)
            build = project / "build"
            build.mkdir()
            fbx = build / "robe.fbx"
            fbx.write_bytes(b"fake")
            manifest = build / "manifest.json"
            manifest.write_text(
                json.dumps(
                    {
                        "id": "robe",
                        "assetType": "clothing",
                        "status": "complete",
                        "output": str(fbx),
                    }
                ),
                encoding="utf-8",
            )

            with self.assertRaises(CharacterFactoryError):
                stage_manifest_for_unity(
                    manifest,
                    Path("GeneratedOutsideAssets"),
                    project_root=project,
                )


if __name__ == "__main__":
    unittest.main()
