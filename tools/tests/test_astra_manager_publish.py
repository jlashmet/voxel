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

SPEC = importlib.util.spec_from_file_location("astra_manager_publish", TOOLS / "astra_manager_publish.py")
publish = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(publish)


def git(root: Path, *args: str) -> None:
    p = subprocess.run(["git", "-C", str(root), *args], text=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if p.returncode:
        raise AssertionError(p.stderr)


class PublisherTests(unittest.TestCase):
    def test_merge_method_prefers_merge_then_squash_then_rebase(self):
        self.assertEqual("--merge", publish.choose_merge_flag({"allow_merge_commit": True, "allow_squash_merge": True}))
        self.assertEqual("--squash", publish.choose_merge_flag({"allow_merge_commit": False, "allow_squash_merge": True}))
        self.assertEqual("--rebase", publish.choose_merge_flag({"allow_merge_commit": False, "allow_squash_merge": False, "allow_rebase_merge": True}))

    def test_only_new_manager_followup_sceneissues_are_publishable(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            git(root, "init", "-b", "master")
            git(root, "config", "user.email", "test@example.com")
            git(root, "config", "user.name", "Test")
            (root / "SceneIssues/open").mkdir(parents=True)
            (root / "SceneIssues/README.md").write_text("workflow\n")
            (root / "base.txt").write_text("base\n")
            git(root, "add", ".")
            git(root, "commit", "-m", "base")

            issue = root / "SceneIssues/open/20260905-120000-000-ManagerThing"
            issue.mkdir()
            (issue / "issue.json").write_text(json.dumps({"status": "open", "note": "MANAGER FOLLOW-UP / Thing"}))
            (issue / "plan.md").write_text("# plan\n")
            (issue / "tasks.md").write_text("- [ ] task\n")

            paths = publish.untracked_followups(root)
            self.assertEqual([issue], paths)


if __name__ == "__main__":
    unittest.main()
