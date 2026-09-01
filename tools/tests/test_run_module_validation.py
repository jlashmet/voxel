import importlib.util
import tempfile
import unittest
from pathlib import Path
from unittest import mock

SCRIPT = Path(__file__).resolve().parents[1] / "run-module-validation.py"
spec = importlib.util.spec_from_file_location("run_module_validation", SCRIPT)
runner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(runner)


class RunModuleValidationTests(unittest.TestCase):
    def _run_test_with_results(self, xml_text, platform="PlayMode", assembly="Water.Tests.PlayMode"):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            captured_args = []

            def fake_run(args, check, env):
                captured_args.extend(args)
                results = Path(args[args.index("-testResults") + 1])
                results.parent.mkdir(parents=True, exist_ok=True)
                results.write_text(xml_text, encoding="utf-8")
                return mock.Mock(returncode=0)

            item = {"module": "water", "platform": platform, "assembly": assembly}
            with mock.patch.object(runner.subprocess, "run", side_effect=fake_run):
                seconds = runner.run_test("/fake/unity", item, root)
            return seconds, captured_args

    def _run_persistent(self, edit=True, play=True, skipped=0):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            captured = {}

            def fake_run(args, check, env):
                captured["args"] = list(args)
                captured["env"] = dict(env)
                out = Path(env["VOXEL_CI_RESULTS_ROOT"])
                out.mkdir(parents=True, exist_ok=True)
                (out / "persistent-summary.txt").write_text(
                    "exit_code=0\nstatus=passed\nmessage=Persistent test phases passed.\n",
                    encoding="utf-8",
                )
                if edit:
                    (out / "persistent-editmode.txt").write_text(
                        f"passed=2\nfailed=0\nskipped={skipped}\ninconclusive=0\n",
                        encoding="utf-8",
                    )
                if play:
                    (out / "persistent-playmode.txt").write_text(
                        "passed=3\nfailed=0\nskipped=0\ninconclusive=0\n",
                        encoding="utf-8",
                    )
                return mock.Mock(returncode=0)

            items = []
            if edit:
                items.extend([
                    {"module": "terrain", "platform": "EditMode", "assembly": "Terrain.Tests.EditMode"},
                    {"module": "water", "platform": "EditMode", "assembly": "Water.Tests.EditMode"},
                ])
            if play:
                items.append(
                    {"module": "water", "platform": "PlayMode", "assembly": "Water.Tests.PlayMode"}
                )
            with mock.patch.object(runner.subprocess, "run", side_effect=fake_run):
                seconds = runner.run_persistent_tests("/fake/unity", items, root)
            return seconds, captured

    def test_required_module_test_rejects_zero_match(self):
        with self.assertRaises(SystemExit) as raised:
            self._run_test_with_results("<test-run />")
        self.assertIn("executed zero tests", str(raised.exception))

    def test_required_module_test_rejects_skipped_case(self):
        with self.assertRaises(SystemExit) as raised:
            self._run_test_with_results('<test-run><test-case result="Skipped" /></test-run>')
        self.assertIn("required module test assembly failed", str(raised.exception))

    def test_required_module_test_accepts_passed_case(self):
        seconds, _ = self._run_test_with_results('<test-run><test-case result="Passed" /></test-run>')
        self.assertGreaterEqual(seconds, 0)

    def test_editmode_module_tests_keep_graphics_device_available(self):
        _, args = self._run_test_with_results(
            '<test-run><test-case result="Passed" /></test-run>',
            platform="EditMode",
            assembly="VoxelEngine.Rendering.Tests.EditMode",
        )
        self.assertNotIn("-nographics", args)
        self.assertEqual(args[args.index("-testPlatform") + 1], "EditMode")
        self.assertEqual(args[args.index("-assemblyNames") + 1], "VoxelEngine.Rendering.Tests.EditMode")

    def test_compatible_editmode_and_playmode_share_one_persistent_editor(self):
        seconds, captured = self._run_persistent()
        self.assertGreaterEqual(seconds, 0)
        self.assertEqual(
            captured["env"]["VOXEL_CI_EDITMODE_ASSEMBLIES"],
            "Terrain.Tests.EditMode;Water.Tests.EditMode",
        )
        self.assertEqual(
            captured["env"]["VOXEL_CI_PLAYMODE_ASSEMBLIES"],
            "Water.Tests.PlayMode",
        )
        self.assertEqual(captured["env"]["VOXEL_CI_BAKE_SHOWCASE"], "0")
        self.assertIn("-executeMethod", captured["args"])
        self.assertEqual(
            captured["args"][captured["args"].index("-executeMethod") + 1],
            "VoxelCiPersistentTestRunner.Run",
        )
        self.assertNotIn("-runTests", captured["args"])
        self.assertNotIn("-nographics", captured["args"])

    def test_persistent_required_module_tests_reject_skips(self):
        with self.assertRaises(SystemExit) as raised:
            self._run_persistent(play=False, skipped=1)
        self.assertIn("did not all pass", str(raised.exception))
        self.assertIn("1 skipped", str(raised.exception))

    def test_known_native_allocation_suite_remains_process_isolated(self):
        self.assertIn("VoxelEngine.Tests.PlayMode", runner.PROCESS_ISOLATED_ASSEMBLIES)


if __name__ == "__main__":
    unittest.main()
