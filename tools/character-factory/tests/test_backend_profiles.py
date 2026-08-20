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

from api.models import BuildSpec, CharacterFactoryError, GeneratorBackend, GeneratorConfig
from runtime.pipeline import CharacterFactoryRuntime, generator_metadata


class BackendProfileTests(unittest.TestCase):
    def test_hunyuan_quality_profile_resolves_pinned_environment(self) -> None:
        with patch.dict(os.environ, {"CHARACTER_FACTORY_CACHE_ROOT": "/tmp/cf-cache"}):
            config = GeneratorConfig.from_dict(
                {"profile": "hunyuan-quality-macos"},
                TOOL_ROOT,
                validate_paths=False,
            )

        self.assertEqual("hunyuan-quality-macos", config.profile)
        self.assertEqual(GeneratorBackend.HUNYUAN_PYTORCH, config.backend)
        self.assertEqual("tencent/Hunyuan3D-2mv", config.model)
        self.assertEqual("hunyuan3d-dit-v2-mv-turbo", config.subfolder)
        self.assertEqual(5, config.steps)
        self.assertEqual(256, config.octree_resolution)
        self.assertTrue(config.python.startswith("/tmp/cf-cache/hunyuan3d-2-"))
        self.assertTrue(config.python.endswith("-venv/bin/python"))
        self.assertIsNotNone(config.source_revision)
        self.assertEqual("bootstrap_hunyuan_quality_macos.sh", config.bootstrap_script.name)

    def test_profile_allows_asset_specific_generation_overrides(self) -> None:
        config = GeneratorConfig.from_dict(
            {
                "profile": "hunyuan-quality-macos",
                "seed": 31827,
                "steps": 7,
                "octreeResolution": 320,
                "removeBackground": True,
            },
            TOOL_ROOT,
            validate_paths=False,
        )
        self.assertEqual(31827, config.seed)
        self.assertEqual(7, config.steps)
        self.assertEqual(320, config.octree_resolution)
        self.assertTrue(config.remove_background)

    def test_profile_rejects_machine_environment_overrides(self) -> None:
        with self.assertRaises(CharacterFactoryError):
            GeneratorConfig.from_dict(
                {
                    "profile": "triposr-smoke-macos",
                    "python": "/tmp/custom-python",
                },
                TOOL_ROOT,
                validate_paths=False,
            )

    def test_triposr_profile_resolves_source_and_weights_from_cache_root(self) -> None:
        with patch.dict(os.environ, {"CHARACTER_FACTORY_CACHE_ROOT": "/tmp/cf-cache"}):
            config = GeneratorConfig.from_dict(
                {"profile": "triposr-smoke-macos", "mcResolution": 320},
                TOOL_ROOT,
                validate_paths=False,
            )

        self.assertEqual(GeneratorBackend.TRIPOSR_MPS, config.backend)
        self.assertEqual(320, config.mc_resolution)
        self.assertEqual(Path("/tmp/cf-cache/models/triposr"), config.weights)
        self.assertTrue(str(config.source).startswith("/tmp/cf-cache/TripoSR-"))
        self.assertEqual("bootstrap_triposr_macos.sh", config.bootstrap_script.name)

    def test_manifest_metadata_keeps_profile_and_source_revision(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            payload = {
                "id": "profiled_staff",
                "assetType": "weapon",
                "views": {"front": "staff.png"},
                "outputDir": "out",
                "generator": {
                    "profile": "triposr-smoke-macos",
                    "mcResolution": 256,
                },
                "rigid": {"blender": "/Applications/Blender.app/Contents/MacOS/Blender"},
                "runtimePart": {"slot": "MainHand", "socketBoneName": "RightHand"},
            }
            spec_path = root / "asset.json"
            spec_path.write_text(json.dumps(payload), encoding="utf-8")
            spec = BuildSpec.load(spec_path, validate_paths=False)
            metadata = generator_metadata(spec)

        self.assertEqual("triposr-smoke-macos", metadata["profile"])
        self.assertEqual(spec.generator.source_revision, metadata["sourceRevision"])
        self.assertEqual(256, metadata["mcResolution"])

    def test_dry_run_records_profile_bootstrap_without_executing_it(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            payload = {
                "id": "profiled_staff",
                "assetType": "weapon",
                "views": {"front": "staff.png"},
                "outputDir": "out",
                "generator": {"profile": "triposr-smoke-macos"},
                "rigid": {"blender": "/Applications/Blender.app/Contents/MacOS/Blender"},
                "runtimePart": {"slot": "MainHand", "socketBoneName": "RightHand"},
            }
            spec_path = root / "asset.json"
            spec_path.write_text(json.dumps(payload), encoding="utf-8")
            spec = BuildSpec.load(spec_path, validate_paths=False)
            manifest = CharacterFactoryRuntime(TOOL_ROOT).build(spec, dry_run=True)
            result = json.loads(manifest.read_text(encoding="utf-8"))

        self.assertEqual("dry-run", result["status"])
        self.assertEqual("triposr-smoke-macos", result["generator"]["profile"])
        self.assertEqual("bash", result["commands"]["bootstrap"][0])
        self.assertTrue(result["commands"]["bootstrap"][1].endswith("bootstrap_triposr_macos.sh"))


if __name__ == "__main__":
    unittest.main()
