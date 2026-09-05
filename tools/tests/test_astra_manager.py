import importlib.util
import json
import subprocess
import tempfile
import unittest
from pathlib import Path

MODULE = Path(__file__).resolve().parents[1] / "astra_manager.py"
SPEC = importlib.util.spec_from_file_location("astra_manager", MODULE)
am = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(am)


def git(root: Path, *args: str) -> str:
    p = subprocess.run(["git", "-C", str(root), *args], text=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if p.returncode:
        raise AssertionError(p.stderr)
    return p.stdout.strip()


class AstraManagerTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        git(self.root, "init", "-b", "master")
        git(self.root, "config", "user.email", "test@example.com")
        git(self.root, "config", "user.name", "Test")
        (self.root / "SceneIssues/open").mkdir(parents=True)
        (self.root / "SceneIssues/closed").mkdir(parents=True)
        (self.root / "SceneIssues/manager").mkdir(parents=True)
        (self.root / "SceneIssues/README.md").write_text("workflow\n")
        self.cfg = {
            "batchHours": 5,
            "staleAgentHours": 12,
            "repeatedCiFailureCount": 3,
            "largeDiffFileCount": 30,
            "reviewBudget": {"routineCompletions": 5, "suspiciousItems": 2, "deepInvestigations": 1},
            "corePathPatterns": ["^Assets/.*Renderer", "^Assets/.*WorldBuilder"],
        }
        (self.root / "SceneIssues/manager/config.json").write_text(json.dumps(self.cfg))
        (self.root / "README.txt").write_text("base")
        git(self.root, "add", ".")
        git(self.root, "commit", "-m", "base")
        self.runtime = Path("SceneIssues/manager/runtime")

    def tearDown(self):
        self.tmp.cleanup()

    def make_issue(self, queue: str, issue_id: str, status: str = "open", note: str = "acceptance", resolution: str = "") -> Path:
        path = self.root / "SceneIssues" / queue / issue_id
        path.mkdir(parents=True, exist_ok=True)
        data = {
            "formatVersion": 3,
            "id": issue_id,
            "capturedUtc": "2026-09-05T00:00:00Z",
            "note": note,
            "status": status,
            "resolvedUtc": "2026-09-05T01:00:00Z" if status == "fixed" else "",
            "resolutionSummary": resolution,
            "regressionTest": "Tests.Foo",
            "fixCommit": "",
            "unityVersion": "6000.5.6f1",
            "platform": "Feature",
            "sceneName": "",
            "scenePath": "",
            "sceneBuildIndex": -1,
            "captures": [],
        }
        (path / "issue.json").write_text(json.dumps(data, indent=2))
        (path / "plan.md").write_text("# plan\n")
        (path / "tasks.md").write_text("- [x] one\n- [ ] two\n" if status == "open" else "- [x] one\n- [x] two\n")
        return path

    def test_bootstrap_and_no_change_do_not_need_astra(self):
        first = am.collect(self.root, self.runtime, self.cfg, "x/y", False)
        self.assertTrue(first["bootstrap"])
        self.assertFalse(first["wakeAstra"])
        second = am.collect(self.root, self.runtime, self.cfg, "x/y", False)
        self.assertFalse(second["managerReviewRequired"])
        self.assertEqual([], am.state(self.root, self.runtime)["pendingReviews"])

    def test_closed_issue_queues_selective_packet(self):
        am.collect(self.root, self.runtime, self.cfg, "x/y", False)
        issue_id = "20260905-120000-000-DoneThing"
        self.make_issue("closed", issue_id, status="fixed", resolution="fixed it")
        (self.root / "Assets").mkdir()
        (self.root / "Assets/FooRenderer.cs").write_text("class X {}")
        git(self.root, "add", ".")
        git(self.root, "commit", "-m", "close issue")
        signal = am.collect(self.root, self.runtime, self.cfg, "x/y", False)
        self.assertTrue(signal["wakeAstra"])
        pending = [x for x in am.state(self.root, self.runtime)["pendingReviews"] if x["kind"] == "completion"]
        self.assertEqual(1, len(pending))
        self.assertTrue((self.root / pending[0]["packet"]).exists())
        self.assertEqual("suspicious", pending[0]["priority"])

    def test_routine_task_progress_does_not_wake_astra(self):
        issue_id = "20260905-120000-000-Working"
        path = self.make_issue("open", issue_id)
        git(self.root, "add", ".")
        git(self.root, "commit", "-m", "add issue")
        am.collect(self.root, self.runtime, self.cfg, "x/y", False)
        (path / "tasks.md").write_text("- [x] one\n- [x] two\n")
        git(self.root, "add", ".")
        git(self.root, "commit", "-m", "progress")
        signal = am.collect(self.root, self.runtime, self.cfg, "x/y", False)
        self.assertFalse(signal["managerReviewRequired"])

    def test_acceptance_change_wakes_astra(self):
        issue_id = "20260905-120000-000-Working"
        path = self.make_issue("open", issue_id, note="first acceptance")
        git(self.root, "add", ".")
        git(self.root, "commit", "-m", "add issue")
        am.collect(self.root, self.runtime, self.cfg, "x/y", False)
        data = json.loads((path / "issue.json").read_text())
        data["note"] = "changed acceptance"
        (path / "issue.json").write_text(json.dumps(data, indent=2))
        git(self.root, "add", ".")
        git(self.root, "commit", "-m", "change acceptance")
        signal = am.collect(self.root, self.runtime, self.cfg, "x/y", False)
        self.assertTrue(signal["wakeAstra"])
        self.assertTrue(any(x["kind"] == "acceptance-change" for x in am.state(self.root, self.runtime)["pendingReviews"]))

    def test_apply_decision_creates_standard_followup_and_advances_cursor(self):
        am.collect(self.root, self.runtime, self.cfg, "x/y", False)
        origin = "20260905-120000-000-DoneThing"
        self.make_issue("closed", origin, status="fixed", resolution="fixed it")
        git(self.root, "add", ".")
        git(self.root, "commit", "-m", "close issue")
        signal = am.collect(self.root, self.runtime, self.cfg, "x/y", False)
        key = next(x["key"] for x in am.state(self.root, self.runtime)["pendingReviews"] if x["kind"] == "completion")
        decision = {
            "reviewedMasterSha": signal["masterSha"],
            "reviewedItems": [{"key": key, "result": "follow-up-created", "note": "concrete gap"}],
            "followups": [{
                "title": "Renderer Boundary Regression",
                "originIssue": origin,
                "originSha": signal["masterSha"],
                "evidence": "review packet shows a shared boundary changed without the required invariant",
                "problem": "shared renderer accepts scene-specific policy",
                "impact": "independent scenes can regress",
                "expectedBehavior": "renderer remains semantic and scene agnostic",
                "acceptanceCriteria": ["scene-specific policy is removed", "an independent consumer regression passes"],
                "relevantPaths": ["Assets/FooRenderer.cs"],
            }],
            "unresolvedQuestions": [],
        }
        decision_path = self.root / self.runtime / "decision.json"
        decision_path.write_text(json.dumps(decision))
        result = am.apply(self.root, self.runtime, decision_path)
        self.assertEqual(1, len(result["createdSceneIssues"]))
        created = self.root / "SceneIssues/open" / result["createdSceneIssues"][0]
        self.assertTrue((created / "issue.json").exists())
        self.assertTrue((created / "plan.md").exists())
        self.assertTrue((created / "tasks.md").exists())
        final = am.state(self.root, self.runtime)
        self.assertEqual([], final["pendingReviews"])
        self.assertEqual(signal["masterSha"], final["lastReviewedMasterSha"])


if __name__ == "__main__":
    unittest.main()
