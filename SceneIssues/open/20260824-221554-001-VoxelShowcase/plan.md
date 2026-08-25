# Plan — Scene issue 20260824-221554-001-VoxelShowcase

## Goal

Ensure the captured `VoxelShowcase` staircase retains its intended architectural/road material instead of being repainted with the moss surface material.

## Scope and constraints

- Work only on this assigned capture and the smallest responsible Kentridge terrace material path.
- Reuse the saved `VoxelShowcase` camera pose as the replay fixture.
- Keep authoritative voxel/material state deterministic and CPU-owned.
- Remote validation uses the repository's targeted CI path; do not invoke Unity locally.
- Preserve the original screenshot and capture data unchanged.
- `.github/test-request.json` changes belong only on `ci-test/fixes/agent-3`.
- Temporary diagnostic CI workflows and probes stay on `ci-test/fixes/agent-3`; they must never be merged into the feature branch.

## Acceptance criteria

- [x] Inspect the saved capture metadata and original screenshot.
- [x] Replay the saved viewpoint before the fix and reproduce the green stair treads.
- [x] Reproduce/deterministically confirm why staircase cells receive the wrong green material.
- [x] Identify the smallest responsible material-ownership invariant.
- [x] Add a focused regression that demonstrates the failure before the production fix.
- [ ] Implement the smallest production fix.
- [ ] Pass the focused regression through targeted CI.
- [ ] Replay/verify the recorded viewpoint after the fix.
- [x] Document the diagnostic experiments and discarded hypotheses.
- [ ] Commit and push production/test work to `fixes/agent-3`.
- [ ] In a separate bookkeeping commit, mark the issue fixed and move the full capture to `SceneIssues/closed/`.

## Findings

- The capture note is: `stairs shouldn't be textured as grass`.
- The original screenshot and an exact pre-fix replay show a broad multi-step Kentridge urban terrace shoulder with green tread surfaces and masonry seams/risers.
- `ShowcaseWorld.VoxelSize` is 0.1 m. The saved camera localizes to the east shoulder of the `market-main` district terrace.
- An exact captured-ray probe against authoritative `ShowcaseWorld.SurfaceQuery` reports material ID `14` (`Moss`) on nearly every foreground tread and material ID `1` on occasional masonry seams. The renderer is displaying the authoritative material correctly.
- `KentridgeDistrictTerraceCatalogue` authors an Urban terrace with `RoadSurface` on its broad stepped shoulder and `DarkMasonry` on its core.
- `KentridgeTerraceSurfaceCorrectionCatalogue` currently repaints the entire footprint Moss, then restores DarkMasonry only over the Urban core. That leaves the shoulder Moss and causes this capture.
- The smallest ownership invariant is: Urban surface correction must preserve `RoadSurface` on the full footprint/shoulder before reasserting the DarkMasonry core.
- The earlier `proceduralBakeSurfaceMaterialId` theory was false; the current scene does not serialize those fields. The first material probe using `Camera.main` was also discarded because it sampled a different active camera.
