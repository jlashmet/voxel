# Experiment 008 — current-head exact-view replay success

**Hypothesis** — The standalone development-player replay reaches the saved `VoxelShowcase` camera pose on the current implementation, and the corrected Medrare House facade is visibly separated at the originally reported view.

**What was performed** — GitHub Actions run `32831102139` executed the hardened saved-view replay workflow against source commit `86cddfd2ce219256cc86ea6e85760dd80e5a9332`. The workflow built the development player, launched `Assets/Scenes/VoxelShowcase.unity` with `-voxel-scene-issue SceneIssues/20260824-220416-979-VoxelShowcase/issue.json`, required the runtime `SceneIssueReplayVerification` success log, captured five stationary frames, and uploaded replay artifact `9556851106` (`scene-220416-replay`, artifact digest `sha256:8ccd0c48c086de89fbe45be456ce666825bae1dede950988eda0bac91e2a85ea`).

**Pose verification** — Passed. `player-run.log` contains `Replaying issue with 1 screenshot(s). Verified standalone frozen pose.` from `SceneIssueReplayVerification`. The workflow's replay step and full job both completed successfully. The retained log downloaded from the artifact hashes to `sha256:0cfea431444e5b26e7e5b059eca2d943e4748684db7e3876caa64e24cfc3874a`.

**Visual result** — Passed. I reviewed the final 1200×675 replay frame (`replay-latest.jpg`, `sha256:fee6fd27888bc3e216ebd652482e950b8c5e296e17c6bdf63c9ecbf6a96c59fc`). At the saved camera/circle, the public entrance and the left frontage-window opening no longer occupy the same facade span: a distinct masonry pier separates the window opening from the doorway. The right frontage window remains present, so the fix has not solved the overlap by deleting the authored frontage rhythm.

**What was learned** — The earlier failure in run `32830868865` was a build-stage transient/infrastructure failure rather than evidence of a replay defect. With the same production geometry and replay semantics, the rerun reached the exact frozen view and rendered the intended facade separation.

**Result** — Hypothesis confirmed. The exact-view visual acceptance requirement is satisfied for source `86cddfd2ce219256cc86ea6e85760dd80e5a9332`.

**Next** — Restore the repurposed one-shot workflow to its pre-issue contents, review the final net diff for a single active frontage invariant, update the plan, and then mark the SceneIssue fixed in a bookkeeping commit.
