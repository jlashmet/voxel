import importlib.util
import json
import tempfile
import unittest
from pathlib import Path
from unittest import mock

SCRIPT = Path(__file__).resolve().parents[1] / "run-module-validation.py"
spec = importlib.util.spec_from_file_location("run_module_validation_requested_coverage", SCRIPT)
runner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(runner)


class RequestedCoverageTests(unittest.TestCase):
    def test_exact_leaf_resolves_to_selected_owning_assembly(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            tests = root / "Assets" / "Game" / "Composition" / "Showcase" / "Tests" / "EditMode"
            tests.mkdir(parents=True)
            (tests / "Game.Composition.Showcase.Tests.EditMode.asmdef").write_text(
                json.dumps({"name": "Game.Composition.Showcase.Tests.EditMode"}), encoding="utf-8"
            )
            (tests / "ShowcaseStartupBakeArtifactTests.cs").write_text(
                "public sealed class ShowcaseStartupBakeArtifactTests { "
                "public void CurrentSourceBakeExportsPayloadAndMatchingManifest() {} }",
                encoding="utf-8",
            )
            selected = [{
                "module": "Assets/Game/Composition/Showcase",
                "platform": "EditMode",
                "assembly": "Game.Composition.Showcase.Tests.EditMode",
            }]
            requested = (
                "VoxelEngine.Showcase.Tests.EditMode.ShowcaseStartupBakeArtifactTests."
                "CurrentSourceBakeExportsPayloadAndMatchingManifest"
            )
            self.assertTrue(
                runner._requested_test_covered_by_selected_assembly(
                    requested, "EditMode", selected, project_root=root
                )
            )
            self.assertFalse(
                runner._requested_test_covered_by_selected_assembly(
                    requested, "PlayMode", selected, project_root=root
                )
            )

    def test_main_skips_only_redundant_requested_invocation_and_records_proof(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            plan = root / "plan.json"
            output = root / "out"
            plan.write_text(json.dumps({
                "tests": [{
                    "module": "Assets/Game/Composition/Showcase",
                    "platform": "EditMode",
                    "assembly": "Game.Composition.Showcase.Tests.EditMode",
                }],
                "playerValidations": [],
            }), encoding="utf-8")
            requested = (
                "VoxelEngine.Showcase.Tests.EditMode.ShowcaseStartupBakeArtifactTests."
                "CurrentSourceBakeExportsPayloadAndMatchingManifest"
            )
            with mock.patch.object(
                runner, "_requested_test_covered_by_selected_assembly", return_value=True
            ), mock.patch.object(runner, "run_persistent_tests", return_value=2.0) as run:
                result = runner.main([
                    "--unity", "/fake/unity",
                    "--plan", str(plan),
                    "--output", str(output),
                    "--requested-test", requested,
                    "--requested-platform", "EditMode",
                ])

            self.assertEqual(result, 0)
            self.assertEqual(run.call_args.kwargs["requested_test"], "")
            self.assertEqual(run.call_args.kwargs["requested_platform"], "")
            summary = json.loads((output / "module-validation-summary.json").read_text(encoding="utf-8"))
            self.assertEqual(summary["requestedTest"]["test"], requested)
            self.assertEqual(summary["requestedTest"]["execution"], "covered-by-module-assembly")


if __name__ == "__main__":
    unittest.main()
