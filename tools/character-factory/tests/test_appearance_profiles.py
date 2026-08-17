from __future__ import annotations

import json
from pathlib import Path
import sys
import tempfile
import unittest

TOOL_ROOT = Path(__file__).resolve().parents[1]
if str(TOOL_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOL_ROOT))

from api import AppearanceStrategy, BuildSpec, CharacterFactoryError
from runtime.appearance import (
    appearance_profile_for,
    multiview_command,
    validate_appearance_spec,
)


class AppearanceProfileTests(unittest.TestCase):
    def test_default_strategies_preserve_legacy_behavior(self) -> None:
        self.assertEqual(
            AppearanceStrategy.CHARACTER_MULTIVIEW,
            self._load(self._character_payload()).appearance_strategy,
        )
        self.assertEqual(
            AppearanceStrategy.PRESERVE_GENERATOR,
            self._load(self._clothing_payload()).appearance_strategy,
        )
        self.assertEqual(
            AppearanceStrategy.PRESERVE_GENERATOR,
            self._load(self._weapon_payload()).appearance_strategy,
        )

    def test_strategy_must_match_asset_type(self) -> None:
        payload = self._weapon_payload()
        payload["appearance"] = {"strategy": "character-multiview"}
        with self.assertRaisesRegex(CharacterFactoryError, "not valid for weapon"):
            self._load(payload)

    def test_garment_multiview_uses_garment_projection_profile(self) -> None:
        payload = self._clothing_payload()
        payload["appearance"] = {"strategy": "garment-multiview"}
        spec = self._load(payload)
        profile = validate_appearance_spec(spec)
        self.assertEqual("garment", profile.projection_profile)

        command = multiview_command(
            TOOL_ROOT,
            spec,
            input_mesh=Path("/tmp/robe.prepared.fbx"),
            output_mesh=Path("/tmp/robe.fbx"),
            atlas=Path("/tmp/robe.png"),
        )
        self.assertIn("blender_project_multiview_asset.py", command[command.index("--python") + 1])
        self.assertEqual("garment", command[command.index("--profile") + 1])

    def test_rigid_multiview_uses_rigid_projection_profile(self) -> None:
        payload = self._weapon_payload(four_views=True)
        payload["appearance"] = {"strategy": "rigid-multiview"}
        spec = self._load(payload)
        profile = appearance_profile_for(spec.appearance_strategy)
        self.assertEqual("rigid", profile.projection_profile)

        command = multiview_command(
            TOOL_ROOT,
            spec,
            input_mesh=Path("/tmp/sword.prepared.fbx"),
            output_mesh=Path("/tmp/sword.fbx"),
            atlas=Path("/tmp/sword.png"),
        )
        self.assertEqual("rigid", command[command.index("--profile") + 1])

    def test_multiview_strategy_rejects_missing_views_before_generation(self) -> None:
        payload = self._weapon_payload(four_views=False)
        payload["appearance"] = {"strategy": "rigid-multiview"}
        spec = self._load(payload)
        with self.assertRaisesRegex(CharacterFactoryError, "missing: back, left, right"):
            validate_appearance_spec(spec)

    def _load(self, payload: dict[str, object]) -> BuildSpec:
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "asset.json"
            path.write_text(json.dumps(payload), encoding="utf-8")
            return BuildSpec.load(path, validate_paths=False)

    @staticmethod
    def _character_payload() -> dict[str, object]:
        return {
            "id": "hero",
            "assetType": "character",
            "views": {
                "front": "front.png",
                "back": "back.png",
                "left": "left.png",
                "right": "right.png",
            },
            "generator": {"python": "/tmp/generator/python"},
            "rig": {
                "blender": "/Applications/Blender.app/Contents/MacOS/Blender",
                "canonicalBody": "canonical.glb",
            },
        }

    @staticmethod
    def _clothing_payload() -> dict[str, object]:
        return {
            "id": "robe",
            "assetType": "clothing",
            "views": {
                "front": "front.png",
                "back": "back.png",
                "left": "left.png",
                "right": "right.png",
            },
            "generator": {"python": "/tmp/generator/python"},
            "rig": {
                "blender": "/Applications/Blender.app/Contents/MacOS/Blender",
                "canonicalBody": "canonical.glb",
            },
            "runtimePart": {"slot": "Torso"},
        }

    @staticmethod
    def _weapon_payload(*, four_views: bool = False) -> dict[str, object]:
        views: dict[str, object] = {"front": "front.png"}
        if four_views:
            views.update(
                {
                    "back": "back.png",
                    "left": "left.png",
                    "right": "right.png",
                }
            )
        return {
            "id": "sword",
            "assetType": "weapon",
            "views": views,
            "generator": {"python": "/tmp/generator/python"},
            "rigid": {"blender": "/Applications/Blender.app/Contents/MacOS/Blender"},
            "runtimePart": {
                "slot": "MainHand",
                "socketBoneName": "RightHand",
            },
        }


if __name__ == "__main__":
    unittest.main()
