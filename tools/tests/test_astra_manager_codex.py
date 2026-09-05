import importlib.util
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

    def test_command_is_ephemeral_read_only_astra_low_reasoning_and_schema_constrained(self):
        schema = Path("/repo/SceneIssues/manager/decision.schema.json")
        decision = Path("/repo/SceneIssues/manager/runtime/decision.json")
        command = codex.build_command({}, "/usr/local/bin/codex", schema, decision)
        self.assertEqual("/usr/local/bin/codex", command[0])
        self.assertEqual("exec", command[1])
        self.assertIn("--ephemeral", command)
        self.assertIn("--ignore-user-config", command)
        self.assertIn("gpt-6-astra", command)
        self.assertIn("read-only", command)
        self.assertIn(str(schema), command)
        self.assertIn(str(decision), command)
        self.assertIn('model_reasoning_effort="low"', command)
        self.assertIn('approval_policy="never"', command)
        self.assertIn('web_search="disabled"', command)
        self.assertNotIn("sandbox_workspace_write.network_access=false", command)
        self.assertEqual("-", command[-1])

    def test_launch_uses_codex_output_file_and_keeps_repo_read_only(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            runtime = root / "SceneIssues/manager/runtime"
            runtime.mkdir(parents=True)
            (root / "SceneIssues/README.md").write_text("workflow\n")
            (root / "SceneIssues/manager/decision.schema.json").write_text(
                '{"type":"object","required":["reviewedMasterSha","reviewedItems","followups","unresolvedQuestions"]}\n'
            )
            (root / ".gitignore").write_text("/SceneIssues/manager/runtime/\n")
            subprocess.run(["git", "init", "-b", "master"], cwd=root, check=True, stdout=subprocess.DEVNULL)
            subprocess.run(["git", "config", "user.email", "test@example.com"], cwd=root, check=True)
            subprocess.run(["git", "config", "user.name", "Test"], cwd=root, check=True)
            (root / "tracked.txt").write_text("clean\n")

            fake = root / "fake-codex"
            fake.write_text(
                "#!/bin/sh\n"
                "if [ \"$1\" = \"--version\" ]; then echo 'codex-cli 0.153.0'; exit 0; fi\n"
                "out=''\n"
                "prev=''\n"
                "for arg in \"$@\"; do\n"
                "  if [ \"$prev\" = '--output-last-message' ]; then out=\"$arg\"; fi\n"
                "  prev=\"$arg\"\n"
                "done\n"
                "cat >/dev/null\n"
                "test -n \"$out\" || exit 9\n"
                "mkdir -p \"$(dirname \"$out\")\"\n"
                "printf '%s\\n' '{\"reviewedMasterSha\":\"abc123\",\"reviewedItems\":[],\"followups\":[],\"unresolvedQuestions\":[]}' > \"$out\"\n"
            )
            fake.chmod(fake.stat().st_mode | stat.S_IXUSR)
            subprocess.run(["git", "add", "."], cwd=root, check=True)
            subprocess.run(["git", "commit", "-m", "base"], cwd=root, check=True, stdout=subprocess.DEVNULL)

            window = runtime / "review-window.md"
            window.write_text("# window\n")
            cfg = {"codex": {"binary": str(fake)}}
            decision = codex.launch(root, Path("SceneIssues/manager/runtime"), cfg, window)
            self.assertTrue(decision.exists())
            self.assertIn('"reviewedMasterSha":"abc123"', decision.read_text())
            self.assertEqual("", subprocess.check_output(["git", "status", "--porcelain"], cwd=root, text=True).strip())

    def test_missing_decision_schema_is_rejected_before_launch(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "SceneIssues/manager/runtime").mkdir(parents=True)
            (root / "SceneIssues/README.md").write_text("workflow\n")
            subprocess.run(["git", "init", "-b", "master"], cwd=root, check=True, stdout=subprocess.DEVNULL)
            window = root / "SceneIssues/manager/runtime/review-window.md"
            window.write_text("# window\n")
            with self.assertRaises(codex.core.ManagerError):
                codex.launch(root, Path("SceneIssues/manager/runtime"), {}, window)

    def test_old_codex_version_is_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            fake = Path(tmp) / "codex"
            fake.write_text("#!/bin/sh\necho 'codex-cli 0.152.9'\n")
            fake.chmod(fake.stat().st_mode | stat.S_IXUSR)
            with self.assertRaises(codex.core.ManagerError):
                codex.require_codex({"codex": {"binary": str(fake)}})


if __name__ == "__main__":
    unittest.main()
