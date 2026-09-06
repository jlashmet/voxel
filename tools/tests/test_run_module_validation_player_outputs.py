import importlib.util
import json
import tempfile
import unittest
from pathlib import Path
from unittest import mock

SCRIPT = Path(__file__).resolve().parents[1] / "run-module-validation.py"
spec = importlib.util.spec_from_file_location("run_module_validation_player_outputs", SCRIPT)
runner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(runner)


class PlayerOutputIsolationTests(unittest.TestCase):
    def test_same_module_player_scenes_get_distinct_artifact_roots(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            plan = root / "plan.json"
            output = root / "out"
            players = [
                {
                    "module": "Assets/Game/Composition/Showcase/SceneRuntime",
                    "scene": "Assets/Game/Composition/Showcase/SceneRuntime/Validation/PropShowcaseMaterialValidation.unity",
                    "scenario": "prop.json",
                },
                {
                    "module": "Assets/Game/Composition/Showcase/SceneRuntime",
                    "scene": "Assets/Game/Composition/Showcase/SceneRuntime/Validation/ShowcaseInputRuntimeValidation.unity",
                    "scenario": "input.json",
                },
            ]
            plan.write_text(json.dumps({"tests": [], "playerValidations": players}), encoding="utf-8")
            outputs = []

            def fake_run(args, check, env):
                self.assertTrue(check)
                self.assertEqual(args[:2], ["python3", "tools/player-validation.py"])
                outputs.append(Path(args[args.index("--output") + 1]))
                return mock.Mock(returncode=0)

            with mock.patch.object(runner.subprocess, "run", side_effect=fake_run):
                self.assertEqual(runner.main([
                    "--unity", "/fake/unity",
                    "--plan", str(plan),
                    "--output", str(output),
                ]), 0)

            self.assertEqual(len(outputs), 2)
            self.assertNotEqual(outputs[0], outputs[1])
            self.assertTrue(all(path.parent.name == "Players" for path in outputs))
            self.assertIn("PropShowcaseMaterialValidation_unity", outputs[0].name)
            self.assertIn("ShowcaseInputRuntimeValidation_unity", outputs[1].name)

    def test_output_root_is_deterministic_for_same_target(self):
        root = Path("Artifacts")
        item = {
            "module": "Assets/Game/Structures",
            "scene": "Assets/Game/Structures/Validation/PropShowcaseProductionValidation.unity",
        }
        self.assertEqual(
            runner._player_output_root(root, item),
            runner._player_output_root(root, dict(item)),
        )


if __name__ == "__main__":
    unittest.main()
