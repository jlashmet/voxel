from __future__ import annotations

import json
from pathlib import Path
import sys
import tempfile
import unittest

TOOL_ROOT = Path(__file__).resolve().parents[1]
if str(TOOL_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOL_ROOT))

from api import BuildSpec, CharacterFactoryError
from runtime.pipelines.accessory import AccessoryPipeline
from runtime.pipelines.weapon import WeaponPipeline


class RigidContractTests(unittest.TestCase):
    def test_weapon_can_declare_axis_length_and_grip_anchor(self) -> None:
        payload = self._weapon_payload()
        payload["rigid"].update(
            {
                "canonicalAxis": "z",
                "targetLength": 1.25,
                "anchorFraction": [0.5, 0.5, 0.12],
            }
        )
        spec = self._load(payload)
        assert spec.rigid is not None
        self.assertEqual("z", spec.rigid.canonical_axis)
        self.assertEqual(1.25, spec.rigid.target_length)
        self.assertEqual((0.5, 0.5, 0.12), spec.rigid.anchor_fraction)

        command = WeaponPipeline(TOOL_ROOT)._prepare_command(
            spec,
            Path("/tmp/raw.glb"),
            Path("/tmp/sword.fbx"),
        )
        self.assertEqual("z", command[command.index("--canonical-axis") + 1])
        self.assertEqual("1.25", command[command.index("--target-length") + 1])
        anchor_index = command.index("--anchor-fraction")
        self.assertEqual(["0.5", "0.5", "0.12"], command[anchor_index + 1 : anchor_index + 4])

    def test_accessory_uses_same_generic_mount_anchor_contract(self) -> None:
        payload = self._weapon_payload()
        payload["id"] = "charm"
        payload["assetType"] = "accessory"
        payload["runtimePart"] = {
            "slot": "Accessory",
            "socketBoneName": "Chest",
        }
        payload["rigid"].update(
            {
                "canonicalAxis": "y",
                "targetLength": 0.18,
                "anchorFraction": [0.5, 0.0, 0.5],
            }
        )
        spec = self._load(payload)
        command = AccessoryPipeline(TOOL_ROOT)._prepare_command(
            spec,
            Path("/tmp/raw.glb"),
            Path("/tmp/charm.fbx"),
        )
        self.assertIn("--canonical-axis", command)
        self.assertIn("--target-length", command)
        self.assertIn("--anchor-fraction", command)

    def test_generated_detail_shaft_uses_named_detail_as_geometry_input(self) -> None:
        payload = self._weapon_payload()
        payload["references"] = {
            "details": {
                "ornament": "ornament.png",
            }
        }
        payload["rigid"].update(
            {
                "canonicalAxis": "z",
                "targetLength": 1.8,
                "anchorFraction": [0.5, 0.5, 0.1],
                "composition": {
                    "strategy": "generated-detail-shaft",
                    "detailReference": "ornament",
                    "totalLength": 1.8,
                    "detailLength": 0.38,
                    "shaftRadius": 0.024,
                    "axis": "auto",
                    "attachmentSide": "min",
                    "overlap": 0.025,
                },
            }
        )
        spec = self._load(payload)
        assert spec.rigid is not None
        assert spec.rigid.composition is not None
        self.assertEqual("ornament", spec.rigid.composition.detail_reference)

        plan = WeaponPipeline(TOOL_ROOT).plan(spec)
        ornament = str(spec.detail_references["ornament"])
        geometry_front = str(spec.views.front)
        self.assertIn(ornament, plan.generator_command)
        self.assertNotIn(geometry_front, plan.generator_command)
        self.assertTrue(str(plan.raw_mesh).endswith("sword.ornament.raw.glb"))
        self.assertIn("blender_compose_generated_detail_shaft.py", " ".join(plan.prepare_command))
        self.assertEqual("weapon", plan.prepare_command[plan.prepare_command.index("--part-kind") + 1])
        self.assertEqual("z", plan.prepare_command[plan.prepare_command.index("--canonical-axis") + 1])

    def test_accessory_can_use_generated_detail_shaft_composition(self) -> None:
        payload = self._weapon_payload()
        payload["id"] = "lantern"
        payload["assetType"] = "accessory"
        payload["runtimePart"] = {
            "slot": "Accessory",
            "socketBoneName": "RightHand",
        }
        payload["references"] = {"details": {"shade": "shade.png"}}
        payload["rigid"]["composition"] = {
            "strategy": "generated-detail-shaft",
            "detailReference": "shade",
            "totalLength": 0.45,
            "detailLength": 0.14,
            "shaftRadius": 0.01,
        }
        spec = self._load(payload)
        plan = AccessoryPipeline(TOOL_ROOT).plan(spec)
        self.assertIn(str(spec.detail_references["shade"]), plan.generator_command)
        self.assertEqual("accessory", plan.prepare_command[plan.prepare_command.index("--part-kind") + 1])

    def test_composition_requires_existing_named_detail(self) -> None:
        payload = self._weapon_payload()
        payload["rigid"]["composition"] = {
            "strategy": "generated-detail-shaft",
            "detailReference": "ornament",
        }
        with self.assertRaisesRegex(CharacterFactoryError, "references.details"):
            self._load(payload)

    def test_invalid_rigid_canonicalization_is_rejected_before_generation(self) -> None:
        cases = (
            ({"canonicalAxis": "diagonal"}, "canonicalAxis"),
            ({"targetLength": 0}, "targetLength"),
            ({"anchorFraction": [0.5, 1.1, 0.5]}, "anchorFraction"),
            ({"anchorFraction": [0.5, 0.5]}, "anchorFraction"),
        )
        for overrides, expected in cases:
            with self.subTest(overrides=overrides):
                payload = self._weapon_payload()
                payload["rigid"].update(overrides)
                with self.assertRaisesRegex(CharacterFactoryError, expected):
                    self._load(payload)

    def test_invalid_rigid_composition_is_rejected_before_generation(self) -> None:
        cases = (
            ({"strategy": "magic"}, "strategy"),
            ({"strategy": "generated-detail-shaft"}, "detailReference"),
            (
                {
                    "strategy": "generated-detail-shaft",
                    "detailReference": "ornament",
                    "totalLength": 0.2,
                    "detailLength": 0.3,
                },
                "totalLength",
            ),
            (
                {
                    "strategy": "generated-detail-shaft",
                    "detailReference": "ornament",
                    "axis": "diagonal",
                },
                "axis",
            ),
        )
        for composition, expected in cases:
            with self.subTest(composition=composition):
                payload = self._weapon_payload()
                payload["references"] = {"details": {"ornament": "ornament.png"}}
                payload["rigid"]["composition"] = composition
                with self.assertRaisesRegex(CharacterFactoryError, expected):
                    self._load(payload)

    def _load(self, payload: dict[str, object]) -> BuildSpec:
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "asset.json"
            path.write_text(json.dumps(payload), encoding="utf-8")
            return BuildSpec.load(path, validate_paths=False)

    @staticmethod
    def _weapon_payload() -> dict[str, object]:
        return {
            "id": "sword",
            "assetType": "weapon",
            "views": {"front": "front.png"},
            "generator": {"python": "/tmp/generator/python"},
            "rigid": {
                "blender": "/Applications/Blender.app/Contents/MacOS/Blender"
            },
            "runtimePart": {
                "slot": "MainHand",
                "socketBoneName": "RightHand",
            },
        }


if __name__ == "__main__":
    unittest.main()
