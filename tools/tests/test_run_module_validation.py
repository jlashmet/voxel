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


if __name__ == "__main__":
    unittest.main()