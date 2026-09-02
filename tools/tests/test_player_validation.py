import importlib.util
import json
import tempfile
import unittest
from pathlib import Path

SCRIPT = Path(__file__).resolve().parents[1] / "player-validation.py"
spec = importlib.util.spec_from_file_location("player_validation", SCRIPT)
runner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(runner)


class PlayerValidationTests(unittest.TestCase):
    def _scenario(self, capture_overrides=None):
        capture = {
            "width": 1600,
            "height": 900,
            "intervalSeconds": 10,
            "minimumFrames": 2,
        }
        capture.update(capture_overrides or {})
        return {
            "schemaVersion": 1,
            "runSeconds": 30,
            "capture": capture,
            "timeline": {},
            "assertions": {},
        }

    def _load(self, data):
        with tempfile.TemporaryDirectory() as td:
            path = Path(td) / "fixture.player-scenario.json"
            path.write_text(json.dumps(data), encoding="utf-8")
            return runner.load_scenario(path)

    def test_capture_evidence_after_is_generic_optional_metadata(self):
        self.assertEqual(self._load(self._scenario())["evidenceAfter"], 0)
        self.assertEqual(
            self._load(self._scenario({"evidenceAfterSeconds": 4.5}))["evidenceAfter"],
            4.5,
        )

    def test_capture_evidence_after_rejects_invalid_window(self):
        with self.assertRaises(SystemExit):
            self._load(self._scenario({"evidenceAfterSeconds": -1}))
        with self.assertRaises(SystemExit):
            self._load(self._scenario({"evidenceAfterSeconds": 30}))


if __name__ == "__main__":
    unittest.main()
