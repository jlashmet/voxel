from __future__ import annotations

import binascii
import json
from pathlib import Path
import struct
import sys
import tempfile
import unittest
import zlib

TOOL_ROOT = Path(__file__).resolve().parents[1]
if str(TOOL_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOL_ROOT))

from api import BuildSpec, CharacterFactoryError
from api.references import inspect_image
from runtime.pipeline import reference_metadata
from runtime.production import appearance_views_for


def write_png(path: Path, width: int = 32, height: int = 48) -> None:
    def chunk(name: bytes, payload: bytes) -> bytes:
        crc = binascii.crc32(name)
        crc = binascii.crc32(payload, crc) & 0xFFFFFFFF
        return struct.pack(">I", len(payload)) + name + payload + struct.pack(">I", crc)

    rows = []
    for y in range(height):
        row = bytearray([0])
        for x in range(width):
            row.extend(((x * 7) % 256, (y * 5) % 256, ((x + y) * 3) % 256))
        rows.append(bytes(row))
    data = (
        b"\x89PNG\r\n\x1a\n"
        + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0))
        + chunk(b"IDAT", zlib.compress(b"".join(rows)))
        + chunk(b"IEND", b"")
    )
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(data)


class ReferenceContractTests(unittest.TestCase):
    def test_view_directory_discovers_canonical_names(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            views = root / "views"
            for name in ("front", "back", "left", "right"):
                write_png(views / f"{name}.png")
            spec_path = root / "asset.json"
            payload = self._weapon_payload()
            payload["references"] = {"geometry": {"directory": "views"}}
            spec_path.write_text(json.dumps(payload), encoding="utf-8")

            spec = BuildSpec.load(spec_path)

        self.assertEqual("front.png", spec.views.front.name)
        self.assertEqual("back.png", spec.views.back.name)
        self.assertEqual("left.png", spec.views.left.name)
        self.assertEqual("right.png", spec.views.right.name)

    def test_geometry_appearance_and_details_are_independent(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for folder in ("geometry", "appearance"):
                for name in ("front", "back", "left", "right"):
                    write_png(root / folder / f"{name}.png")
            write_png(root / "details" / "face.png", 64, 64)

            payload = self._character_payload()
            payload["references"] = {
                "geometry": {"directory": "geometry"},
                "appearance": {"directory": "appearance"},
                "details": {"face": "details/face.png"},
            }
            spec_path = root / "asset.json"
            spec_path.write_text(json.dumps(payload), encoding="utf-8")
            spec = BuildSpec.load(spec_path, validate_paths=False)

        self.assertEqual("geometry", spec.views.front.parent.name)
        self.assertIsNotNone(spec.appearance_views)
        self.assertEqual("appearance", spec.appearance_views.front.parent.name)
        self.assertEqual("face.png", spec.detail_references["face"].name)
        self.assertEqual(spec.appearance_views, appearance_views_for(spec))
        metadata = reference_metadata(spec)
        self.assertIn("face", metadata["details"])

    def test_legacy_views_can_add_detail_references_during_migration(self) -> None:
        payload = self._character_payload()
        payload["views"] = {
            "front": "front.png",
            "back": "back.png",
            "left": "left.png",
            "right": "right.png",
        }
        payload["references"] = {"details": {"face": "face.png"}}
        with tempfile.TemporaryDirectory() as directory:
            spec_path = Path(directory) / "asset.json"
            spec_path.write_text(json.dumps(payload), encoding="utf-8")
            spec = BuildSpec.load(spec_path, validate_paths=False)

        self.assertEqual("front.png", spec.views.front.name)
        self.assertEqual("face.png", spec.detail_references["face"].name)

    def test_geometry_reference_and_legacy_views_cannot_both_define_geometry(self) -> None:
        payload = self._character_payload()
        payload["views"] = {"front": "legacy.png"}
        payload["references"] = {"geometry": {"front": "new.png"}}
        with tempfile.TemporaryDirectory() as directory:
            spec_path = Path(directory) / "asset.json"
            spec_path.write_text(json.dumps(payload), encoding="utf-8")
            with self.assertRaisesRegex(CharacterFactoryError, "either legacy views"):
                BuildSpec.load(spec_path, validate_paths=False)

    def test_ambiguous_canonical_view_is_rejected_before_generation(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            views = root / "views"
            write_png(views / "front.png")
            # Discovery is intentionally extension-agnostic and rejects ambiguity
            # before trying to decode either candidate.
            (views / "front.jpg").write_bytes((views / "front.png").read_bytes())
            payload = self._weapon_payload()
            payload["references"] = {"geometry": {"directory": "views"}}
            spec_path = root / "asset.json"
            spec_path.write_text(json.dumps(payload), encoding="utf-8")

            with self.assertRaisesRegex(CharacterFactoryError, "multiple 'front' images"):
                BuildSpec.load(spec_path)

    def test_invalid_image_header_is_rejected_before_generation(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            bad = root / "front.png"
            bad.write_text("not an image", encoding="utf-8")
            payload = self._weapon_payload()
            payload["references"] = {"geometry": {"front": "front.png"}}
            spec_path = root / "asset.json"
            spec_path.write_text(json.dumps(payload), encoding="utf-8")

            with self.assertRaisesRegex(CharacterFactoryError, "supported PNG/JPEG"):
                BuildSpec.load(spec_path)

    def test_reference_metadata_reads_real_dimensions(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "reference.png"
            write_png(path, 37, 53)
            metadata = inspect_image(path)

        self.assertEqual("png", metadata.format)
        self.assertEqual(37, metadata.width)
        self.assertEqual(53, metadata.height)
        self.assertGreater(metadata.size_bytes, 100)

    @staticmethod
    def _weapon_payload() -> dict[str, object]:
        return {
            "id": "weapon_01",
            "assetType": "weapon",
            "generator": {"python": "/tmp/generator/python"},
            "rigid": {"blender": "/tmp/blender"},
            "runtimePart": {
                "slot": "MainHand",
                "socketBoneName": "RightHand",
            },
        }

    @staticmethod
    def _character_payload() -> dict[str, object]:
        return {
            "id": "character_01",
            "assetType": "character",
            "generator": {"python": "/tmp/generator/python"},
            "rig": {
                "blender": "/tmp/blender",
                "canonicalBody": "canonical.glb",
            },
        }


if __name__ == "__main__":
    unittest.main()
