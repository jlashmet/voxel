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
from runtime.catalogue import catalogue_payload, load_catalogue_entries, select_entries


class CatalogueTests(unittest.TestCase):
    def test_catalogue_indexes_types_profiles_runtime_parts_and_rigid_contract(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self._write_weapon(root / "weapons" / "sword" / "asset.json", "sword_01")
            self._write_clothing(root / "clothing" / "robe" / "asset.json", "robe_01")

            payload = catalogue_payload(root)

        self.assertEqual(2, payload["assetCount"])
        self.assertEqual(1, payload["typeCounts"]["weapon"])
        self.assertEqual(1, payload["typeCounts"]["clothing"])
        by_key = {entry["key"]: entry for entry in payload["assets"]}
        sword = by_key["weapon:sword_01"]
        self.assertEqual("preserve-generator", sword["appearanceStrategy"])
        self.assertEqual("MainHand", sword["runtimePart"]["slot"])
        self.assertEqual("z", sword["rigidCanonicalization"]["canonicalAxis"])
        self.assertEqual(1.2, sword["rigidCanonicalization"]["targetLength"])
        self.assertIsNotNone(sword["specSha256"])

    def test_duplicate_type_and_id_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self._write_weapon(root / "a" / "asset.json", "duplicate")
            self._write_weapon(root / "b" / "asset.json", "duplicate")
            with self.assertRaisesRegex(CharacterFactoryError, "duplicate production asset key"):
                load_catalogue_entries(root)

    def test_selection_filters_by_type_and_id(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self._write_weapon(root / "sword.json", "sword_01")
            self._write_weapon(root / "staff.json", "staff_01")
            self._write_clothing(root / "robe.json", "robe_01")
            entries = load_catalogue_entries(root)

            weapons = select_entries(entries, asset_types={AssetType.WEAPON})
            self.assertEqual(["staff_01", "sword_01"], sorted(e.spec.asset_id for e in weapons))

            one = select_entries(entries, asset_ids={"robe_01"})
            self.assertEqual(["clothing:robe_01"], [entry.key for entry in one])

            combined = select_entries(
                entries,
                asset_types={AssetType.WEAPON},
                asset_ids={"staff_01"},
            )
            self.assertEqual(["weapon:staff_01"], [entry.key for entry in combined])

    @staticmethod
    def _write_weapon(path: Path, asset_id: str) -> None:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(
            json.dumps(
                {
                    "id": asset_id,
                    "assetType": "weapon",
                    "views": {"front": "front.png"},
                    "generator": {"python": "/tmp/generator/python"},
                    "rigid": {
                        "blender": "/Applications/Blender.app/Contents/MacOS/Blender",
                        "canonicalAxis": "z",
                        "targetLength": 1.2,
                        "anchorFraction": [0.5, 0.5, 0.1],
                    },
                    "runtimePart": {
                        "slot": "MainHand",
                        "socketBoneName": "RightHand",
                    },
                }
            ),
            encoding="utf-8",
        )

    @staticmethod
    def _write_clothing(path: Path, asset_id: str) -> None:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(
            json.dumps(
                {
                    "id": asset_id,
                    "assetType": "clothing",
                    "views": {"front": "front.png"},
                    "generator": {"python": "/tmp/generator/python"},
                    "rig": {
                        "blender": "/Applications/Blender.app/Contents/MacOS/Blender",
                        "canonicalBody": "canonical.glb",
                    },
                    "runtimePart": {"slot": "Torso"},
                }
            ),
            encoding="utf-8",
        )


if __name__ == "__main__":
    unittest.main()
