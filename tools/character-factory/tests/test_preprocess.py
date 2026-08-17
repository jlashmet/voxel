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
                                "arguments": ["--output", "generated/full.png"],
                                "outputs": ["generated/full.png"],
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


if __name__ == "__main__":
    unittest.main()
