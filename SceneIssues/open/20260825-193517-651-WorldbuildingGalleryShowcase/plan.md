# Plan — 20260825-193517-651 WorldbuildingGalleryShowcase grass quality

## Goal
Improve the grass visible in the captured WorldbuildingGalleryShowcase viewpoint so it reads as deliberate stylized vegetation rather than poor/repetitive geometry, while keeping the change focused on the existing grass/worldbuilding rendering path.

## Scope and constraints
- Work only on capture `20260825-193517-651-WorldbuildingGalleryShowcase` on `fixes/agent-8`.
- Preserve the original capture and circle annotation; do not create another capture.
- Use the current grass/vegetation architecture rather than introducing a duplicate scene-specific renderer unless source inspection proves no reusable path exists.
- Treat the capture note as the requested rendering direction: instanced billboard-style blades, alpha cutout, per-instance size/color/phase variation, world-space patch/wind noise, non-synchronized sway, displacement hooks, and stylized/toon lighting where compatible with the existing renderer.
- Connector-only validation must use `ci-test/fixes/agent-8`; `.github/test-request.json` must never be changed on the feature branch.
- Every replay/probe/test/fix attempt gets an immediate numbered experiment file.

## Acceptance criteria
- The responsible grass implementation is identified from current repository source, not guessed from stale workflow paths.
- A focused regression proves the grass rendering/configuration invariant changed by the fix.
- The smallest relevant `ci/single-test` request completes successfully and executes a non-zero matching test.
- The original saved scene/pose is replay-verified after the fix, with the circled region checked using CI/replay evidence available to the remote agent.
- Final diff is reviewed, the production/test fix is committed separately from terminal issue bookkeeping, and the capture moves from `open/` to `closed/` with `status: fixed`, `resolvedUtc`, `resolutionSummary`, `regressionTest`, and the real production/test `fixCommit` SHA.
- No branch or capture is created beyond the assigned feature/CI branches and assigned issue.

## Tasks
- [x] Re-read `CLAUDE.md`, `AGENTS.md`, `SceneIssues/README.md`, and the assigned `issue.json`.
- [x] Confirm `fixes/agent-8` is at the assigned intake commit and contains no unmerged agent-8 work.
- [x] Inspect the saved capture metadata, camera pose, note, and marked region; document remote binary-view limitation rather than claiming pixel inspection.
- [ ] Locate the current WorldbuildingGalleryShowcase scene assembly and responsible grass/vegetation renderer, shader/material, and tests.
- [ ] Establish/replay the baseline through the repository-supported remote validation path and document the result.
- [ ] Add/extend the focused regression.
- [ ] Implement the smallest production fix.
- [ ] Commit/push production + regression changes on `fixes/agent-8`.
- [ ] Reset/reuse `ci-test/fixes/agent-8`, add the targeted request only there, and obtain green `ci/single-test`.
- [ ] Replay/verify the original fixture after the fix and document evidence.
- [ ] Review final diff and architecture constraints.
- [ ] Commit terminal `issue.json` bookkeeping and open→closed move, push, and verify remote terminal state.
