"""Exercise real validation orchestration; replace only the external Unity processes."""
import contextlib
import importlib.util
import io
import json
import subprocess
import tempfile
import unittest
from pathlib import Path
from unittest import mock

SCRIPT = Path(__file__).resolve().parents[1] / "run-module-validation.py"
spec = importlib.util.spec_from_file_location("module_validation_isolation", SCRIPT)
runner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(runner)


class ModuleValidationIsolationTests(unittest.TestCase):
    MODULE_TESTS = [
        {"module": "first", "platform": "EditMode", "assembly": "First.Tests.EditMode"},
        {"module": "second", "platform": "PlayMode", "assembly": "Second.Tests.PlayMode"},
        {"module": "third", "platform": "PlayMode", "assembly": "Third.Tests.PlayMode"},
    ]
    PLAYER = {"module": "second", "scene": "SecondValidation", "scenario": "second-scenario"}

    def run_plan(self, requested_platform="PlayMode", requested="Example.MaterialModeTests",
                 isolated_results='<test-run><test-case result="Passed" /></test-run>',
                 process_error=False, reject_requested_only=False):
        """The doubles produce the same result contracts consumed by the real runner."""
        calls = []
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            plan = root / "plan.json"
            plan.write_text(json.dumps({"tests": self.MODULE_TESTS, "playerValidations": [self.PLAYER]}))

            def fake_run(args, check, env):
                self.assertTrue(check)
                calls.append((list(args), dict(env)))
                if "-executeMethod" in args:
                    out = Path(env["VOXEL_CI_RESULTS_ROOT"])
                    out.mkdir(parents=True, exist_ok=True)
                    (out / "persistent-summary.txt").write_text("exit_code=0\nstatus=passed\nmessage=passed\n")
                    for platform in ("editmode", "playmode"):
                        assemblies = env["VOXEL_CI_" + platform.upper() + "_ASSEMBLIES"].split(";")
                        for index, assembly in enumerate(filter(None, assemblies)):
                            (out / f"persistent-{platform}-{index}.txt").write_text(
                                f"assembly={assembly}\npassed=1\nfailed=0\nskipped=0\ninconclusive=0\n")
                    if env["VOXEL_CI_REQUESTED_TEST"]:
                        (out / "persistent-requested.txt").write_text(
                            f"test={env['VOXEL_CI_REQUESTED_TEST']}\npassed=1\nfailed=0\nskipped=0\ninconclusive=0\n")
                elif "-runTests" in args:
                    if process_error:
                        raise subprocess.CalledProcessError(2, args)
                    result = isolated_results
                    if reject_requested_only and "-testFilter" not in args:
                        result = '<test-run><test-case result="Passed" /></test-run>'
                    if result is not None:
                        xml = Path(args[args.index("-testResults") + 1])
                        xml.parent.mkdir(parents=True, exist_ok=True)
                        xml.write_text(result)
                else:
                    self.assertEqual(args[:2], ["python3", "tools/player-validation.py"])
                return mock.Mock(returncode=0)

            cli = ["--unity", "/fake/Unity", "--plan", str(plan), "--output", str(root / "out")]
            if requested:
                cli += ["--requested-test", requested, "--requested-platform", requested_platform]
            with mock.patch.object(runner.subprocess, "run", side_effect=fake_run), contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(runner.main(cli), 0)
            summary = json.loads((root / "out/module-validation-summary.json").read_text())
            return calls, summary

    def test_two_playmode_phases_never_share_the_persistent_editor(self):
        calls, summary = self.run_plan(requested_platform="EditMode")
        persistent = [(args, env) for args, env in calls if "-executeMethod" in args]
        self.assertEqual(len(persistent), 1)
        self.assertEqual(persistent[0][1]["VOXEL_CI_EDITMODE_ASSEMBLIES"], "First.Tests.EditMode")
        self.assertEqual(persistent[0][1]["VOXEL_CI_PLAYMODE_ASSEMBLIES"], "")
        self.assertEqual(persistent[0][1]["VOXEL_CI_REQUESTED_TEST"], "Example.MaterialModeTests")
        isolated = [(args, env) for args, env in calls if "-runTests" in args]
        self.assertEqual(len(isolated), 2)
        self.assertEqual([args[args.index("-assemblyNames") + 1] for args, _ in isolated],
                         ["Second.Tests.PlayMode", "Third.Tests.PlayMode"])
        self.assertEqual(summary["requestedTest"]["execution"], "persistent-editor")

    def test_requested_playmode_filter_is_isolated_without_guessing_its_assembly(self):
        calls, summary = self.run_plan()
        persistent = [env for args, env in calls if "-executeMethod" in args]
        self.assertEqual(persistent[0]["VOXEL_CI_PLAYMODE_ASSEMBLIES"], "")
        self.assertEqual(persistent[0]["VOXEL_CI_REQUESTED_TEST"], "")
        isolated = [(args, env) for args, env in calls if "-runTests" in args]
        self.assertEqual(len(isolated), 3)
        requested = [(args, env) for args, env in isolated if "-testFilter" in args]
        self.assertEqual(len(requested), 1)
        args, env = requested[0]
        self.assertEqual(args[args.index("-testFilter") + 1], "Example.MaterialModeTests")
        self.assertNotIn("-assemblyNames", args)
        self.assertEqual(args[args.index("-testPlatform") + 1], "PlayMode")
        self.assertEqual(summary["requestedTest"]["execution"], "isolated-editor")
        for args, env in isolated:
            self.assertEqual(args[0], "tools/unity-run.sh")
            self.assertEqual(env["UNITY_BIN"], "/fake/Unity")
            self.assertEqual(env["UNITY_MAX_MINUTES"], "4")
            self.assertEqual(env["VOXEL_DISABLE_GPU_CUTOVER"], "1")

    def test_routing_retains_every_module_test_and_discovered_player(self):
        calls, summary = self.run_plan()
        self.assertEqual([{key: row[key] for key in ("module", "platform", "assembly")}
                          for row in summary["tests"]], self.MODULE_TESTS)
        self.assertEqual([row["execution"] for row in summary["tests"]],
                         ["persistent-editor", "isolated-editor", "isolated-editor"])
        self.assertEqual([{key: row[key] for key in self.PLAYER} for row in summary["players"]], [self.PLAYER])
        args, env = calls[-1]
        self.assertEqual(args[args.index("--scene") + 1], self.PLAYER["scene"])
        self.assertEqual(args[args.index("--scenario") + 1], self.PLAYER["scenario"])

    def test_isolated_module_rejects_zero_match_skip_failure_and_missing_output(self):
        for result in ("<test-run />", '<test-run><test-case result="Skipped" /></test-run>',
                       '<test-run><test-case result="Failed" /></test-run>', None):
            with self.subTest(result=result), self.assertRaises(SystemExit):
                self.run_plan(isolated_results=result)

    def test_isolated_requested_test_rejects_zero_match_skip_failure_and_missing_output(self):
        for result in ("<test-run />", '<test-run><test-case result="Skipped" /></test-run>',
                       '<test-run><test-case result="Failed" /></test-run>', None):
            with self.subTest(result=result), self.assertRaises(SystemExit):
                self.run_plan(isolated_results=result, reject_requested_only=True)

    def test_isolated_module_process_failure_is_not_reported_as_success(self):
        with self.assertRaises(subprocess.CalledProcessError):
            self.run_plan(process_error=True)

    def test_unscoped_isolated_test_is_rejected_before_starting_unity(self):
        item = {"module": "requested", "platform": "PlayMode", "assembly": ""}
        with tempfile.TemporaryDirectory() as td, mock.patch.object(runner.subprocess, "run") as run:
            with self.assertRaises(SystemExit):
                runner.run_test("/fake/Unity", item, Path(td))
            run.assert_not_called()

    def test_stale_xml_cannot_satisfy_a_new_isolated_test(self):
        item = {"module": "second", "platform": "PlayMode", "assembly": "Second.Tests.PlayMode"}
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            xml = root / "Tests/second-PlayMode-Second_Tests_PlayMode/results.xml"
            xml.parent.mkdir(parents=True)
            xml.write_text('<test-run><test-case result="Passed" /></test-run>')
            with mock.patch.object(runner.subprocess, "run", return_value=mock.Mock(returncode=0)):
                with self.assertRaisesRegex(SystemExit, "produced no results"):
                    runner.run_test("/fake/Unity", item, root)


if __name__ == "__main__":
    unittest.main()
