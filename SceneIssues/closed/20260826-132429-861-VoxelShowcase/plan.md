# Plan — 20260826-132429-861-VoxelShowcase

## Goal

Make the captured Kentridge facade glazing read as architectural windows rather than large flat amber slabs while preserving the masonry/reveal vocabulary.

## Capture

- Note: `are these windows?  they look aweful`
- Scene: `VoxelShowcase`
- Saved camera: `(172.89835, 35.65001, 18.45081)`, FOV `70`, `1928x836`
- One capture; no annotation circles.

## Resolution

The captured facade uses `ArchitectureVoxelPatterns.GlazedOpening`. The first fix made the pane thin but exact replay showed that depth alone did not solve the visual complaint. The final construction keeps masonry around large rectangular openings and through a central mullion, producing two inset thin planar panes while retaining the authored wall normal. Small openings retain the simpler thin-pane form.

Production fix: `93ace9a23d9c1145c64d8c19a559c9b881c59d68`.
Focused regression: `VoxelEngine.Tests.EditMode.ArchitectureVoxelPatternTests.GlazedOpeningFramesAndSubdividesLargeFacadePane`.

## Verification

- [x] Inspected the capture metadata and traced Kentridge window authoring to the shared glazing pattern.
- [x] Added focused regression coverage and implemented the framed/subdivided glazing fix.
- [x] Recorded all product experiments, including the failed thin-pane-only replay.
- [x] Integrated current `master` through `025e88ef6e2d097143607c3018184ddc99cb747c`; process changes did not modify glazing production/test code.
- [x] Focused framed-glazing EditMode regression is green.
- [x] Current-master exact validation passed in run `33014640709` from feature source `9de547d259760989b56a29916819f2c99cbd8d64`: PlayMode production-renderer smoke test, exact saved-camera standalone replay, previews, artifact upload, and `ci/single-test` all succeeded.
- [x] Final exact-pose evidence is committed as `verification-final.png` (plus the original `screenshot-001.png`).
- [x] Final replay result is recorded in `experiment-007-current-master-pending-replay.md`.
- [ ] In a separate bookkeeping commit, set `issue.json.status` to `pending`, fill `resolutionSummary`, `regressionTest`, and `fixCommit`, leave `resolvedUtc` empty, and move the entire capture from `open/` to `pending/`.
- [ ] Verify the remote pending state, exact CI success, fix ancestry, and absence of feature-branch CI-request/workflow changes; then stop and wait for the coordinator/human review flow.

## Constraints

Work only this capture. Use only `fixes/agent-3` and `ci-test/fixes/agent-3`. Do not push `master`, create a review branch/PR, or start another SceneIssue. The coordinator owns pending promotion and human approval.
