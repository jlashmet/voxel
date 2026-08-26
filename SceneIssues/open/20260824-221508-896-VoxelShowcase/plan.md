# Plan — align authored Kentridge pieces without gaps

## Goal

Resolve `20260824-221508-896-VoxelShowcase`: four marked locations in the saved VoxelShowcase view show authored pieces that do not meet cleanly or leave visible gaps.

The fix must identify the shared placement / boundary-ownership rule behind those seams. It must not patch the four screenshot coordinates or add presentation geometry only for this camera.

## Investigation / validation

- [x] Replay the exact saved camera against the current committed VoxelShowcase bake and compare all four marked locations with the original capture.
- [x] Identify the authoritative generators for the Market Square hard surface and market-stall dressing.
- [x] Correct the real inclusive +X/+Z plaza endpoint mismatch (attempt 1); focused tests passed, fresh replay still failed.
- [x] Give the hard piazza one continuous backing slab (attempt 2); focused tests passed, fresh replay still failed.
- [x] Inspect the fresh-bake replay and project the marked seam back into authored world coordinates.
- [x] Trace the surviving seam to zero-thickness authored interfaces rather than missing backing occupancy.
- [x] Add a focused red regression requiring material-only border paint and physical overlap at market-stall supports.
- [x] Prove the regression red in run `32928540659`: one test ran and failed at the intended `PaintSolid` assertion (`expected 3`, `was 0`).
- [x] Apply production attempt 3 in `9c839dcbbe73bb3f325db8d3dd3ef380d22343cf`: keep one geometric piazza slab, paint its border material, and sink market stalls one authored decimetre into the shared surface.
- [x] Prove the focused regression green in run `32929986757`.
- [x] Run the full `VoxelEngine.Tests.EditMode.KentridgeMarketPiazzaTests` class green in run `32930568776`.
- [x] Fresh-bake and replay the exact saved camera in run `32931218515`; technical replay passed, but visual acceptance failed because the blue strip remains in all three lower circles and blue exposure remains at the stall-foot circle.
- [x] Record the failed attempt-3 replay in `experiment-006-attempt3-fresh-replay.md` and `verification-attempt3-fresh-replay.txt`.
- [ ] Build a bare-bones reproduction that isolates the surviving blue exposure with minimum geometry/material/renderer inputs before any further production change.
- [ ] Use the reproduction to identify the actual boundary/renderer owner, then only after that evidence consider another production fix.
- [ ] If a later evidence-backed fix passes focused CI and fresh exact-pose replay, move the capture to `SceneIssues/closed/` with terminal fixed bookkeeping; until then keep it open.
- [ ] Restore/remove any temporary reproduction and CI-only wiring before master integration.
- [x] Do not start another capture; the user explicitly prohibited it.

## Findings

Attempts 1–3 each corrected a real structural weakness but did not remove the assigned visual defect. Attempt 1 fixed the hard/graded Market Square inclusive +X/+Z count mismatch. Attempt 2 gave the piazza one continuous backing slab. Attempt 3 removed coplanar geometry ownership from the dark border material and overlapped market-stall supports one authored decimetre into the piazza.

Attempt 3 has a complete structural red→green chain: run `32928540659` failed the pre-fix authored program at the intended `PaintSolid` contract; the same focused regression passed after attempt 3 in run `32929986757`; and the full `KentridgeMarketPiazzaTests` class passed in run `32930568776`.

The mandatory fresh visual verification disproved attempt 3 as the complete visual cause. Run `32931218515` preserved the saved camera/pose, removed annotations, freshly regenerated `ShowcaseWorld.bytes`, and verified the frozen pose in the standalone player. Its artifact `scene-221508-unobscured-view` / `9593376251` still shows the long light-blue strip through all three lower marked regions. Visible blue exposure also remains in the stall-support marked region. This is a real visual failure, not stale bake or replay drift.

The surviving strip is therefore not explained by the three full-scene production hypotheses already tried. Any further production edit would now be guesswork. The next required step is a minimum reproduction that can answer whether the visible blue is produced by voxel boundary extraction/material transitions, independently touching solids, the road/piazza authored relationship, or another renderer input entirely.

## Three-attempt rule

Production-fix attempts: **3 / 3 completed and visually failed**. No fourth production change is allowed until a bare-bones reproduction isolates the surviving behavior. Diagnostics, reproduction code, regression authoring, exact replays, and CI-only wiring do not themselves count as production-fix attempts, but temporary reproduction/wiring must be removed before merging the eventual verified fix to `master`.

## Acceptance

- All four marked seams/gaps in the saved view are absent after a fresh bake and exact-pose replay.
- The responsible geometry/material/renderer contract is demonstrated by a focused regression or reproduction, rather than inferred from screenshot coordinates.
- Nearby unmarked structures retain their intended spacing and silhouette.
- No camera-, screenshot-, or hard-coded issue-coordinate special case is introduced.
- A focused regression proves the final root-cause contract.
- A fresh standalone exact-camera replay from the regenerated startup bake visually passes.
