import importlib.util
import json
import tempfile
import unittest
from pathlib import Path
from unittest import mock

SCRIPT = Path(__file__).resolve().parents[1] / "run-module-validation.py"
spec = importlib.util.spec_from_file_location("run_module_validation_player_artifacts", SCRIPT)
runner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(runner)


class PlayerArtifactIsolationTests(unittest.TestCase):
    def test_same_module_player_targets_use_distinct_artifact_roots(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            plan_path = root / "plan.json"
            output = root / "out"
            plan_path.write_text(json.dumps({
                "tests": [],
                "playerValidations": [
                    {
                        "module": "Assets/Game/Composition/Kentridge/Playable",
                        "scene": "Assets/Game/Composition/Kentridge/Playable/Validation/MultiplayerSmoke.unity",
                        "scenario": "Assets/Game/Composition/Kentridge/Playable/Validation/MultiplayerSmoke.player-scenario.json",
                    },
                    {
                        "module": "Assets/Game/Composition/Kentridge/Playable",
                        "scene": "Assets/Game/Composition/Kentridge/Playable/Validation/Release/MultiplayerRehost.unity",
                        "scenario": "Assets/Game/Composition/Kentridge/Playable/Validation/Release/MultiplayerRehost.player-scenario.json",
                    },
                ],
            }), encoding="utf-8")
            outputs = []

            def fake_run(args, check, env):
                if args[:2] == ["python3", "tools/player-validation.py"]:
                    outputs.append(Path(args[args.index("--output") + 1]))
                return mock.Mock(returncode=0)

            with mock.patch.object(runner.subprocess, "run", side_effect=fake_run):
                result = runner.main([
                    "--unity", "/fake/unity",
                    "--plan", str(plan_path),
                    "--output", str(output),
                    "--source-sha", "a" * 40,
                ])

            self.assertEqual(result, 0)
            self.assertEqual(2, len(outputs))
            self.assertNotEqual(outputs[0], outputs[1])
            self.assertEqual(output / "Players", outputs[0].parent)
            self.assertEqual(output / "Players", outputs[1].parent)

            summary = json.loads((output / "module-validation-summary.json").read_text(encoding="utf-8"))
            roots = [Path(item["artifactRoot"]) for item in summary["players"]]
            self.assertEqual(outputs, roots)
            self.assertEqual(2, len(set(roots)))

    def test_long_artifact_key_is_bounded_and_stable(self):
        item = {
            "module": "Assets/" + "VeryLongModule/" * 20,
            "scene": "Assets/" + "Deep/" * 30 + "Scene.unity",
            "scenario": "Assets/" + "Deep/" * 30 + "Scene.player-scenario.json",
        }
        first = runner._player_artifact_key(item)
        second = runner._player_artifact_key(item)
        self.assertEqual(first, second)
        self.assertLessEqual(len(first), 174)


if __name__ == "__main__":
    unittest.main()
