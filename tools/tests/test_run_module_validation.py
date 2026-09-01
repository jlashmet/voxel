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
    def _run_test_with_results(self, xml_text):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)

            def fake_run(args, check, env):
                results = Path(args[args.index("-testResults") + 1])
                results.parent.mkdir(parents=True, exist_ok=True)
                results.write_text(xml_text, encoding="utf-8")
                return mock.Mock(returncode=0)

            item = {"module": "water", "platform": "PlayMode", "assembly": "Water.Tests.PlayMode"}
            with mock.patch.object(runner.subprocess, "run", side_effect=fake_run):
                return runner.run_test("/fake/unity", item, root)

    def test_required_module_test_rejects_zero_match(self):
        with self.assertRaises(SystemExit) as raised:
            self._run_test_with_results("<test-run />")
        self.assertIn("executed zero tests", str(raised.exception))

    def test_required_module_test_rejects_skipped_case(self):
        with self.assertRaises(SystemExit) as raised:
            self._run_test_with_results('<test-run><test-case result="Skipped" /></test-run>')
        self.assertIn("required module test assembly failed", str(raised.exception))

    def test_required_module_test_accepts_passed_case(self):
        seconds = self._run_test_with_results('<test-run><test-case result="Passed" /></test-run>')
        self.assertGreaterEqual(seconds, 0)


if __name__ == "__main__":
    unittest.main()
