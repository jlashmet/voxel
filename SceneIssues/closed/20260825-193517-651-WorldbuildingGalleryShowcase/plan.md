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
- Post-fix visual verification uses the existing real-player `--scene-issue` fixture replay path. Any capture-specific CI routing needed to expose that path is temporary and must be removed before terminal bookkeeping.
- Character interaction must be live-wired from an existing actor source through the game/composition layer into the engine rendering bridge; the VoxelEngine renderer must not acquire a dependency on game code.

## Acceptance criteria
- The responsible grass implementation is identified from current repository source, not guessed from stale workflow paths.
- A focused regression proves the grass rendering/configuration invariant changed by the fix.
- The bounded 64-entry character-displacement shader input is fed by a real production publisher rather than existing only as an unused API hook.
- The smallest relevant `ci/single-test` request completes successfully and executes a non-zero matching test.
- The original saved scene/pose is replay-verified after the fix, with the circled region checked using CI/replay evidence available to the remote agent.
- Final diff is reviewed, the production/test fix is committed separately from terminal issue bookkeeping, and the capture moves from `open/` to `closed/` with `status: fixed`, `resolvedUtc`, `resolutionSummary`, `regressionTest`, and the real production/test `fixCommit` SHA.
- No branch or capture is created beyond the assigned feature/CI branches and assigned issue.

## Tasks
- [x] Re-read `CLAUDE.md`, `AGENTS.md`, `SceneIssues/README.md`, and the assigned `issue.json`.
- [x] Confirm `fixes/agent-8` is at the assigned intake commit and contains no unmerged agent-8 work.
- [x] Inspect the saved capture metadata, camera pose, note, and marked region; document remote binary-view limitation rather than claiming pixel inspection.
- [x] Locate the current WorldbuildingGalleryShowcase scene assembly and responsible grass/vegetation renderer, shader/material, and tests.
- [x] Establish the red baseline through the repository-supported targeted-CI path and document the result.
- [x] Add/extend the focused regression.
- [x] Implement the shared foliage shader/material rendering fix.
- [x] Commit/push the initial production + regression changes on `fixes/agent-8`.
- [x] Reset/reuse `ci-test/fixes/agent-8`, add the targeted request only there, and obtain green `ci/single-test` for the shader contract.
- [x] Replay/verify the original fixture after the fix through real-player `--scene-issue` capture and document evidence.
- [x] Remove the temporary replay-routing change and confirm the feature branch contains only durable production/test/evidence changes.
- [x] Wire existing live actor positions into the bounded grass-interactor bridge without reversing game/engine dependencies, and extend the regression to prove that publisher is present.
- [x] Obtain a final green targeted `ci/single-test` on the complete durable source after the publisher wiring.
- [x] Review final diff and architecture constraints.
- [x] Commit terminal `issue.json` bookkeeping and open→closed move, push, and verify remote terminal state.
