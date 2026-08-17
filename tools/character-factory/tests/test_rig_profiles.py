from __future__ import annotations

import json
import os
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

TOOL_ROOT = Path(__file__).resolve().parents[1]
if str(TOOL_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOL_ROOT))

from api.models import AssetType, BuildSpec, CharacterFactoryError, RigConfig
from api.rig_profiles import canonical_donor_state
from runtime.geometry_cache import geometry_fingerprint
from runtime.pipeline import CharacterFactoryRuntime, rig_metadata
from runtime.pipelines.character import CharacterPipeline


class RigProfileTests(unittest.TestCase):
    def test_profile_resolves_code_keyed_donor_and_character_defaults(self) -> None:
        with patch.dict(os.environ, {"CHARACTER_FACTORY_CACHE_ROOT": "/tmp/cf-rig-cache"}):
            config = RigConfig.from_dict(
                {"profile": "canonical-humanoid-macos"},
                TOOL_ROOT,
                asset_type=AssetType.CHARACTER,
                validate_paths=False,
            )

        self.assertEqual("canonical-humanoid-macos", config.profile)
        self.assertEqual("Body", config.body_object)
        self.assertEqual("Armature", config.armature_object)
        self.assertTrue(str(config.canonical_body).startswith("/tmp/cf-rig-cache/canonical-donors/"))
        self.assertTrue(str(config.canonical_body).endswith("canonical_female_with_garment_donor.glb"))
        self.assertIsNotNone(config.source_revision)
        self.assertEqual("bootstrap_canonical.py", config.bootstrap_script.name)

    def test_clothing_profile_uses_garment_donor(self) -> None:
        config = RigConfig.from_dict(
            {"profile": "canonical-humanoid-macos", "maxTransferDistance": 0.31},
            TOOL_ROOT,
            asset_type=AssetType.CLOTHING,
            validate_paths=False,
        )
        self.assertEqual("GarmentDonor", config.body_object)
        self.assertEqual(0.31, config.max_transfer_distance)

    def test_profile_rejects_donor_identity_overrides(self) -> None:
        for key, value in (
            ("canonicalBody", "/tmp/custom.glb"),
            ("blender", "/tmp/custom-blender"),
            ("bodyObject", "OtherBody"),
        ):
            with self.subTest(key=key):
                with self.assertRaises(CharacterFactoryError):
                    RigConfig.from_dict(
                        {"profile": "canonical-humanoid-macos", key: value},
                        TOOL_ROOT,
                        asset_type=AssetType.CHARACTER,
                        validate_paths=False,
                    )

    def test_explicit_legacy_rig_remains_supported(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            donor = root / "donor.glb"
            donor.write_bytes(b"legacy-donor")
            config = RigConfig.from_dict(
                {
                    "blender": "/Applications/Blender.app/Contents/MacOS/Blender",
                    "canonicalBody": str(donor),
                    "bodyObject": "Body",
                    "armatureObject": "Armature",
                },
                root,
                asset_type=AssetType.CHARACTER,
                validate_paths=True,
            )
        self.assertIsNone(config.profile)
        self.assertIsNone(config.bootstrap_script)
        self.assertEqual(donor, config.canonical_body)

    def test_build_spec_accepts_profile_before_donor_exists(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            payload = self._character_payload(root)
            path = root / "asset.json"
            path.write_text(json.dumps(payload), encoding="utf-8")
            with patch.dict(os.environ, {"CHARACTER_FACTORY_CACHE_ROOT": str(root / "cache")}):
                spec = BuildSpec.load(path, validate_paths=False)
                self.assertEqual("canonical-humanoid-macos", spec.rig.profile)
                self.assertFalse(spec.rig.canonical_body.exists())
                self.assertEqual("Body", spec.rig.body_object)

    def test_dry_run_records_rig_bootstrap_and_manifest_metadata(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            path = root / "asset.json"
            path.write_text(json.dumps(self._character_payload(root)), encoding="utf-8")
            with patch.dict(os.environ, {"CHARACTER_FACTORY_CACHE_ROOT": str(root / "cache")}):
                spec = BuildSpec.load(path, validate_paths=False)
                manifest = CharacterFactoryRuntime(TOOL_ROOT).build(spec, dry_run=True)
                payload = json.loads(manifest.read_text(encoding="utf-8"))

        self.assertEqual("canonical-humanoid-macos", payload["rig"]["profile"])
        self.assertEqual(spec.rig.source_revision, payload["rig"]["sourceRevision"])
        self.assertIn("rigBootstrap", payload["commands"])
        self.assertTrue(payload["commands"]["rigBootstrap"][1].endswith("bootstrap_canonical.py"))
        self.assertIn("--blender", payload["commands"]["rigBootstrap"])

    def test_profile_geometry_fingerprint_is_stable_across_donor_bootstrap(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            path = root / "asset.json"
            path.write_text(json.dumps(self._character_payload(root)), encoding="utf-8")
            with patch.dict(os.environ, {"CHARACTER_FACTORY_CACHE_ROOT": str(root / "cache")}):
                spec = BuildSpec.load(path, validate_paths=False)
                pipeline = CharacterPipeline(TOOL_ROOT)
                before = geometry_fingerprint(TOOL_ROOT, spec, pipeline.plan(spec)).value

                revision, donor = canonical_donor_state(TOOL_ROOT)
                donor.parent.mkdir(parents=True, exist_ok=True)
                donor.write_bytes(b"generated-canonical-donor")
                (donor.parent / "source.sha256").write_text(revision + "\n", encoding="utf-8")

                spec_after = BuildSpec.load(path, validate_paths=False)
                after = geometry_fingerprint(
                    TOOL_ROOT,
                    spec_after,
                    pipeline.plan(spec_after),
                ).value

        self.assertEqual(before, after)

    def test_ready_profile_skips_bootstrap_execution(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            path = root / "asset.json"
            path.write_text(json.dumps(self._character_payload(root)), encoding="utf-8")
            with patch.dict(os.environ, {"CHARACTER_FACTORY_CACHE_ROOT": str(root / "cache")}):
                spec = BuildSpec.load(path, validate_paths=False)
                revision, donor = canonical_donor_state(TOOL_ROOT)
                donor.parent.mkdir(parents=True, exist_ok=True)
                donor.write_bytes(b"canonical")
                (donor.parent / "source.sha256").write_text(revision + "\n", encoding="utf-8")
                runtime = CharacterFactoryRuntime(TOOL_ROOT)
                command = runtime._ensure_rig_profile(spec, dry_run=False)
                self.assertIsNotNone(command)
                self.assertTrue(runtime._rig_profile_ready(spec))

    def test_rig_metadata_records_profile_identity(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            path = root / "asset.json"
            path.write_text(json.dumps(self._character_payload(root)), encoding="utf-8")
            spec = BuildSpec.load(path, validate_paths=False)
            metadata = rig_metadata(spec)
        self.assertEqual("canonical-humanoid-macos", metadata["profile"])
        self.assertEqual("Body", metadata["bodyObject"])

    @staticmethod
    def _character_payload(root: Path) -> dict[str, object]:
        return {
            "id": "profiled_character",
            "assetType": "character",
            "views": {"front": "front.png"},
            "outputDir": str(root / "out"),
            "generator": {"python": "/tmp/generator/python"},
            "rig": {"profile": "canonical-humanoid-macos"},
        }


if __name__ == "__main__":
    unittest.main()
