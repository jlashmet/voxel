from __future__ import annotations

import json
from pathlib import Path
import sys
import tempfile
import unittest

TOOL_ROOT = Path(__file__).resolve().parents[1]
if str(TOOL_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOL_ROOT))

from api import AssetType, BuildSpec, CharacterFactoryError
from api.models import GeneratorConfig
from runtime.pipeline import pipeline_type_for


class PipelineRoutingTests(unittest.TestCase):
    def test_every_asset_type_has_its_own_pipeline(self) -> None:
        names = {asset_type: pipeline_type_for(asset_type).__name__ for asset_type in AssetType}
        self.assertEqual("CharacterPipeline", names[AssetType.CHARACTER])
        self.assertEqual("ClothingPipeline", names[AssetType.CLOTHING])
        self.assertEqual("WeaponPipeline", names[AssetType.WEAPON])
        self.assertEqual("AccessoryPipeline", names[AssetType.ACCESSORY])
        self.assertEqual(4, len(set(names.values())))

    def test_smoke_generator_preset_is_fast_turbo(self) -> None:
        config = GeneratorConfig.from_dict({"python": "/tmp/hunyuan/bin/python"})
        self.assertEqual("smoke", config.preset)
        self.assertEqual("tencent/Hunyuan3D-2mini", config.model)
        self.assertEqual("hunyuan3d-dit-v2-mini-turbo", config.subfolder)
        self.assertEqual(5, config.steps)
        self.assertEqual(64, config.octree_resolution)
        self.assertTrue(config.enable_flashvdm)

    def test_quality_generator_preset_can_be_selected_without_pipeline_changes(self) -> None:
        config = GeneratorConfig.from_dict(
            {"python": "/tmp/hunyuan/bin/python", "preset": "quality"}
        )
        self.assertEqual("tencent/Hunyuan3D-2mv", config.model)
        self.assertEqual("hunyuan3d-dit-v2-mv", config.subfolder)
        self.assertEqual(50, config.steps)
        self.assertFalse(config.enable_flashvdm)

    def test_unknown_generator_preset_is_rejected(self) -> None:
        with self.assertRaises(CharacterFactoryError):
            GeneratorConfig.from_dict(
                {"python": "/tmp/hunyuan/bin/python", "preset": "unknown"}
            )

    def test_clothing_spec_is_not_a_generic_wearable(self) -> None:
        payload = {
            "id": "robe",
            "assetType": "clothing",
            "views": {"front": "front.png"},
            "generator": {"python": "/tmp/hunyuan/bin/python"},
            "rig": {
                "blender": "/Applications/Blender.app/Contents/MacOS/Blender",
                "canonicalBody": "canonical.glb",
            },
            "runtimePart": {"slot": "Torso"},
        }

        with tempfile.TemporaryDirectory() as directory:
            spec_path = Path(directory) / "robe.json"
            spec_path.write_text(json.dumps(payload), encoding="utf-8")
            spec = BuildSpec.load(spec_path, validate_paths=False)

        self.assertEqual(AssetType.CLOTHING, spec.asset_type)
        self.assertIsNotNone(spec.rig)
        self.assertIsNone(spec.rigid)

    def test_weapon_requires_socket_metadata(self) -> None:
        payload = {
            "id": "sword",
            "assetType": "weapon",
            "views": {"front": "front.png"},
            "generator": {"python": "/tmp/hunyuan/bin/python"},
            "rigid": {"blender": "/Applications/Blender.app/Contents/MacOS/Blender"},
            "runtimePart": {
                "slot": "MainHand",
                "socketBoneName": "RightHand",
            },
        }

        with tempfile.TemporaryDirectory() as directory:
            spec_path = Path(directory) / "sword.json"
            spec_path.write_text(json.dumps(payload), encoding="utf-8")
            spec = BuildSpec.load(spec_path, validate_paths=False)

        self.assertEqual(AssetType.WEAPON, spec.asset_type)
        self.assertEqual("RightHand", spec.runtime_part.socket_bone_name)


if __name__ == "__main__":
    unittest.main()
