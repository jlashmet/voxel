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
from runtime.catalogue import (
    catalogue_payload,
    classify_changes,
    load_catalogue_entries,
    select_changed_entries,
    select_entries,
)


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
        self.assertIsNotNone(sword["referenceHashes"]["geometry"]["front"])
        self.assertIsNotNone(sword["referenceHashes"]["appearance"]["front"])
        self.assertIsNotNone(sword["referenceHashes"]["details"]["ornament"])

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

    def test_change_classifier_distinguishes_reference_stages(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            spec_path = root / "weapon" / "asset.json"
            self._write_weapon(spec_path, "sword_01")
            baseline = catalogue_payload(root)

            # Geometry-only content change.
            (spec_path.parent / "front.png").write_bytes(b"geometry-v2")
            changes, removed = classify_changes(load_catalogue_entries(root), baseline)
            self.assertEqual([], removed)
            self.assertEqual(1, len(changes))
            self.assertEqual(frozenset({"geometry"}), changes[0].kinds)

            # Reset the baseline, then change only appearance.
            baseline = catalogue_payload(root)
            (spec_path.parent / "appearance.png").write_bytes(b"appearance-v2")
            changes, _ = classify_changes(load_catalogue_entries(root), baseline)
            self.assertEqual(frozenset({"appearance"}), changes[0].kinds)
            self.assertEqual(
                ["weapon:sword_01"],
                [entry.key for entry in select_changed_entries(changes, change_kinds={"appearance"})],
            )
            self.assertEqual(
                [],
                select_changed_entries(changes, change_kinds={"geometry"}),
            )

            # Reset again, then change only a named detail.
            baseline = catalogue_payload(root)
            (spec_path.parent / "ornament.png").write_bytes(b"ornament-v2")
            changes, _ = classify_changes(load_catalogue_entries(root), baseline)
            self.assertEqual(frozenset({"details"}), changes[0].kinds)

    def test_change_classifier_handles_spec_new_and_removed_assets(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            sword_path = root / "sword" / "asset.json"
            robe_path = root / "robe" / "asset.json"
            self._write_weapon(sword_path, "sword_01")
            self._write_clothing(robe_path, "robe_01")
            baseline = catalogue_payload(root)

            # Spec-only change: adjust target length without touching references.
            payload = json.loads(sword_path.read_text(encoding="utf-8"))
            payload["rigid"]["targetLength"] = 1.35
            sword_path.write_text(json.dumps(payload), encoding="utf-8")

            # Remove the robe and add a new staff.
            robe_path.unlink()
            self._write_weapon(root / "staff" / "asset.json", "staff_01")

            changes, removed = classify_changes(load_catalogue_entries(root), baseline)
            by_key = {change.key: change.kinds for change in changes}
            self.assertEqual(frozenset({"spec"}), by_key["weapon:sword_01"])
            self.assertEqual(
                frozenset({"new", "spec", "geometry", "appearance", "details"}),
                by_key["weapon:staff_01"],
            )
            self.assertEqual(["clothing:robe_01"], removed)

    @staticmethod
    def _write_weapon(path: Path, asset_id: str) -> None:
        path.parent.mkdir(parents=True, exist_ok=True)
        (path.parent / "front.png").write_bytes(b"geometry-v1")
        (path.parent / "appearance.png").write_bytes(b"appearance-v1")
        (path.parent / "ornament.png").write_bytes(b"ornament-v1")
        path.write_text(
            json.dumps(
                {
                    "id": asset_id,
                    "assetType": "weapon",
                    "views": {"front": "front.png"},
                    "references": {
                        "appearance": {"front": "appearance.png"},
                        "details": {"ornament": "ornament.png"},
                    },
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
        (path.parent / "front.png").write_bytes(b"robe-geometry-v1")
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
