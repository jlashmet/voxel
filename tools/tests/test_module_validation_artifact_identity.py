"""A module's second player must not overwrite the first player's proof."""
import contextlib
import importlib.util
import io
import json
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest
from unittest import mock

RUNNER_PATH = Path(__file__).resolve().parents[1] / "run-module-validation.py"
SPEC = importlib.util.spec_from_file_location("module_validation_artifact_runner", RUNNER_PATH)
RUNNER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(RUNNER)


class PlayerArtifactIdentityTests(unittest.TestCase):
    def run_plan(self, root, items):
        plan = root / "plan.json"
        plan.write_text(json.dumps({"tests": [], "playerValidations": items}), encoding="utf-8")
        outputs = []

        def player_process(args, **kwargs):
            scene = args[args.index("--scene") + 1]
            scenario = args[args.index("--scenario") + 1]
            out = Path(args[args.index("--output") + 1])
            # Simulate the standalone capture process replacing its own output files.
            shutil.rmtree(out, ignore_errors=True)
            (out / "Screenshots").mkdir(parents=True)
            identity = json.dumps([scene, scenario])
            (out / "player-run.log").write_text(identity, encoding="utf-8")
            (out / "Screenshots" / "frame.png").write_bytes(identity.encode("utf-8"))
            outputs.append(out)
            return subprocess.CompletedProcess(args, 0)

        with mock.patch.object(RUNNER.subprocess, "run", side_effect=player_process), contextlib.redirect_stdout(io.StringIO()):
            result = RUNNER.main(["--unity", "unused-unity", "--plan", str(plan), "--output", str(root / "results")])
        self.assertEqual(0, result)
        self.assertEqual(len(items), len(set(outputs)), "Player targets shared an artifact directory")
        summary = json.loads((root / "results" / "module-validation-summary.json").read_text(encoding="utf-8"))
        for item, out, recorded in zip(items, outputs, summary["players"]):
            identity = json.dumps([item["scene"], item["scenario"]])
            self.assertEqual(identity, (out / "player-run.log").read_text(encoding="utf-8"))
            self.assertEqual(identity.encode("utf-8"), (out / "Screenshots" / "frame.png").read_bytes())
            self.assertEqual(str(out), recorded["output"])
        return outputs

    def test_two_scenes_in_one_module_retain_both_artifacts(self):
        module = "Assets/VoxelEngine/Rendering"
        items = [dict(module=module, scene=f"{module}/Validation/{name}/{name}Demo.unity",
                      scenario=f"{module}/Validation/{name}/{name}Demo.player-scenario.json")
                 for name in ("FarWorld", "Water")]
        with tempfile.TemporaryDirectory() as directory:
            self.run_plan(Path(directory), items)

    def test_same_scene_with_two_scenarios_retains_both_artifacts(self):
        items = [dict(module="Assets/Module", scene="Assets/Module/Validation/Demo.unity",
                      scenario=f"Assets/Module/Validation/{name}.player-scenario.json")
                 for name in ("stationary", "traversal")]
        with tempfile.TemporaryDirectory() as directory:
            self.run_plan(Path(directory), items)

    def test_same_basename_and_sanitized_module_do_not_collide(self):
        items = [dict(module=module, scene=f"{module}/Validation/Demo.unity",
                      scenario=f"{module}/Validation/Demo.player-scenario.json")
                 for module in ("Assets/A_B", "Assets/A/B")]
        with tempfile.TemporaryDirectory() as directory:
            self.run_plan(Path(directory), items)

    def test_identity_does_not_depend_on_plan_order(self):
        items = [dict(module="Assets/Module", scene=f"Assets/Module/{name}.unity",
                      scenario=f"Assets/Module/{name}.player-scenario.json")
                 for name in ("first", "second")]
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            original = self.run_plan(root, items)
            reversed_outputs = self.run_plan(root, list(reversed(items)))
            self.assertEqual(original, list(reversed(reversed_outputs)))

    def test_failed_player_is_not_reported_as_success(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            plan = root / "plan.json"
            plan.write_text(json.dumps({"playerValidations": [dict(module="Assets/Module",
                scene="Assets/Module/Demo.unity", scenario="Assets/Module/Demo.player-scenario.json")]}), encoding="utf-8")
            with mock.patch.object(RUNNER.subprocess, "run", side_effect=subprocess.CalledProcessError(1, "player")):
                with self.assertRaises(subprocess.CalledProcessError):
                    RUNNER.main(["--unity", "unused", "--plan", str(plan), "--output", str(root / "results")])
            self.assertFalse((root / "results" / "module-validation-summary.json").exists())


if __name__ == "__main__":
    unittest.main()
