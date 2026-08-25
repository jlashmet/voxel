# Experiment 004 — circulation production attempt 1, exact-view visual failure

**Hypothesis under test** — The duplicate lower-town stair geometry visible in the reported VoxelShowcase frame is produced by the retired lower-west secondary stair chain and/or the dedicated `kentridge-stair-*` overlays on the already-ramped main road.

**What was performed** — After production attempt 1 was structurally green, the temporary one-shot SceneIssue workflow replayed the original saved camera in a development player. GitHub Actions run `32834542369`, source `3bb5538b960952d260a945b9d96bd769b6ded1fb`, passed `SceneIssueReplayVerification` and uploaded artifact `9558155377` (`scene-220516-attempt-1-exact-view`, digest `sha256:52ab79fb55000ee9c6cb824be936cf313f41521c2b5fc53d41a7bd6a757787e7`).

**Pose verification** — Passed. The player reached and froze the recorded camera position, rotation, and FOV before capture.

**Visual result** — Failed. The post-attempt exact-view frame is effectively identical to the pre-attempt reproduction. A direct pixel comparison between the baseline replay and attempt-1 replay found differences only in the top-left debug/FPS text region (bounding box approximately x=8..113, y=7..27). The town geometry below that region is unchanged: the same foreground white stair ribbons, central brown climb, and dense uphill composition remain visible.

**Interpretation** — The production changes removed real redundant circulation semantics and catalogue entries, and the focused regressions correctly prove those redundancies are gone. However, those retired entries are not the geometry responsible for the visible stair bundle in this captured VoxelShowcase view (or they were never effective in the rendered composition). Therefore structural green is not evidence that the SceneIssue improved visually.

**Attempt accounting** — This is production attempt 1 and it is a visual failure for `20260824-220516-659-VoxelShowcase`. The three-attempt rule now leaves two production attempts before a required minimal reproduction/escalation.

**Next** — Do not make another production edit until the actual VoxelShowcase runtime catalogue/bootstrap path is confirmed and the stair-producing definitions/explicit placements intersecting the saved camera's lower-town band are enumerated. Production attempt 2 must target a definition proven to be live in the exact captured view.
