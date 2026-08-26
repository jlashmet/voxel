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
- [x] Implement the smallest production fix.
- [x] Pass the focused regression through targeted CI.
- [x] Replay/verify the recorded viewpoint after the fix.
- [x] Document the diagnostic experiments and discarded hypotheses.
- [x] Commit and push production/test work to `fixes/agent-3`.
- [x] In a separate bookkeeping commit, mark the issue fixed and move the full capture to `SceneIssues/closed/`.

## Findings

- The capture note is: `stairs shouldn't be textured as grass`.
- The original screenshot and pre-fix replay show a broad multi-step Kentridge urban terrace shoulder with green tread surfaces and masonry seams/risers.
- The saved fixture camera is `Showcase Camera` at approximately `(136.3243, 25.5500, 58.9826)`, FOV 70, at 1364×836.
- `ShowcaseWorld.VoxelSize` is 0.1 m. The saved camera localizes to the east shoulder of the `market-main` district terrace.
- An authoritative surface-material probe reports material ID `14` (`Moss`) on the affected tread surfaces. The renderer was displaying the authoritative material correctly.
- `KentridgeDistrictTerraceCatalogue` authors an Urban terrace with `RoadSurface` on its broad stepped shoulder and `DarkMasonry` on its core.
- `KentridgeTerraceSurfaceCorrectionCatalogue` repainted the entire footprint Moss, then restored DarkMasonry only over the Urban core. That left the shoulder Moss and caused this capture.
- The smallest ownership invariant is: Urban surface correction must preserve `RoadSurface` on the full footprint/shoulder before reasserting the DarkMasonry core; non-Urban correction remains Moss.
- The production/test fix is commit `d9e0d893795147cfcd9580495358dd173ee77176`.
- Focused CI run `32928906165` passed `VoxelEngine.Tests.EditMode.KentridgeTerraceSurfaceCorrectionTests.MarketMainUrbanShoulderUsesRoadSurfaceInsteadOfMoss` on CI commit `f8c15cf0ee15c5e4ae2e96728a78ae2a309d8560`, which contains the recorded fix commit.
- Post-fix replay run `32930014899` on CI commit `bd53f8d3f0378666d073b8cfd43457a596736511` rebaked `ShowcaseWorld.bytes`, invoked `showcase-player-capture.sh --scene-issue` with this issue, and completed successfully.
- The final replay screenshot shows road/stone-colored tread surfaces with the masonry risers retained; the grass-textured stair defect is gone. Durable evidence is saved as `verification-postfix.png` and `verification-postfix.txt`.
- Experiment 003 was retained but corrected during terminal audit: its replay was invalid because it used stale baked startup-world data, not because the `(136.32, 25.55, 58.98)`, FOV 70 camera was wrong.
