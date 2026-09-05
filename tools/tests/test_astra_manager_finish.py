import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path

TOOLS = Path(__file__).resolve().parents[1]
if str(TOOLS) not in sys.path:
    sys.path.insert(0, str(TOOLS))

SPEC = importlib.util.spec_from_file_location("astra_manager_finish", TOOLS / "astra_manager_finish.py")
finish = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(finish)


class FinishValidationTests(unittest.TestCase):
    def _root(self, tmp: str) -> tuple[Path, Path]:
        root = Path(tmp)
        runtime = Path("SceneIssues/manager/runtime")
        (root / runtime).mkdir(parents=True)
        (root / "SceneIssues/README.md").write_text("workflow\n")
        (root / runtime / "signal.json").write_text(json.dumps({
            "masterSha": "abc",
            "selectedReviewKeys": ["selected-1", "selected-2"],
        }))
        return root, runtime

    def test_rejects_review_key_not_exposed_by_window(self):
        with tempfile.TemporaryDirectory() as tmp:
            root, runtime = self._root(tmp)
            decision = root / runtime / "decision.json"
            decision.write_text(json.dumps({
                "reviewedMasterSha": "abc",
                "reviewedItems": [{"key": "hidden-backlog-key", "result": "accepted"}],
                "followups": [],
            }))
            with self.assertRaises(finish.core.ManagerError):
                finish.validate_decision(root, runtime, decision)

    def test_followup_requires_selected_followup_created_result(self):
        with tempfile.TemporaryDirectory() as tmp:
            root, runtime = self._root(tmp)
            decision = root / runtime / "decision.json"
            decision.write_text(json.dumps({
                "reviewedMasterSha": "abc",
                "reviewedItems": [{"key": "selected-1", "result": "accepted"}],
                "followups": [{"title": "unexpected"}],
            }))
            with self.assertRaises(finish.core.ManagerError):
                finish.validate_decision(root, runtime, decision)

    def test_valid_bounded_decision_passes(self):
        with tempfile.TemporaryDirectory() as tmp:
            root, runtime = self._root(tmp)
            decision = root / runtime / "decision.json"
            value = {
                "reviewedMasterSha": "abc",
                "reviewedItems": [{"key": "selected-2", "result": "deferred"}],
                "followups": [],
            }
            decision.write_text(json.dumps(value))
            self.assertEqual(value, finish.validate_decision(root, runtime, decision))


if __name__ == "__main__":
    unittest.main()
