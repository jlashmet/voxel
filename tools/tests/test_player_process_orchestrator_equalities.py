import importlib.util
import sys
import unittest
from pathlib import Path

SCRIPT = Path(__file__).resolve().parents[1] / "player_process_orchestrator.py"
spec = importlib.util.spec_from_file_location("player_process_orchestrator_equality_target", SCRIPT)
runner = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = runner
spec.loader.exec_module(runner)


class PlayerProcessMilestoneEqualityTests(unittest.TestCase):
    def _config(self):
        return {
            "mode": "multiProcess",
            "runSeconds": 60,
            "processes": [
                {"role": "authority", "headless": True},
                {"role": "client-a", "headless": True},
                {"role": "client-b", "headless": True},
            ],
            "operations": [
                {"op": "launch", "role": "authority"},
                {"op": "wait", "role": "authority", "name": "baseline-ready"},
            ],
            "assertions": {
                "equalMilestoneFields": [
                    {
                        "name": "baseline-ready",
                        "roles": ["authority", "client-a", "client-b"],
                        "fields": ["revision", "stateDigest"],
                    }
                ]
            },
        }

    def test_normalize_accepts_semantic_cross_role_equality(self):
        config = runner.normalize_config(self._config())
        self.assertEqual(len(config["equalities"]), 1)
        equality = config["equalities"][0]
        self.assertEqual(equality.name, "baseline-ready")
        self.assertEqual(equality.roles, ("authority", "client-a", "client-b"))
        self.assertEqual(equality.fields, ("revision", "stateDigest"))

    def test_normalize_rejects_unknown_or_harness_owned_equality_fields(self):
        config = self._config()
        config["assertions"]["equalMilestoneFields"][0]["roles"] = ["authority", "missing"]
        with self.assertRaises(runner.OrchestrationError):
            runner.normalize_config(config)

        config = self._config()
        config["assertions"]["equalMilestoneFields"][0]["fields"] = ["revision", "attempt"]
        with self.assertRaises(runner.OrchestrationError):
            runner.normalize_config(config)

    def test_equal_milestone_fields_returns_durable_semantic_proof(self):
        assertion = runner.MilestoneFieldEquality(
            "baseline-ready",
            ("authority", "client-a", "client-b"),
            ("revision", "stateDigest"),
        )
        history = [
            {"name": "baseline-ready", "role": "authority", "attempt": 1,
             "revision": "7", "stateDigest": "abc"},
            {"name": "baseline-ready", "role": "client-a", "attempt": 1,
             "revision": "7", "stateDigest": "abc"},
            {"name": "baseline-ready", "role": "client-b", "attempt": 1,
             "revision": "7", "stateDigest": "abc"},
        ]

        proof = runner._assert_equal_milestone_fields(history, [assertion])

        self.assertEqual(proof, [{
            "name": "baseline-ready",
            "roles": ["authority", "client-a", "client-b"],
            "fields": {"revision": "7", "stateDigest": "abc"},
        }])

    def test_equal_milestone_fields_rejects_semantic_divergence(self):
        assertion = runner.MilestoneFieldEquality(
            "baseline-ready",
            ("authority", "client-a", "client-b"),
            ("revision", "stateDigest"),
        )
        history = [
            {"name": "baseline-ready", "role": "authority", "revision": "7", "stateDigest": "abc"},
            {"name": "baseline-ready", "role": "client-a", "revision": "7", "stateDigest": "abc"},
            {"name": "baseline-ready", "role": "client-b", "revision": "8", "stateDigest": "abc"},
        ]

        with self.assertRaisesRegex(runner.OrchestrationError, "baseline-ready.revision mismatch"):
            runner._assert_equal_milestone_fields(history, [assertion])


if __name__ == "__main__":
    unittest.main()
