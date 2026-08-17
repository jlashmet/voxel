from __future__ import annotations

import json
from pathlib import Path
import sys
import tempfile
import unittest

TOOL_ROOT = Path(__file__).resolve().parents[1]
if str(TOOL_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOL_ROOT))

from api import AssetType, BuildSpec
from runtime import CharacterFactoryRuntime
from runtime.production import (
    ProductionRunner,
    discover_specs,
    has_complete_multiview,
    production_profile_for,
)


class ProductionRoutingTests(unittest.TestCase):
    def test_every_asset_type_has_a_production_profile(self) -> None:
        profiles = {asset_type: production_profile_for(asset_type) for asset_type in AssetType}
        self.assertEqual(set(AssetType), set(profiles))
        self.assertTrue(profiles[AssetType.CHARACTER].project_multiview_appearance)
        self.assertEqual(
            ("verify_skinned_character.py",),
            profiles[AssetType.CLOTHING].verification_scripts,
        )
        self.assertEqual(
            ("verify_rigid_asset.py",),
            profiles[AssetType.WEAPON].verification_scripts,
        )
        self.assertEqual(
            ("verify_rigid_asset.py",),
            profiles[AssetType.ACCESSORY].verification_scripts,
        )

    def test_discovery_is_recursive_and_ignores_generated_manifests(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            nested = root / "characters" / "madeline"
            nested.mkdir(parents=True)
            spec = nested / "asset.json"
            spec.write_text(
                json.dumps({"id": "madeline", "assetType": "character"}),
                encoding="utf-8",
            )
            (nested / "manifest.json").write_text(
                json.dumps({"id": "generated", "assetType": "character"}),
                encoding="utf-8",
            )
            (nested / "madeline.characterfactory.json").write_text(
                json.dumps({"id": "staged", "assetType": "character"}),
                encoding="utf-8",
            )
            (root / "notes.json").write_text("{}", encoding="utf-8")

            self.assertEqual([spec.resolve()], discover_specs(root))
            self.assertEqual([], discover_specs(root, recursive=False))

    def test_complete_multiview_is_required_for_character_projection(self) -> None:
        payload = self._character_payload()
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            spec_path = root / "character.json"
            spec_path.write_text(json.dumps(payload), encoding="utf-8")
            spec = BuildSpec.load(spec_path, validate_paths=False)
            self.assertTrue(has_complete_multiview(spec))

            payload["views"].pop("right")
            spec_path.write_text(json.dumps(payload), encoding="utf-8")
            spec = BuildSpec.load(spec_path, validate_paths=False)
            self.assertFalse(has_complete_multiview(spec))

    def test_character_production_dry_run_records_multiview_and_character_gates(self) -> None:
        payload = self._character_payload()
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            payload["outputDir"] = str(root / "out")
            spec_path = root / "character.json"
            spec_path.write_text(json.dumps(payload), encoding="utf-8")
            spec = BuildSpec.load(spec_path, validate_paths=False)
            manifest = ProductionRunner(
                TOOL_ROOT,
                CharacterFactoryRuntime(TOOL_ROOT),
            ).produce(spec, dry_run=True)
            result = json.loads(manifest.read_text(encoding="utf-8"))

        production = result["production"]
        self.assertEqual("multiview-project", production["appearance"]["mode"])
        self.assertEqual(
            ["verify_skinned_character.py", "verify_character_animations.py"],
            production["verification"],
        )
        self.assertIn("idle", production["previews"])

    def test_weapon_production_dry_run_preserves_generator_appearance_and_uses_rigid_gate(self) -> None:
        payload = {
            "id": "sword_01",
            "assetType": "weapon",
            "views": {"front": "front.png"},
            "generator": {"python": "/tmp/generator/python"},
            "rigid": {"blender": "/Applications/Blender.app/Contents/MacOS/Blender"},
            "runtimePart": {
                "slot": "MainHand",
                "socketBoneName": "RightHand",
            },
        }
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            payload["outputDir"] = str(root / "out")
            spec_path = root / "weapon.json"
            spec_path.write_text(json.dumps(payload), encoding="utf-8")
            spec = BuildSpec.load(spec_path, validate_paths=False)
            manifest = ProductionRunner(
                TOOL_ROOT,
                CharacterFactoryRuntime(TOOL_ROOT),
            ).produce(spec, dry_run=True)
            result = json.loads(manifest.read_text(encoding="utf-8"))

        production = result["production"]
        self.assertEqual("preserve-generator", production["appearance"]["mode"])
        self.assertEqual(["verify_rigid_asset.py"], production["verification"])
        self.assertNotIn("idle", production["previews"])

    @staticmethod
    def _character_payload() -> dict[str, object]:
        return {
            "id": "character_01",
            "assetType": "character",
            "views": {
                "front": "front.png",
                "back": "back.png",
                "left": "left.png",
                "right": "right.png",
            },
            "generator": {
                "python": "/tmp/generator/python",
                "backend": "hunyuan-pytorch",
            },
            "rig": {
                "blender": "/Applications/Blender.app/Contents/MacOS/Blender",
                "canonicalBody": "canonical.glb",
            },
        }


if __name__ == "__main__":
    unittest.main()
