"""Exercise capture command construction without launching Unity or a player."""
import json
import os
import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path

SCRIPT = Path(__file__).resolve().parents[1] / "showcase-player-capture.sh"


class PlayerCaptureFrameTimingTests(unittest.TestCase):
    def build_arguments(self, extra=(), scene_issue=False, expected_status=71):
        with tempfile.TemporaryDirectory(prefix="capture timing ") as td:
            root = Path(td)
            (root / "tools").mkdir()
            (root / "bin").mkdir()
            (root / "Assets").mkdir()
            scene = "Assets/Fixture.unity"
            (root / scene).write_text("%YAML 1.1\n", encoding="utf-8")
            wrapper = root / "tools/unity-run.sh"
            wrapper.write_text(
                '#!/usr/bin/env bash\nprintf "%s\\0" "$@" > "$BUILD_ARG_CAPTURE"\nexit 71\n',
                encoding="utf-8")
            wrapper.chmod(0o755)
            # Do not inspect or wait for the developer's real editor in a tooling unit test.
            pgrep = root / "bin/pgrep"
            pgrep.write_text("#!/bin/sh\nexit 1\n", encoding="utf-8")
            pgrep.chmod(0o755)
            captured = root / "build-arguments.bin"
            environment = dict(os.environ)
            environment["PATH"] = str(root / "bin") + os.pathsep + environment.get("PATH", "")
            environment["BUILD_ARG_CAPTURE"] = str(captured)
            command = ["bash", str(SCRIPT), "--unity", shutil.which("true"),
                       "--output", str(root / "evidence"), "--run-seconds", "30"]
            if scene_issue:
                issue = root / "issue.json"
                issue.write_text(json.dumps({"scenePath": scene, "captures": [],
                                             "screenWidth": 1920, "screenHeight": 1080}),
                                 encoding="utf-8")
                command += ["--scene-issue", str(issue)]
            else:
                command += ["--scene", scene]
            result = subprocess.run(command + list(extra), cwd=root, env=environment,
                                    capture_output=True, text=True, timeout=10)
            self.assertEqual(result.returncode, expected_status, result.stdout + result.stderr)
            if expected_status != 71:
                self.assertFalse(captured.exists(), "Invalid inputs must not invoke the build")
                return []
            self.assertTrue(captured.is_file(), "The existing Unity wrapper must receive the build")
            args = captured.read_bytes().decode("utf-8").rstrip("\0").split("\0")
            self.assertEqual(args[args.index("-executeMethod") + 1],
                             "VoxelEngine.Showcase.Editor.ShowcasePlayerBuild.Build")
            self.assertEqual(args[args.index("-voxelScene") + 1], scene)
            self.assertNotIn("-voxelDevelopment", args, "Timing must not require a profiler build")
            self.assertFalse((root / "evidence/Player").exists(), "Failed builds must be cleaned up")
            return args

    def assert_timing_requested(self, args):
        self.assertEqual(args.count("-voxelFrameTimingStats"), 1,
                         "FRAMEPIPE sampling requires timing enabled in the player build")

    def test_ordinary_capture_requests_frame_timing(self):
        self.assert_timing_requested(self.build_arguments())

    def test_traversal_capture_requests_frame_timing(self):
        self.assert_timing_requested(self.build_arguments(["--autowalk-after", "10"]))

    def test_scene_issue_capture_requests_frame_timing(self):
        self.assert_timing_requested(self.build_arguments(scene_issue=True))

    def test_stationary_capture_still_requests_frame_timing_once(self):
        self.assert_timing_requested(self.build_arguments(["--stationary-sample", "5"]))

    def test_incompatible_movement_is_rejected_before_build(self):
        self.build_arguments(["--stationary-sample", "5", "--autowalk-after", "10"],
                             expected_status=2)


if __name__ == "__main__":
    unittest.main()
