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


class CatalogueTagTests(unittest.TestCase):
    def test_repeated_tags_are_anded_with_type_filter(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self._write_weapon(root / "sword.json", "sword", ["castle", "guard", "steel"])
            self._write_weapon(root / "staff.json", "staff", ["castle", "mage"])
            self._write_clothing(root / "robe.json", "robe", ["castle", "guard", "cloth"])
            entries = load_catalogue_entries(root)

            castle_guard = select_entries(entries, tags={"castle", "guard"})
            self.assertEqual(
                ["clothing:robe", "weapon:sword"],
                [entry.key for entry in castle_guard],
            )

            weapons = select_entries(
                entries,
                asset_types={AssetType.WEAPON},
                tags={"castle", "guard"},
            )
            self.assertEqual(["weapon:sword"], [entry.key for entry in weapons])

            payload = catalogue_payload(root)
            by_key = {asset["key"]: asset for asset in payload["assets"]}
            self.assertEqual(["castle", "guard", "steel"], by_key["weapon:sword"]["tags"])

    def test_tags_are_normalized_and_deduplicated(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self._write_weapon(root / "sword.json", "sword", ["Castle", "GUARD", "castle"])
            entries = load_catalogue_entries(root)
            self.assertEqual(("castle", "guard"), entries[0].tags)
            selected = select_entries(entries, tags={"CASTLE", "guard"})
            self.assertEqual(["weapon:sword"], [entry.key for entry in selected])

    def test_invalid_tag_is_rejected_while_cataloguing(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self._write_weapon(root / "sword.json", "sword", ["castle guard"])
            with self.assertRaisesRegex(CharacterFactoryError, "invalid catalogue tag"):
                load_catalogue_entries(root)

    @staticmethod
    def _write_weapon(path: Path, asset_id: str, tags: list[str]) -> None:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(
            json.dumps(
                {
                    "id": asset_id,
                    "assetType": "weapon",
                    "tags": tags,
                    "views": {"front": "front.png"},
                    "generator": {"python": "/tmp/generator/python"},
                    "rigid": {"blender": "/Applications/Blender.app/Contents/MacOS/Blender"},
                    "runtimePart": {
                        "slot": "MainHand",
                        "socketBoneName": "RightHand",
                    },
                }
            ),
            encoding="utf-8",
        )

    @staticmethod
    def _write_clothing(path: Path, asset_id: str, tags: list[str]) -> None:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(
            json.dumps(
                {
                    "id": asset_id,
                    "assetType": "clothing",
                    "tags": tags,
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
