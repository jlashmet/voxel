import importlib.util
import json
import tempfile
import unittest
from pathlib import Path
from unittest import mock

SCRIPT = Path(__file__).resolve().parents[1] / "run-module-validation.py"
spec = importlib.util.spec_from_file_location("run_module_validation_source_sha", SCRIPT)
runner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(runner)


class RunModuleValidationSourceShaTests(unittest.TestCase):
    def test_exact_source_sha_is_forwarded_to_each_player_validation(self):
        source_sha = "a" * 40
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            plan = root / "plan.json"
            output = root / "out"
            plan.write_text(json.dumps({
                "playerValidations": [{
                    "module": "game-integration",
                    "scene": "Assets/Scenes/KentridgePlayableSlice.unity",
                    "scenario": "Assets/Scenes/Validation/kentridge.player-scenario.json",
                }]
            }), encoding="utf-8")
            captured = {}

            def fake_run(args, check, env):
                captured["args"] = list(args)
                return mock.Mock(returncode=0)

            with mock.patch.object(runner.subprocess, "run", side_effect=fake_run):
                result = runner.main([
                    "--unity", "/fake/unity",
                    "--plan", str(plan),
                    "--output", str(output),
                    "--source-sha", source_sha,
                ])

            self.assertEqual(result, 0)
            self.assertIn("--source-sha", captured["args"])
            self.assertEqual(captured["args"][captured["args"].index("--source-sha") + 1], source_sha)
            summary = json.loads((output / "module-validation-summary.json").read_text(encoding="utf-8"))
            self.assertEqual(summary["sourceSha"], source_sha)


if __name__ == "__main__":
    unittest.main()
