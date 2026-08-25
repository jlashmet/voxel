# Experiment 007 — current-head replay verifier

**Hypothesis** — The current development-player replay path reaches the saved VoxelShowcase camera pose, and `SceneIssueReplayVerification` can prove that the replay is frozen at the recorded position, rotation, and FOV.

**What was performed** — Updated the temporary one-shot replay workflow to require the verifier success log after running `tools/showcase-player-capture.sh --scene-issue SceneIssues/20260824-220416-979-VoxelShowcase/issue.json`. GitHub Actions run `32830868865` executed against source commit `ca240836aaeb28420132c66ed6e5670dddcb34cd`.

**Result** — Inconclusive. The workflow passed checkout, replay-plumbing checks, Unity resolution, and the idle-Unity guard, but the development player never launched. `ShowcasePlayerBuild.Build` threw during the batchmode build after about 74 seconds (`unity-run` status 1, peak RSS about 8297 MB). Because the shell step exited immediately, the workflow did not preserve `player-build.log`, and no replay/verifier assertion ran.

**What was learned** — The stricter proof exposed a build-stage failure, not evidence that the camera replay itself is wrong. The hypothesis is neither confirmed nor disproven. The current workflow does not retain enough diagnostics when the player build fails.

**Next** — Make the temporary replay workflow tail and upload `player-build.log` even when the build command fails, then rerun the same current-head replay without changing production geometry or replay semantics.
