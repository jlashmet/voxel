import importlib.util
import json
import stat
import subprocess
import sys
import tempfile
import types
import unittest
from pathlib import Path

TOOLS = Path(__file__).resolve().parents[1]

VISUALS_SPEC = importlib.util.spec_from_file_location(
    "astra_manager_visuals", TOOLS / "astra_manager_visuals.py"
)
visuals = importlib.util.module_from_spec(VISUALS_SPEC)
assert VISUALS_SPEC.loader is not None
VISUALS_SPEC.loader.exec_module(visuals)


class AstraManagerVisualEvidenceTests(unittest.TestCase):
    def _git(self, root: Path, *args: str) -> str:
        result = subprocess.run(
            ["git", "-C", str(root), *args], text=True,
            stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=True,
        )
        return result.stdout.strip()

    def _review_fixture(self, root: Path, issue_id: str, regression: str = "") -> tuple[Path, Path]:
        issue_dir = root / "SceneIssues/closed" / issue_id
        issue_dir.mkdir(parents=True)
        (root / "SceneIssues/manager/runtime/packets").mkdir(parents=True)
        issue = {
            "formatVersion": 3,
            "id": issue_id,
            "status": "fixed",
            "regressionTest": regression,
            "resolutionSummary": "",
            "captures": [],
        }
        (issue_dir / "issue.json").write_text(json.dumps(issue), encoding="utf-8")
        packet = root / "SceneIssues/manager/runtime/packets" / f"{issue_id}.md"
        packet.write_text("# packet\n", encoding="utf-8")
        window = root / "SceneIssues/manager/runtime/review-window.md"
        window.write_text(
            f"### completion:{issue_id}:abc\n"
            f"- Completion packet: `SceneIssues/manager/runtime/packets/{issue_id}.md`\n",
            encoding="utf-8",
        )
        return issue_dir, window

    def test_local_issue_capture_is_attached_and_manifested(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self._git(root, "init", "-b", "master")
            issue_id = "20260905-120000-000-Visual"
            issue_dir, window = self._review_fixture(root, issue_id)
            capture_dir = issue_dir / "captures"
            capture_dir.mkdir()
            capture = capture_dir / "final.png"
            capture.write_bytes(b"\x89PNG\r\nfake")
            issue = json.loads((issue_dir / "issue.json").read_text())
            issue["captures"] = ["captures/final.png"]
            (issue_dir / "issue.json").write_text(json.dumps(issue), encoding="utf-8")

            manifest, images = visuals.prepare(
                root, Path("SceneIssues/manager/runtime"),
                {"visualEvidence": {"maxImagesPerReview": 2, "maxImagesPerCompletion": 2}},
                window,
            )
            self.assertEqual([capture.resolve()], images)
            self.assertIn("Attached image(s): `1`", manifest.read_text())
            self.assertIn("captures/final.png", manifest.read_text())

    def test_unrelated_local_reference_image_is_not_implicitly_attached(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self._git(root, "init", "-b", "master")
            issue_id = "20260905-120000-000-Visual"
            issue_dir, window = self._review_fixture(root, issue_id)
            (issue_dir / "reference.png").write_bytes(b"png")

            manifest, images = visuals.prepare(
                root, Path("SceneIssues/manager/runtime"),
                {"visualEvidence": {"maxImagesPerReview": 2, "maxImagesPerCompletion": 2}},
                window,
            )
            self.assertEqual([], images)
            self.assertIn("Attached image(s): `0`", manifest.read_text())

    def test_explicit_reference_named_capture_is_still_attached(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self._git(root, "init", "-b", "master")
            issue_id = "20260905-120000-000-Visual"
            issue_dir, window = self._review_fixture(root, issue_id)
            reference = issue_dir / "reference.png"
            reference.write_bytes(b"png")
            issue = json.loads((issue_dir / "issue.json").read_text())
            issue["captures"] = ["reference.png"]
            (issue_dir / "issue.json").write_text(json.dumps(issue), encoding="utf-8")

            _, images = visuals.prepare(
                root, Path("SceneIssues/manager/runtime"),
                {"visualEvidence": {"maxImagesPerReview": 2, "maxImagesPerCompletion": 2}},
                window,
            )
            self.assertEqual([reference.resolve()], images)

    def test_artifact_screenshots_are_downloaded_when_issue_has_no_local_capture(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self._git(root, "init", "-b", "master")
            self._git(root, "remote", "add", "origin", "git@github.com:example/voxel.git")
            issue_id = "20260905-120000-000-Visual"
            _, window = self._review_fixture(
                root, issue_id, "Exact-SHA validation passed; workflow run 33988857330."
            )
            fake_gh = root / "fake-gh"
            fake_gh.write_text(
                "#!/bin/sh\n"
                "if [ \"$1\" = \"api\" ]; then\n"
                "  printf '%s\\n' '{\"artifacts\":[{\"expired\":false,\"size_in_bytes\":1024}]}'\n"
                "  exit 0\n"
                "fi\n"
                "if [ \"$1\" = \"run\" ] && [ \"$2\" = \"download\" ]; then\n"
                "  dest=''\n"
                "  prev=''\n"
                "  for arg in \"$@\"; do\n"
                "    if [ \"$prev\" = \"--dir\" ]; then dest=\"$arg\"; fi\n"
                "    prev=\"$arg\"\n"
                "  done\n"
                "  mkdir -p \"$dest/SceneIssue/Screenshots\"\n"
                "  printf 'generic' > \"$dest/SceneIssue/Screenshots/showcase-000.png\"\n"
                "  printf 'final' > \"$dest/SceneIssue/verification-final.png\"\n"
                "  printf 'preview' > \"$dest/SceneIssue/Screenshots/showcase-000.preview.jpg\"\n"
                "  exit 0\n"
                "fi\n"
                "exit 2\n",
                encoding="utf-8",
            )
            fake_gh.chmod(fake_gh.stat().st_mode | stat.S_IXUSR)

            manifest, images = visuals.prepare(
                root, Path("SceneIssues/manager/runtime"),
                {"visualEvidence": {
                    "ghBinary": str(fake_gh),
                    "maxImagesPerReview": 2,
                    "maxImagesPerCompletion": 2,
                }},
                window,
            )
            self.assertEqual(2, len(images))
            self.assertTrue(str(images[0]).endswith("SceneIssue/verification-final.png"))
            self.assertIn("run-33988857330", str(images[0]))
            self.assertIn("Attached image(s): `2`", manifest.read_text())

    def test_review_budget_round_robins_images_across_completions(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self._git(root, "init", "-b", "master")
            runtime = root / "SceneIssues/manager/runtime"
            (runtime / "packets").mkdir(parents=True)
            ids = ["20260905-120000-000-A", "20260905-120000-001-B"]
            lines = []
            for issue_id in ids:
                issue_dir = root / "SceneIssues/closed" / issue_id
                capture_dir = issue_dir / "captures"
                capture_dir.mkdir(parents=True)
                (issue_dir / "issue.json").write_text(
                    json.dumps({"id": issue_id, "captures": []}), encoding="utf-8"
                )
                for index in range(3):
                    (capture_dir / f"evidence-{index}.png").write_bytes(b"png")
                (runtime / "packets" / f"{issue_id}.md").write_text("# packet\n")
                lines.append(
                    f"- Completion packet: `SceneIssues/manager/runtime/packets/{issue_id}.md`"
                )
            window = runtime / "review-window.md"
            window.write_text("\n".join(lines))

            _, images = visuals.prepare(
                root, Path("SceneIssues/manager/runtime"),
                {"visualEvidence": {"maxImagesPerReview": 2, "maxImagesPerCompletion": 3}},
                window,
            )
            self.assertEqual(2, len(images))
            self.assertIn(ids[0], str(images[0]))
            self.assertIn(ids[1], str(images[1]))


class CodexImageCommandTests(unittest.TestCase):
    def test_build_command_passes_each_image_to_codex(self):
        core = types.ModuleType("astra_manager")
        class ManagerError(RuntimeError):
            pass
        core.ManagerError = ManagerError
        sys.modules["astra_manager"] = core
        sys.modules["astra_manager_visuals"] = visuals
        spec = importlib.util.spec_from_file_location(
            "astra_manager_codex_for_visual_test", TOOLS / "astra_manager_codex.py"
        )
        module = importlib.util.module_from_spec(spec)
        assert spec.loader is not None
        spec.loader.exec_module(module)
        images = [Path("/repo/a.png"), Path("/repo/b.jpg")]
        command = module.build_command(
            {}, "/usr/local/bin/codex", Path("/repo/schema.json"),
            Path("/repo/decision.json"), images,
        )
        pairs = [(command[index], command[index + 1]) for index in range(len(command) - 1)]
        self.assertIn(("--image", "/repo/a.png"), pairs)
        self.assertIn(("--image", "/repo/b.jpg"), pairs)
        self.assertEqual(2, command.count("--image"))
        self.assertEqual("-", command[-1])


if __name__ == "__main__":
    unittest.main()
