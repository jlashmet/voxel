import importlib.util
import contextlib
import io
import json
import shutil
import tempfile
import unittest
from pathlib import Path
from unittest import mock

SCRIPT = Path(__file__).resolve().parents[1] / "run-module-validation.py"
spec = importlib.util.spec_from_file_location("module_player_evidence_runner", SCRIPT)
runner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(runner)


class ModulePlayerEvidenceTests(unittest.TestCase):
    def _execute(self, root, players):
        plan = root / "plan.json"
        plan.write_text(json.dumps({"playerValidations": players}), encoding="utf-8")
        paths = []

        def fake_run(args, check, env):
            self.assertTrue(check)
            self.assertEqual(env["VOXEL_DISABLE_GPU_CUTOVER"], "1")
            out = Path(args[args.index("--output") + 1])
            scene = args[args.index("--scene") + 1]
            scenario = args[args.index("--scenario") + 1]
            # Model the real capture helper's cleanup and fixed output filenames.
            shutil.rmtree(out / "Screenshots", ignore_errors=True)
            (out / "Screenshots").mkdir(parents=True)
            (out / "Screenshots" / "frame.png").write_text(scene, encoding="utf-8")
            (out / "player-run.log").write_text(scenario, encoding="utf-8")
            paths.append(out)

        with mock.patch.object(runner.subprocess, "run", side_effect=fake_run):
            with contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(runner.main([
                    "--unity", "/fake/unity", "--plan", str(plan),
                    "--output", str(root / "output"),
                ]), 0)
        return paths, json.loads((root / "output" / "module-validation-summary.json").read_text())

    def test_multiple_players_in_one_module_preserve_all_captures_and_logs(self):
        players = [
            {"module": "Assets/Rendering", "scene": "Assets/Rendering/Validation/FarWorld.unity", "scenario": "far.json"},
            {"module": "Assets/Rendering", "scene": "Assets/Rendering/Validation/Water.unity", "scenario": "water.json"},
        ]
        with tempfile.TemporaryDirectory() as td:
            paths, summary = self._execute(Path(td), players)
            self.assertEqual(len(set(paths)), 2)
            for item, out, recorded in zip(players, paths, summary["players"]):
                self.assertEqual((out / "Screenshots" / "frame.png").read_text(), item["scene"])
                self.assertEqual((out / "player-run.log").read_text(), item["scenario"])
                self.assertEqual(recorded["output"], str(out))

    def test_same_scene_different_scenarios_never_share_evidence(self):
        players = [
            {"module": "Assets/Module", "scene": "Assets/Module/Validation/Scene.unity", "scenario": "day.json"},
            {"module": "Assets/Module", "scene": "Assets/Module/Validation/Scene.unity", "scenario": "night.json"},
        ]
        with tempfile.TemporaryDirectory() as td:
            paths, _ = self._execute(Path(td), players)
            self.assertNotEqual(paths[0], paths[1])

    def test_identical_basenames_and_sanitization_collisions_are_distinct(self):
        players = [
            {"module": "Assets/A.B", "scene": "Assets/A.B/One/Test.unity", "scenario": "case.json"},
            {"module": "Assets/A_B", "scene": "Assets/A_B/One/Test.unity", "scenario": "case.json"},
            {"module": "Assets/A.B", "scene": "Assets/A.B/Two/Test.unity", "scenario": "case.json"},
        ]
        with tempfile.TemporaryDirectory() as td:
            paths, _ = self._execute(Path(td), players)
            self.assertEqual(len(set(paths)), 3)

    def test_output_paths_are_stable_when_plan_order_changes(self):
        players = [
            {"module": "Assets/Module", "scene": "Assets/Module/One.unity", "scenario": "one.json"},
            {"module": "Assets/Module", "scene": "Assets/Module/Two.unity", "scenario": "two.json"},
        ]
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            first, _ = self._execute(root, players)
            second, _ = self._execute(root, list(reversed(players)))
            self.assertEqual(first, list(reversed(second)))


if __name__ == "__main__":
    unittest.main()
