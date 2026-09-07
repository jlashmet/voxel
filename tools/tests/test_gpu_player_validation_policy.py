import importlib.util
import json
import os
import tempfile
import unittest
from pathlib import Path
from unittest import mock

SCRIPT = Path(__file__).resolve().parents[1] / "player-validation.py"
spec = importlib.util.spec_from_file_location("gpu_player_validation_policy", SCRIPT)
runner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(runner)


class GpuPlayerValidationPolicyTests(unittest.TestCase):
    def scenario(self, policy="required"):
        return {
            "schemaVersion": 1, "runSeconds": 18, "gpuCutover": policy,
            "capture": {"width": 1600, "height": 900, "intervalSeconds": 2,
                        "minimumFrames": 6, "evidenceAfterSeconds": 2},
            "assertions": {"requiredLogPatterns": ["GPU success:"],
                           "forbiddenLogPatterns": ["GPU failure:"]},
        }

    def launch(self, policy, parent):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            scene = root / "fixture.unity"
            scenario = root / "fixture.player-scenario.json"
            scene.write_text("%YAML 1.1\n", encoding="utf-8")
            scenario.write_text(json.dumps(self.scenario(policy)), encoding="utf-8")
            captured = {}

            def launch_process(cmd, check, env=None):
                captured.update(command=cmd, check=check,
                                environment=dict(os.environ if env is None else env))

            with mock.patch.dict(os.environ, parent, clear=True), \
                 mock.patch.object(runner.subprocess, "run", side_effect=launch_process):
                self.assertEqual(runner.main([
                    "--unity", "/fake/unity", "--scene", str(scene),
                    "--scenario", str(scenario), "--output", str(root / "out")]), 0)
                self.assertEqual(dict(os.environ), parent, "Child policy must not mutate parent state")
            return captured

    def test_required_gpu_ignores_inherited_cpu_force(self):
        child = self.launch("required", {"VOXEL_DISABLE_GPU_CUTOVER": "1", "KEEP": "value"})
        self.assertNotIn("VOXEL_DISABLE_GPU_CUTOVER", child["environment"])
        self.assertEqual(child["environment"]["KEEP"], "value")
        self.assertTrue(child["check"])
        self.assertEqual(child["command"][:2], ["bash", "tools/showcase-player-capture.sh"])
        self.assertIn("GPU success:", child["command"])
        self.assertIn("GPU failure:", child["command"])

    def test_inherit_preserves_explicit_diagnostic_choice(self):
        child = self.launch("inherit", {"VOXEL_DISABLE_GPU_CUTOVER": "1"})
        self.assertEqual(child["environment"]["VOXEL_DISABLE_GPU_CUTOVER"], "1")

    def test_required_then_inherit_does_not_leak_policy(self):
        parent = {"VOXEL_DISABLE_GPU_CUTOVER": "1"}
        self.assertNotIn("VOXEL_DISABLE_GPU_CUTOVER", self.launch("required", parent)["environment"])
        self.assertEqual(self.launch("inherit", parent)["environment"], parent)

    def test_required_survives_scenario_loading(self):
        with tempfile.TemporaryDirectory() as td:
            path = Path(td) / "fixture.player-scenario.json"
            path.write_text(json.dumps(self.scenario()), encoding="utf-8")
            self.assertEqual(runner.load_scenario(path).get("gpuCutover"), "required")

    def test_unknown_policy_fails_instead_of_silently_running_cpu(self):
        with tempfile.TemporaryDirectory() as td:
            path = Path(td) / "fixture.player-scenario.json"
            path.write_text(json.dumps(self.scenario("requird")), encoding="utf-8")
            with self.assertRaisesRegex(SystemExit, "gpuCutover"):
                runner.load_scenario(path)


if __name__ == "__main__":
    unittest.main()
