import importlib.util
import json
import os
import stat
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

TOOLS = Path(__file__).resolve().parents[1]
if str(TOOLS) not in sys.path:
    sys.path.insert(0, str(TOOLS))

SPEC = importlib.util.spec_from_file_location("astra_manager_codex", TOOLS / "astra_manager_codex.py")
codex = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(codex)


class CodexLauncherTests(unittest.TestCase):
    def test_parses_and_enforces_astra_minimum_version(self):
        self.assertEqual((0, 153, 0), codex.parse_version("codex-cli 0.153.0"))
        self.assertEqual((1, 2, 3), codex.parse_version("codex 1.2.3-beta"))

    def test_command_is_ephemeral_astra_low_reasoning_and_offline(self):
        command = codex.build_command({}, "/usr/local/bin/codex")
        self.assertEqual("/usr/local/bin/codex", command[0])
        self.assertEqual("exec", command[1])
        self.assertIn("--ephemeral", command)
        self.assertIn("--ignore-user-config", command)
        self.assertIn("gpt-6-astra", command)
        self.assertIn('model_reasoning_effort="low"', command)
        self.assertIn('approval_policy="never"', command)
        self.assertIn("sandbox_workspace_write.network_access=false", command)
        self.assertIn('web_search="disabled"', command)
        self.assertEqual("-", command[-1])

    def test_launch_requires_decision_and_does_not_allow_repo_edits(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "SceneIssues/manager/runtime").mkdir(parents=True)
            (root / "SceneIssues/README.md").write_text("workflow\n")
            (root / ".gitignore").write_text("/SceneIssues/manager/runtime/\n")
            subprocess.run(["git", "init", "-b", "master"], cwd=root, check=True, stdout=subprocess.DEVNULL)
            subprocess.run(["git", "config", "user.email", "test@example.com"], cwd=root, check=True)
            subprocess.run(["git", "config", "user.name", "Test"], cwd=root, check=True)
            (root / "tracked.txt").write_text("clean\n")
            subprocess.run(["git", "add", "."], cwd=root, check=True)
            subprocess.run(["git", "commit", "-m", "base"], cwd=root, check=True, stdout=subprocess.DEVNULL)

            window = root / "SceneIssues/manager/runtime/review-window.md"
            window.write_text("# window\n")
            fake = root / "fake-codex"
            fake.write_text(
                "#!/bin/sh\n"
                "if [ \"$1\" = \"--version\" ]; then echo 'codex-cli 0.153.0'; exit 0; fi\n"
                "cat >/dev/null\n"
                "mkdir -p SceneIssues/manager/runtime\n"
                "printf '%s\\n' '{\"reviewedItems\": [], \"followups\": []}' > SceneIssues/manager/runtime/decision.json\n"
            )
            fake.chmod(fake.stat().st_mode | stat.S_IXUSR)
            cfg = {"codex": {"binary": str(fake)}}
            decision = codex.launch(root, Path("SceneIssues/manager/runtime"), cfg, window)
            self.assertTrue(decision.exists())
            self.assertEqual("", subprocess.check_output(["git", "status", "--porcelain"], cwd=root, text=True).strip())

    def test_old_codex_version_is_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            fake = Path(tmp) / "codex"
            fake.write_text("#!/bin/sh\necho 'codex-cli 0.152.9'\n")
            fake.chmod(fake.stat().st_mode | stat.S_IXUSR)
            with self.assertRaises(codex.core.ManagerError):
                codex.require_codex({"codex": {"binary": str(fake)}})


if __name__ == "__main__":
    unittest.main()
