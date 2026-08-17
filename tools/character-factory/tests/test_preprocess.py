from __future__ import annotations

import json
import os
from pathlib import Path
from types import SimpleNamespace
import sys
import tempfile
import unittest
from unittest.mock import patch

TOOL_ROOT = Path(__file__).resolve().parents[1]
if str(TOOL_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOL_ROOT))

from api.preprocess import PreprocessContractError, resolve_preprocess_steps
from runtime.catalogue import catalogue_payload, classify_changes, load_catalogue_entries
from runtime.preprocess import declared_preprocess_steps, prepare_spec_references


class PreprocessTests(unittest.TestCase):
    def test_tpose_garment_uses_generator_profile_by_default(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            with patch.dict(os.environ, {"CHARACTER_FACTORY_CACHE_ROOT": str(root / "cache")}):
                steps = resolve_preprocess_steps(
                    [
                        {
                            "strategy": "tpose-garment-views",
                            "inputDirectory": "views",
                            "outputDirectory": "generated/robe",
                            "headCutFraction": 0.2,
                        }
                    ],
                    base_dir=root,
                    tool_root=TOOL_ROOT,
                    default_python_profile="hunyuan-quality-macos",
                )

        self.assertEqual(1, len(steps))
        step = steps[0]
        self.assertEqual("tpose-garment-views", step.strategy)
        self.assertEqual("hunyuan-quality-macos", step.python_profile)
        self.assertIn("prepare_tpose_garment_views.py", step.command[1])
        self.assertIn("--head-cut-fraction", step.command)
        self.assertEqual(frozenset({"geometry", "appearance"}), step.affects)
        self.assertEqual(
            {"front.png", "back.png", "left.png", "right.png"},
            {path.name for path in step.outputs},
        )

    def test_preprocess_requires_profile_without_generator_profile(self) -> None:
        with self.assertRaisesRegex(PreprocessContractError, "requires pythonProfile"):
            resolve_preprocess_steps(
                [
                    {
                        "strategy": "linear-terminal-detail",
                        "input": "source.png",
                        "output": "detail.png",
                    }
                ],
                base_dir=TOOL_ROOT,
                tool_root=TOOL_ROOT,
            )

    def test_python_script_requires_declared_outputs(self) -> None:
        with self.assertRaisesRegex(PreprocessContractError, "outputs"):
            resolve_preprocess_steps(
                [
                    {
                        "strategy": "python-script",
                        "pythonProfile": "triposr-smoke-macos",
                        "script": "asset_local.py",
                        "inputs": ["source.png"],
                    }
                ],
                base_dir=TOOL_ROOT,
                tool_root=TOOL_ROOT,
            )

    def test_python_script_requires_declared_inputs(self) -> None:
        with self.assertRaisesRegex(PreprocessContractError, "inputs"):
            resolve_preprocess_steps(
                [
                    {
                        "strategy": "python-script",
                        "pythonProfile": "triposr-smoke-macos",
                        "script": "asset_local.py",
                        "outputs": ["generated.png"],
                    }
                ],
                base_dir=TOOL_ROOT,
                tool_root=TOOL_ROOT,
            )

    def test_dry_run_plans_without_materializing_outputs(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            spec = root / "asset.json"
            spec.write_text(
                json.dumps(
                    {
                        "id": "dry_preprocess",
                        "assetType": "weapon",
                        "generator": {"profile": "triposr-smoke-macos"},
                        "preprocess": [
                            {
                                "strategy": "linear-terminal-detail",
                                "input": "source.png",
                                "output": "generated/detail.png",
                            }
                        ],
                    }
                ),
                encoding="utf-8",
            )
            with patch.dict(os.environ, {"CHARACTER_FACTORY_CACHE_ROOT": str(root / "cache")}):
                steps = prepare_spec_references(spec, TOOL_ROOT, dry_run=True)

        self.assertEqual(1, len(steps))
        self.assertFalse((root / "generated/detail.png").exists())

    def test_ordered_python_script_can_feed_terminal_detail(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / "source.jpg"
            source.write_bytes(b"source")
            script = root / "asset_local.py"
            script.write_text("print('fixture')\n", encoding="utf-8")
            spec = root / "asset.json"
            spec.write_text(
                json.dumps(
                    {
                        "id": "ordered_preprocess",
                        "assetType": "weapon",
                        "generator": {"profile": "triposr-smoke-macos"},
                        "preprocess": [
                            {
                                "strategy": "python-script",
                                "script": "asset_local.py",
                                "inputs": ["source.jpg"],
                                "arguments": ["--output", "generated/full.png"],
                                "outputs": ["generated/full.png"],
                                "affects": ["geometry", "details"],
                            },
                            {
                                "strategy": "linear-terminal-detail",
                                "input": "generated/full.png",
                                "output": "generated/detail.png",
                                "axis": "vertical",
                                "terminal": "min",
                            },
                        ],
                    }
                ),
                encoding="utf-8",
            )

            with patch.dict(os.environ, {"CHARACTER_FACTORY_CACHE_ROOT": str(root / "cache")}):
                steps = declared_preprocess_steps(spec, TOOL_ROOT)
                self.assertEqual(frozenset({"geometry", "details"}), steps[0].affects)
                self.assertIn(source.resolve(), steps[0].inputs)
                self.assertIn(script.resolve(), steps[0].inputs)
                python = Path(steps[0].python)
                python.parent.mkdir(parents=True, exist_ok=True)
                python.write_bytes(b"python")

                calls: list[list[str]] = []

                def fake_run(command, cwd, check):
                    calls.append(list(command))
                    if "asset_local.py" in str(command[1]):
                        output = root / "generated/full.png"
                    else:
                        output = root / "generated/detail.png"
                    output.parent.mkdir(parents=True, exist_ok=True)
                    output.write_bytes(b"image")
                    return SimpleNamespace(returncode=0)

                with patch("runtime.preprocess.subprocess.run", side_effect=fake_run):
                    result = prepare_spec_references(spec, TOOL_ROOT, dry_run=False)

        self.assertEqual(2, len(result))
        self.assertEqual(2, len(calls))
        self.assertIn("asset_local.py", calls[0][1])
        self.assertIn("prepare_linear_terminal_detail.py", calls[1][1])

    def test_catalogue_source_change_uses_declared_affects(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source, _script = self._write_catalogue_fixture(root)
            previous = catalogue_payload(root)
            source.write_bytes(b"source-v2")

            changes, removed = classify_changes(
                load_catalogue_entries(root),
                previous,
            )

        self.assertEqual([], removed)
        self.assertEqual(1, len(changes))
        self.assertEqual(frozenset({"geometry", "details"}), changes[0].kinds)

    def test_catalogue_preprocessor_code_change_uses_declared_affects(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            _source, script = self._write_catalogue_fixture(root)
            previous = catalogue_payload(root)
            script.write_text("print('v2')\n", encoding="utf-8")

            changes, removed = classify_changes(
                load_catalogue_entries(root),
                previous,
            )

        self.assertEqual([], removed)
        self.assertEqual(1, len(changes))
        self.assertEqual(frozenset({"geometry", "details"}), changes[0].kinds)

    def test_catalogue_ignores_materialization_of_derived_references(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self._write_catalogue_fixture(root)
            previous = catalogue_payload(root)
            asset = previous["assets"][0]
            self.assertEqual(
                "preprocess-derived",
                asset["referenceHashes"]["geometry"]["front"],
            )
            self.assertEqual(
                "preprocess-derived",
                asset["referenceHashes"]["details"]["ornament"],
            )

            generated = root / "generated"
            generated.mkdir(parents=True, exist_ok=True)
            (generated / "full.png").write_bytes(b"materialized-full")
            (generated / "detail.png").write_bytes(b"materialized-detail")

            current = catalogue_payload(root)
            changes, removed = classify_changes(
                load_catalogue_entries(root),
                previous,
            )

        self.assertEqual(
            previous["assets"][0]["referenceHashes"],
            current["assets"][0]["referenceHashes"],
        )
        self.assertEqual(previous["assets"][0]["preprocessHashes"], current["assets"][0]["preprocessHashes"])
        self.assertEqual([], changes)
        self.assertEqual([], removed)

    @staticmethod
    def _write_catalogue_fixture(root: Path) -> tuple[Path, Path]:
        source = root / "source.jpg"
        source.write_bytes(b"source-v1")
        script = root / "asset_local.py"
        script.write_text("print('v1')\n", encoding="utf-8")
        spec = root / "asset.json"
        spec.write_text(
            json.dumps(
                {
                    "id": "catalogued_staff",
                    "assetType": "weapon",
                    "preprocess": [
                        {
                            "strategy": "python-script",
                            "script": "asset_local.py",
                            "inputs": ["source.jpg"],
                            "arguments": ["--input", "source.jpg", "--output", "generated/full.png"],
                            "outputs": ["generated/full.png"],
                            "affects": ["geometry", "details"],
                        },
                        {
                            "strategy": "linear-terminal-detail",
                            "input": "generated/full.png",
                            "output": "generated/detail.png",
                            "axis": "vertical",
                            "terminal": "min",
                        },
                    ],
                    "views": {"front": "generated/full.png"},
                    "references": {
                        "details": {"ornament": "generated/detail.png"},
                    },
                    "outputDir": "out",
                    "generator": {"profile": "triposr-smoke-macos"},
                    "rigid": {
                        "blender": "/Applications/Blender.app/Contents/MacOS/Blender",
                        "composition": {
                            "strategy": "generated-detail-shaft",
                            "detailReference": "ornament",
                            "totalLength": 1.8,
                            "detailLength": 0.38,
                            "shaftRadius": 0.024,
                        },
                    },
                    "runtimePart": {
                        "slot": "MainHand",
                        "socketBoneName": "RightHand",
                    },
                }
            ),
            encoding="utf-8",
        )
        return source, script


if __name__ == "__main__":
    unittest.main()
