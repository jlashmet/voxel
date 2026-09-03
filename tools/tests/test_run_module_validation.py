import importlib.util
import json
import tempfile
import unittest
from pathlib import Path
from unittest import mock

SCRIPT = Path(__file__).resolve().parents[1] / "run-module-validation.py"
spec = importlib.util.spec_from_file_location("run_module_validation", SCRIPT)
runner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(runner)


class RunModuleValidationTests(unittest.TestCase):
    def _run_test_with_results(self, xml_text, platform="PlayMode", assembly="Water.Tests.PlayMode", test_filter=None):
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
                seconds = runner.run_test("/fake/unity", item, root, test_filter=test_filter)
            return seconds, captured_args

    def _run_persistent(self, edit=True, play=True, skipped_index=None, zero_index=None, requested_test=""):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            captured = {}

            items = []
            edit_assemblies = []
            play_assemblies = []
            if edit:
                edit_assemblies = ["Terrain.Tests.EditMode", "Water.Tests.EditMode"]
                items.extend([
                    {"module": "terrain", "platform": "EditMode", "assembly": edit_assemblies[0]},
                    {"module": "water", "platform": "EditMode", "assembly": edit_assemblies[1]},
                ])
            if play:
                play_assemblies = ["Water.Tests.PlayMode"]
                items.append({"module": "water", "platform": "PlayMode", "assembly": play_assemblies[0]})

            def fake_run(args, check, env):
                captured["args"] = list(args)
                captured["env"] = dict(env)
                out = Path(env["VOXEL_CI_RESULTS_ROOT"])
                out.mkdir(parents=True, exist_ok=True)
                (out / "persistent-summary.txt").write_text(
                    "exit_code=0\nstatus=passed\nmessage=Persistent test phases passed.\n",
                    encoding="utf-8",
                )
                flat_index = 0
                for platform, assemblies in (("editmode", edit_assemblies), ("playmode", play_assemblies)):
                    for index, assembly in enumerate(assemblies):
                        skipped = 1 if skipped_index == flat_index else 0
                        passed = 0 if zero_index == flat_index else 2
                        (out / f"persistent-{platform}-{index}.txt").write_text(
                            f"phase={platform}-{index}\nassembly={assembly}\npassed={passed}\nfailed=0\n"
                            f"skipped={skipped}\ninconclusive=0\n",
                            encoding="utf-8",
                        )
                        flat_index += 1
                if requested_test:
                    (out / "persistent-requested.txt").write_text(
                        f"phase=requested\ntest={requested_test}\npassed=1\nfailed=0\nskipped=0\ninconclusive=0\n",
                        encoding="utf-8",
                    )
                return mock.Mock(returncode=0)

            with mock.patch.object(runner.subprocess, "run", side_effect=fake_run):
                seconds = runner.run_persistent_tests(
                    "/fake/unity",
                    items,
                    root,
                    requested_test=requested_test,
                    requested_platform="EditMode" if requested_test else "",
                )
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
        self.assertEqual(captured["env"]["VOXEL_CI_EDITMODE_ASSEMBLIES"], "Terrain.Tests.EditMode;Water.Tests.EditMode")
        self.assertEqual(captured["env"]["VOXEL_CI_PLAYMODE_ASSEMBLIES"], "Water.Tests.PlayMode")
        self.assertEqual(captured["env"]["VOXEL_CI_PER_ASSEMBLY"], "1")
        self.assertEqual(captured["env"]["VOXEL_CI_BAKE_SHOWCASE"], "0")
        self.assertIn("-executeMethod", captured["args"])
        self.assertEqual(
            captured["args"][captured["args"].index("-executeMethod") + 1],
            "VoxelCiPersistentTestRunner.Run",
        )
        self.assertNotIn("-runTests", captured["args"])
        self.assertNotIn("-nographics", captured["args"])

    def test_each_required_persistent_assembly_must_execute_tests(self):
        with self.assertRaises(SystemExit) as raised:
            self._run_persistent(zero_index=1)
        self.assertIn("Water.Tests.EditMode", str(raised.exception))
        self.assertIn("executed zero tests", str(raised.exception))

    def test_persistent_required_module_tests_reject_skips_per_assembly(self):
        with self.assertRaises(SystemExit) as raised:
            self._run_persistent(play=False, skipped_index=1)
        self.assertIn("Water.Tests.EditMode", str(raised.exception))
        self.assertIn("1 skipped", str(raised.exception))

    def test_compatible_requested_test_runs_in_same_persistent_editor(self):
        requested = "Game.WorldBuilder.Tests.SecretDiscoveryTests.GeneratesClues"
        _, captured = self._run_persistent(play=False, requested_test=requested)
        self.assertEqual(captured["env"]["VOXEL_CI_REQUESTED_TEST"], requested)
        self.assertEqual(captured["env"]["VOXEL_CI_REQUESTED_PLATFORM"], "EditMode")

    def test_persistent_timeout_scales_with_selected_work(self):
        _, captured = self._run_persistent()
        self.assertEqual(captured["env"]["UNITY_MAX_MINUTES"], "12")

    def test_known_native_allocation_suite_remains_process_isolated(self):
        self.assertIn("VoxelEngine.Tests.PlayMode", runner.PROCESS_ISOLATED_ASSEMBLIES)
        self.assertTrue(runner._requested_is_process_isolated("VoxelEngine.Tests.PlayMode.SomeRegression"))
        self.assertFalse(runner._requested_is_process_isolated("VoxelEngine.Tests.Features.SomeRegression"))

    def test_isolated_requested_filter_is_forwarded(self):
        _, args = self._run_test_with_results(
            '<test-run><test-case result="Passed" /></test-run>',
            platform="PlayMode",
            assembly="VoxelEngine.Tests.PlayMode",
            test_filter="VoxelEngine.Tests.PlayMode.SomeRegression",
        )
        self.assertEqual(args[args.index("-testFilter") + 1], "VoxelEngine.Tests.PlayMode.SomeRegression")

    def test_player_validation_disables_gpu_cutover(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            plan = root / "plan.json"
            output = root / "out"
            plan.write_text(json.dumps({
                "playerValidations": [{
                    "module": "water",
                    "scene": "WaterDemo",
                    "scenario": "water",
                }]
            }), encoding="utf-8")
            captured = {}

            def fake_run(args, check, env):
                captured["args"] = list(args)
                captured["env"] = dict(env)
                return mock.Mock(returncode=0)

            with mock.patch.object(runner.subprocess, "run", side_effect=fake_run):
                result = runner.main([
                    "--unity", "/fake/unity",
                    "--plan", str(plan),
                    "--output", str(output),
                ])

            self.assertEqual(result, 0)
            self.assertEqual(captured["env"]["VOXEL_DISABLE_GPU_CUTOVER"], "1")
            self.assertEqual(captured["args"][:2], ["python3", "tools/player-validation.py"])


if __name__ == "__main__":
    unittest.main()
