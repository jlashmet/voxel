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

from api import BuildSpec
from runtime import CharacterFactoryRuntime
from runtime.geometry_cache import (
    cache_entry,
    geometry_fingerprint,
    restore_geometry_cache,
    store_geometry_cache,
)
from runtime.pipelines.weapon import WeaponPipeline


class GeometryCacheTests(unittest.TestCase):
    def test_store_and_restore_prepared_fbx_and_rigid_contract(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            spec = self._write_and_load(root)
            plan = WeaponPipeline(TOOL_ROOT).plan(spec)
            fingerprint = geometry_fingerprint(TOOL_ROOT, spec, plan)

            with patch.dict(
                os.environ,
                {"CHARACTER_FACTORY_GEOMETRY_CACHE_ROOT": str(root / "cache")},
            ):
                entry = cache_entry(spec, fingerprint)
                plan.output.write_bytes(b"prepared-fbx-v1")
                plan.output.with_suffix(".rigid-contract.json").write_text(
                    json.dumps({"schemaVersion": 1, "targetLength": 1.2}),
                    encoding="utf-8",
                )
                store_geometry_cache(entry, fingerprint, plan)

                plan.output.unlink()
                plan.output.with_suffix(".rigid-contract.json").unlink()
                self.assertTrue(restore_geometry_cache(entry, plan))
                self.assertEqual(b"prepared-fbx-v1", plan.output.read_bytes())
                self.assertEqual(
                    1.2,
                    json.loads(
                        plan.output.with_suffix(".rigid-contract.json").read_text()
                    )["targetLength"],
                )
                metadata = json.loads(entry.metadata.read_text())
                self.assertEqual(fingerprint.value, metadata["fingerprint"])

    def test_runtime_cache_hit_skips_missing_generator_after_appearance_change(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            cache_root = root / "cache"
            spec = self._write_and_load(root)
            pipeline = WeaponPipeline(TOOL_ROOT)
            plan = pipeline.plan(spec)
            fingerprint = geometry_fingerprint(TOOL_ROOT, spec, plan)

            with patch.dict(
                os.environ,
                {"CHARACTER_FACTORY_GEOMETRY_CACHE_ROOT": str(cache_root)},
            ):
                entry = cache_entry(spec, fingerprint)
                plan.output.write_bytes(b"cached-prepared-fbx")
                plan.output.with_suffix(".rigid-contract.json").write_text(
                    json.dumps({"schemaVersion": 1}),
                    encoding="utf-8",
                )
                store_geometry_cache(entry, fingerprint, plan)
                plan.output.unlink()
                plan.output.with_suffix(".rigid-contract.json").unlink()

                # Appearance is not a geometry dependency. The generator executable
                # is intentionally nonexistent; a cache miss would try to execute it
                # and fail this test.
                (root / "appearance.png").write_bytes(b"appearance-v2")
                spec = BuildSpec.load(root / "asset.json", validate_paths=False)
                manifest = CharacterFactoryRuntime(TOOL_ROOT).build(spec)

                payload = json.loads(manifest.read_text(encoding="utf-8"))
                self.assertTrue(payload["geometryCache"]["hit"])
                self.assertEqual(fingerprint.value, payload["geometryCache"]["fingerprint"])
                self.assertEqual(b"cached-prepared-fbx", plan.output.read_bytes())
                self.assertTrue(plan.output.with_suffix(".rigid-contract.json").is_file())

    def test_appearance_and_detail_changes_do_not_invalidate_geometry(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            spec = self._write_and_load(root)
            pipeline = WeaponPipeline(TOOL_ROOT)
            first = geometry_fingerprint(TOOL_ROOT, spec, pipeline.plan(spec)).value

            (root / "appearance.png").write_bytes(b"appearance-v2")
            (root / "ornament.png").write_bytes(b"ornament-v2")
            spec = BuildSpec.load(root / "asset.json", validate_paths=False)
            second = geometry_fingerprint(TOOL_ROOT, spec, pipeline.plan(spec)).value
            self.assertEqual(first, second)

    def test_geometry_generator_and_prepare_changes_invalidate_geometry(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            spec = self._write_and_load(root)
            pipeline = WeaponPipeline(TOOL_ROOT)
            baseline = geometry_fingerprint(TOOL_ROOT, spec, pipeline.plan(spec)).value

            (root / "front.png").write_bytes(b"geometry-v2")
            spec = BuildSpec.load(root / "asset.json", validate_paths=False)
            geometry_changed = geometry_fingerprint(
                TOOL_ROOT, spec, pipeline.plan(spec)
            ).value
            self.assertNotEqual(baseline, geometry_changed)

            payload = json.loads((root / "asset.json").read_text())
            payload["generator"]["seed"] = 999
            (root / "asset.json").write_text(json.dumps(payload), encoding="utf-8")
            spec = BuildSpec.load(root / "asset.json", validate_paths=False)
            generator_changed = geometry_fingerprint(
                TOOL_ROOT, spec, pipeline.plan(spec)
            ).value
            self.assertNotEqual(geometry_changed, generator_changed)

            payload["rigid"]["targetLength"] = 1.35
            (root / "asset.json").write_text(json.dumps(payload), encoding="utf-8")
            spec = BuildSpec.load(root / "asset.json", validate_paths=False)
            prepare_changed = geometry_fingerprint(
                TOOL_ROOT, spec, pipeline.plan(spec)
            ).value
            self.assertNotEqual(generator_changed, prepare_changed)

    @staticmethod
    def _write_and_load(root: Path) -> BuildSpec:
        (root / "front.png").write_bytes(b"geometry-v1")
        (root / "appearance.png").write_bytes(b"appearance-v1")
        (root / "ornament.png").write_bytes(b"ornament-v1")
        payload = {
            "id": "cached_sword",
            "assetType": "weapon",
            "views": {"front": "front.png"},
            "references": {
                "appearance": {"front": "appearance.png"},
                "details": {"ornament": "ornament.png"},
            },
            "generator": {
                "python": "/tmp/generator/python",
                "seed": 123,
            },
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
            "outputDir": str(root / "out"),
        }
        path = root / "asset.json"
        path.write_text(json.dumps(payload), encoding="utf-8")
        return BuildSpec.load(path, validate_paths=False)


if __name__ == "__main__":
    unittest.main()
