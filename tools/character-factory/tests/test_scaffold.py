from __future__ import annotations

import json
from pathlib import Path
import sys
import tempfile
import unittest

TOOL_ROOT = Path(__file__).resolve().parents[1]
if str(TOOL_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOL_ROOT))

from api import AssetType, CharacterFactoryError
from runtime.scaffold import scaffold_asset


class ScaffoldTests(unittest.TestCase):
    def test_character_scaffold_creates_reference_contract_and_rig_profile(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            project = root / "repo"
            library = project / "tools" / "character-factory" / "production-assets"

            result = scaffold_asset(
                project_root=project,
                library_root=library,
                asset_type=AssetType.CHARACTER,
                asset_id="steven_01",
                backend_profile="hunyuan-quality-macos",
                tags=["Castle", "MainCast", "castle"],
            )
            payload = json.loads(result.spec.read_text(encoding="utf-8"))

            self.assertEqual("character", payload["assetType"])
            self.assertEqual("character-multiview", payload["appearance"]["strategy"])
            self.assertEqual("hunyuan-quality-macos", payload["generator"]["profile"])
            self.assertEqual("canonical-humanoid-macos", payload["rig"]["profile"])
            self.assertEqual(0.45, payload["rig"]["maxTransferDistance"])
            self.assertNotIn("canonicalBody", payload["rig"])
            self.assertNotIn("blender", payload["rig"])
            self.assertEqual(["castle", "maincast"], payload["tags"])
            self.assertEqual({"directory": "geometry"}, payload["references"]["geometry"])
            self.assertEqual({"directory": "appearance"}, payload["references"]["appearance"])
            self.assertTrue(result.geometry.is_dir())
            self.assertTrue(result.appearance is not None and result.appearance.is_dir())
            self.assertTrue(result.details.is_dir())
            self.assertNotIn("runtimePart", payload)
            self.assertIn("Artifacts/CharacterFactoryProduction/character/steven_01", payload["outputDir"])

    def test_clothing_scaffold_uses_rig_profile_and_torso_slot(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            result = scaffold_asset(
                project_root=root,
                library_root=root / "assets",
                asset_type=AssetType.CLOTHING,
                asset_id="guard_tunic_01",
                backend_profile="hunyuan-quality-macos",
                tags=["castle", "guard"],
            )
            payload = json.loads(result.spec.read_text(encoding="utf-8"))
            self.assertEqual("garment-multiview", payload["appearance"]["strategy"])
            self.assertEqual("canonical-humanoid-macos", payload["rig"]["profile"])
            self.assertNotIn("bodyObject", payload["rig"])
            self.assertEqual("Torso", payload["runtimePart"]["slot"])
            self.assertIsNone(payload["runtimePart"]["socketBoneName"])

    def test_explicit_canonical_body_remains_supported_for_legacy_assets(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            canonical = root / "canonical.glb"
            canonical.write_bytes(b"canonical")
            result = scaffold_asset(
                project_root=root,
                library_root=root / "assets",
                asset_type=AssetType.CLOTHING,
                asset_id="legacy_robe",
                backend_profile="hunyuan-quality-macos",
                canonical_body=canonical,
                rig_profile=None,
            )
            payload = json.loads(result.spec.read_text(encoding="utf-8"))
            self.assertNotIn("profile", payload["rig"])
            self.assertEqual("GarmentDonor", payload["rig"]["bodyObject"])
            self.assertIn("canonical.glb", payload["rig"]["canonicalBody"])

    def test_weapon_scaffold_is_immediately_rigid_and_socketed(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            result = scaffold_asset(
                project_root=root,
                library_root=root / "assets",
                asset_type=AssetType.WEAPON,
                asset_id="guard_sword_01",
                backend_profile="hunyuan-quality-macos",
                tags=["castle", "guard", "sword"],
            )
            payload = json.loads(result.spec.read_text(encoding="utf-8"))
            self.assertEqual("preserve-generator", payload["appearance"]["strategy"])
            self.assertNotIn("appearance", payload["references"])
            self.assertIsNone(result.appearance)
            self.assertIn("rigid", payload)
            self.assertEqual("MainHand", payload["runtimePart"]["slot"])
            self.assertEqual("RightHand", payload["runtimePart"]["socketBoneName"])

    def test_existing_asset_is_not_overwritten_without_force(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            kwargs = dict(
                project_root=root,
                library_root=root / "assets",
                asset_type=AssetType.ACCESSORY,
                asset_id="badge_01",
                backend_profile="hunyuan-quality-macos",
            )
            result = scaffold_asset(**kwargs)
            result.spec.write_text("sentinel", encoding="utf-8")
            with self.assertRaisesRegex(CharacterFactoryError, "already exists"):
                scaffold_asset(**kwargs)
            self.assertEqual("sentinel", result.spec.read_text(encoding="utf-8"))

            scaffold_asset(**kwargs, force=True)
            self.assertNotEqual("sentinel", result.spec.read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main()
