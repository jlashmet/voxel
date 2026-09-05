import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path

SCRIPT = Path(__file__).resolve().parents[1] / "player_process_orchestrator.py"
spec = importlib.util.spec_from_file_location("player_process_orchestrator_test_target", SCRIPT)
runner = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = runner
spec.loader.exec_module(runner)


class _RunningProcess:
    pid = 1234

    def poll(self):
        return None


class PlayerProcessOrchestratorTests(unittest.TestCase):
    def _config(self):
        return {
            "mode": "multiProcess",
            "runSeconds": 60,
            "processes": [
                {"role": "authority", "arguments": ["-host"], "headless": True},
                {"role": "client-a", "arguments": ["-join", "ABC123"], "headless": True},
            ],
            "operations": [
                {"op": "launch", "role": "authority"},
                {"op": "wait", "role": "authority", "name": "build-identity", "timeoutSeconds": 10},
                {"op": "launch", "role": "client-a"},
                {"op": "wait", "role": "client-a", "name": "gameplay-ready", "timeoutSeconds": 20,
                 "fields": {"session": "ABC123"}},
                {"op": "kill", "role": "client-a"},
                {"op": "relaunch", "role": "client-a"},
                {"op": "wait", "role": "client-a", "name": "gameplay-ready", "timeoutSeconds": 20,
                 "fields": {"reconnected": True}},
            ],
            "assertions": {},
        }

    def test_lifecycle_operations_are_semantic_and_role_driven(self):
        config = runner.normalize_config(self._config())
        self.assertEqual([role.name for role in config["roles"]], ["authority", "client-a"])
        self.assertEqual(
            [operation.op for operation in config["operations"]],
            ["launch", "wait", "launch", "wait", "kill", "relaunch", "wait"],
        )
        self.assertEqual(config["operations"][3].milestone.fields, {"session": "ABC123"})
        self.assertEqual(config["operations"][6].milestone.fields, {"reconnected": True})

    def test_operations_require_at_least_one_semantic_wait(self):
        config = self._config()
        config["operations"] = [{"op": "launch", "role": "authority"}]
        with self.assertRaises(runner.OrchestrationError):
            runner.normalize_config(config)

    def test_legacy_milestones_and_lifecycle_operations_are_not_mixed(self):
        config = self._config()
        config["milestones"] = [
            {"role": "authority", "name": "build-identity", "timeoutSeconds": 10}
        ]
        with self.assertRaises(runner.OrchestrationError):
            runner.normalize_config(config)

    def test_unknown_lifecycle_operation_fails_closed(self):
        config = self._config()
        config["operations"] = [{"op": "teleport-process", "role": "authority"}]
        with self.assertRaises(runner.OrchestrationError):
            runner.normalize_config(config)

    def test_role_state_is_isolated_but_stable_across_attempts(self):
        identity = {"sourceSha": "a" * 40, "executableSha256": "b" * 64}
        authority = runner.RoleSpec("authority", (), {}, True)
        client = runner.RoleSpec("client-a", (), {}, True)
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            authority_root, authority_env = runner.role_environment(root, authority, identity, {})
            client_root, client_env = runner.role_environment(root, client, identity, {})
            _, client_env_again = runner.role_environment(root, client, identity, {})
            self.assertNotEqual(authority_root, client_root)
            self.assertNotEqual(authority_env["HOME"], client_env["HOME"])
            self.assertEqual(client_env["VOXEL_VALIDATION_STATE_ROOT"],
                             client_env_again["VOXEL_VALIDATION_STATE_ROOT"])

    def test_wait_for_milestone_records_role_and_attempt(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            log = root / "player.log"
            log.write_text(
                "noise\n" + runner.MILESTONE_PREFIX
                + json.dumps({"name": "gameplay-ready", "session": "ABC123"}) + "\n",
                encoding="utf-8",
            )
            record = runner.RoleProcess(
                runner.RoleSpec("client-a", (), {}, True),
                _RunningProcess(), root, log, root / "stdout.log", root / "stderr.log", 2,
            )
            expected = runner.MilestoneExpectation("client-a", "gameplay-ready", 1, {"session": "ABC123"})
            history = []
            event = runner.wait_for_milestone(record, expected, history, sleep=lambda _: None)
            self.assertEqual(event["role"], "client-a")
            self.assertEqual(event["attempt"], 2)
            self.assertEqual(history, [event])


if __name__ == "__main__":
    unittest.main()