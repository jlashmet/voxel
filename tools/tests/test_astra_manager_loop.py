import importlib.util
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

TOOLS = Path(__file__).resolve().parents[1]
if str(TOOLS) not in sys.path:
    sys.path.insert(0, str(TOOLS))

SPEC = importlib.util.spec_from_file_location("astra_manager_loop", TOOLS / "astra_manager_loop.py")
loop = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(loop)


def git(root: Path, *args: str) -> str:
    p = subprocess.run(["git", "-C", str(root), *args], text=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if p.returncode:
        raise AssertionError(p.stderr)
    return p.stdout.strip()


class ReviewWindowBudgetTests(unittest.TestCase):
    def test_budget_selects_suspicious_first_and_never_spills_suspicious_into_routine(self):
        pending = [
            {"key": "r1", "priority": "routine"},
            {"key": "s1", "priority": "suspicious"},
            {"key": "r2", "priority": "routine"},
            {"key": "s2", "priority": "suspicious"},
            {"key": "s3", "priority": "suspicious"},
            {"key": "r3", "priority": "routine"},
        ]
        selected = loop.select_review_window(
            pending,
            {"suspiciousItems": 2, "routineCompletions": 4, "deepInvestigations": 1},
        )
        self.assertEqual(["s1", "s2", "r1", "r2", "r3"], [item["key"] for item in selected])
        self.assertNotIn("s3", [item["key"] for item in selected])

    def test_zero_budget_exposes_no_backlog_items(self):
        pending = [{"key": "s1", "priority": "suspicious"}, {"key": "r1", "priority": "routine"}]
        self.assertEqual([], loop.select_review_window(pending, {"suspiciousItems": 0, "routineCompletions": 0}))

    def test_sync_fast_forwards_and_preserves_then_reconciles_published_followup(self):
        with tempfile.TemporaryDirectory() as tmp:
            base = Path(tmp)
            remote = base / "remote.git"
            seed = base / "seed"
            manager = base / "manager"
            subprocess.run(["git", "init", "--bare", str(remote)], check=True, stdout=subprocess.DEVNULL)
            seed.mkdir()
            git(seed, "init", "-b", "master")
            git(seed, "config", "user.email", "test@example.com")
            git(seed, "config", "user.name", "Test")
            (seed / "SceneIssues/open").mkdir(parents=True)
            (seed / "SceneIssues/manager").mkdir(parents=True)
            (seed / "SceneIssues/README.md").write_text("workflow\n")
            (seed / ".gitignore").write_text("/SceneIssues/manager/runtime/\n")
            (seed / "base.txt").write_text("base\n")
            git(seed, "add", ".")
            git(seed, "commit", "-m", "base")
            git(seed, "remote", "add", "origin", str(remote))
            git(seed, "push", "-u", "origin", "master")
            subprocess.run(["git", "clone", "-b", "master", str(remote), str(manager)], check=True, stdout=subprocess.DEVNULL)

            issue_id = "20260905-120000-000-ManagerFollowup"
            local_issue = manager / "SceneIssues/open" / issue_id
            local_issue.mkdir(parents=True)
            issue_json = {"status": "open", "note": "MANAGER FOLLOW-UP / Followup"}
            (local_issue / "issue.json").write_text(json.dumps(issue_json))
            (local_issue / "plan.md").write_text("# plan\n")
            (local_issue / "tasks.md").write_text("- [ ] task\n")

            (seed / "second.txt").write_text("second\n")
            git(seed, "add", ".")
            git(seed, "commit", "-m", "second")
            git(seed, "push", "origin", "master")

            loop.core.fetch(manager)
            loop.sync_master_worktree(manager, Path("SceneIssues/manager/runtime"))
            self.assertTrue((manager / "second.txt").exists())
            self.assertTrue(local_issue.exists())

            remote_issue = seed / "SceneIssues/open" / issue_id
            remote_issue.mkdir(parents=True)
            (remote_issue / "issue.json").write_text(json.dumps(issue_json))
            (remote_issue / "plan.md").write_text("# plan\n")
            (remote_issue / "tasks.md").write_text("- [ ] task\n")
            git(seed, "add", ".")
            git(seed, "commit", "-m", "merge manager followup")
            git(seed, "push", "origin", "master")

            loop.core.fetch(manager)
            loop.sync_master_worktree(manager, Path("SceneIssues/manager/runtime"))
            self.assertTrue(local_issue.exists())
            self.assertEqual("", git(manager, "status", "--porcelain"))
            self.assertEqual(git(manager, "rev-parse", "origin/master"), git(manager, "rev-parse", "HEAD"))


if __name__ == "__main__":
    unittest.main()
