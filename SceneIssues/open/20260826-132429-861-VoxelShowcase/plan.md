# Plan — 20260826-132429-861-VoxelShowcase

## Goal

Make the captured Kentridge facade glazing read unmistakably as architectural windows instead of full-depth glass blocks, while preserving the existing rounded opening/reveal treatment and window materials.

## Capture

- Note: `are these windows?  they look aweful`
- Scene: `VoxelShowcase`
- Saved camera: `(172.89835, 35.65001, 18.45081)`, FOV `70`
- One capture, no annotation circles.

## Working hypothesis

The captured facade is a Kentridge landmark/fabric structure using the shared rectangular `ArchitectureVoxelPatterns.GlazedOpening` helper. That helper currently carves the wall aperture and then restores glazing across the complete aperture depth. The result has no architectural reveal: from an oblique view it reads as a solid glass/warm-material block embedded through the wall rather than as a window pane.

The arched glazing helper already uses the intended construction vocabulary: structural carve plus a thin planar glazing layer. Rectangular glazing should follow the same invariant.

## Acceptance criteria

- [x] Inspect the assigned capture metadata and all annotation circles (none present).
- [x] Trace the captured window semantics from Kentridge generation into voxel authoring.
- [ ] Add a focused EditMode regression proving rectangular glazing preserves a full-depth reveal but restores only a thin planar pane for both Z-normal and X-normal facade orientations.
- [ ] Apply the smallest production fix in the shared glazing pattern; do not special-case the captured city or renderer.
- [ ] Commit and push production/test work to `fixes/agent-3` before issue bookkeeping.
- [ ] Run the focused regression through `ci-test/fixes/agent-3` and obtain green `ci/single-test`.
- [ ] Replay the exact saved SceneIssue viewpoint against freshly generated/baked content and verify the window presentation is corrected.
- [ ] Record every experiment and replay result in this capture directory.
- [ ] Reconcile current `master` into the feature branch before terminal bookkeeping if upstream advanced, and rerun affected validation if inputs changed.
- [ ] In a separate bookkeeping commit, set `issue.json` to `fixed`, fill `resolvedUtc`, `resolutionSummary`, `regressionTest`, and a valid production/test `fixCommit`, then move the entire capture to `SceneIssues/closed/20260826-132429-861-VoxelShowcase`.

## Constraints

- Work only this assigned capture.
- Use only `fixes/agent-3` for production/test/bookkeeping and `ci-test/fixes/agent-3` for targeted CI.
- Never put `.github/test-request.json` on `fixes/agent-3`.
- Remote worker: do not run Unity locally.
- Do not start or capture another SceneIssue.
