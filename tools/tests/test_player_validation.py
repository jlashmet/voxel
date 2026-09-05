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

    def _multi_scenario(self):
        return {
            "schemaVersion": 1,
            "mode": "multiProcess",
            "runSeconds": 30,
            "processes": [
                {"role": "authority", "arguments": ["-validation-host"], "headless": True},
                {"role": "client-a", "arguments": ["-validation-join", "ABC123"], "headless": True},
            ],
            "milestones": [
                {"role": "authority", "name": "build-identity", "timeoutSeconds": 10},
                {"role": "client-a", "name": "gameplay-ready", "timeoutSeconds": 20,
                 "fields": {"session": "ABC123"}},
            ],
            "assertions": {
                "requiredLogPatterns": ["GAMEPLAY_READY"],
                "forbiddenLogPatterns": ["AUTHORITY_MISMATCH"],
            },
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

    def test_multi_process_scenario_uses_shared_orchestrator_schema(self):
        loaded = self._load(self._multi_scenario())
        self.assertEqual(loaded["mode"], "multiProcess")
        config = loaded["config"]
        self.assertEqual([role.name for role in config["roles"]], ["authority", "client-a"])
        self.assertEqual(config["milestones"][1].name, "gameplay-ready")
        self.assertEqual(config["milestones"][1].fields, {"session": "ABC123"})
        self.assertEqual(config["forbidden"], ["AUTHORITY_MISMATCH"])

    def test_multi_process_scenario_rejects_duplicate_role(self):
        scenario = self._multi_scenario()
        scenario["processes"].append({"role": "client-a"})
        with self.assertRaises(SystemExit):
            self._load(scenario)

    def test_unknown_mode_is_rejected_instead_of_falling_back_to_capture(self):
        scenario = self._scenario()
        scenario["mode"] = "networkMaybe"
        with self.assertRaises(SystemExit):
            self._load(scenario)


if __name__ == "__main__":
    unittest.main()