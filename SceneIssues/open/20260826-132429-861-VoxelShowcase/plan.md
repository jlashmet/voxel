# Plan — 20260826-132429-861-VoxelShowcase

## Goal

Make the captured Kentridge facade glazing read unmistakably as architectural windows rather than large flat amber material slabs, while preserving the city's masonry/reveal vocabulary and existing glazing materials.

## Capture

- Note: `are these windows?  they look aweful`
- Scene: `VoxelShowcase`
- Saved camera: `(172.89835, 35.65001, 18.45081)`, FOV `70`
- Resolution: `1928x836`
- One capture, no annotation circles.

## Findings and current hypothesis

The captured facade uses the shared rectangular `ArchitectureVoxelPatterns.GlazedOpening` helper. Attempt 1 proved that restoring glazing through the full wall depth was wrong, but a thin centered pane alone was visually insufficient: the fresh-baked exact-camera replay still read as one uninterrupted amber slab.

Attempt 2 therefore targets the facade composition rather than only pane depth. Large rectangular openings retain masonry at the perimeter and through a central mullion, producing two inset thin planar panes. Small openings keep the simpler single-pane construction. The original facade normal is preserved explicitly while emitting split cells so both X-normal and Z-normal windows retain correct pane depth.

## Acceptance criteria

- [x] Inspect the assigned capture metadata and all annotation circles (none present).
- [x] Trace the captured window semantics from Kentridge generation into voxel authoring.
- [x] Add focused EditMode coverage for thin centered panes and for retained masonry framing/subdivision across facade orientations.
- [x] Record attempt 1's failed fresh-baked exact-camera replay and its visual conclusion.
- [x] Implement attempt 2 in the shared glazing pattern without city/material/renderer special-casing.
- [x] Commit and push attempt-2 production/test work to `fixes/agent-3` before issue bookkeeping.
- [x] Reconcile current `master` into `fixes/agent-3` after the scene-agent CI/replay workflow update.
- [ ] Re-run the affected focused regressions from the reconciled feature state through `ci-test/fixes/agent-3` and obtain green `ci/single-test` using the current one-update request workflow.
- [ ] Use the shared `scene_issue` targeted-CI request to fresh-bake `VoxelShowcase` and replay the exact saved camera in the standalone player.
- [ ] Persist the successful final replay image in this capture directory as `verification-final.png` (and preserve a before/after comparison for the subjective quality review).
- [ ] Record the attempt-2 CI and replay results in numbered experiment files; keep runner/queue observations in `ci-operations.md` if needed.
- [ ] Obtain explicit human approval that the subjective window-quality complaint is resolved before marking the capture fixed.
- [ ] Immediately before terminal bookkeeping/promotion, recheck current `master`; integrate any newer changes and rerun affected validation if tested inputs changed.
- [ ] In a separate bookkeeping commit after approval, set `issue.json` to `fixed`, fill `resolvedUtc`, `resolutionSummary`, `regressionTest`, and the valid production/test `fixCommit`, then move the entire capture to `SceneIssues/closed/20260826-132429-861-VoxelShowcase`.
- [ ] Promote the verified terminal branch to current `master` non-force and verify remote master contains the fix/bookkeeping, only the closed capture, and green required CI.

## Constraints

- Work only this assigned capture.
- Use only `fixes/agent-3` and `ci-test/fixes/agent-3`; do not create another branch, PR, or one-shot replay workflow.
- `.github/test-request.json` changes belong only to CI request commits, never the feature branch.
- Build each CI request commit directly on the exact feature SHA, then move the remote CI ref once per iteration.
- Exact saved-camera replay uses the shared targeted-test workflow with `scene_issue` and `replay_seconds`.
- Remote worker: do not run Unity locally.
- Do not start or capture another SceneIssue.
