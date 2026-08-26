# Plan — reopened VoxelShowcase LOD presentation issue

## Scope

Work only `SceneIssues/open/20260825-032832-253-VoxelShowcase` on `fixes/agent-4`. The earlier agent-4 closure was merged and explicitly reverted because the issue remained visible, so its prior fixes are evidence, not accepted resolution.

## Prior evidence carried forward

- Independent LOD rings can publish overlapping visible geometry. A previous render-staging ownership filter proved that overlap exists, but the exact saved-view replay still showed the reported coarse/striped patches.
- The analytic far terrain is not the source of the three marked regions; the saved view places them well inside its published hole.
- `VoxelShowcase` serialized `m_DetailBandScale = 0.6`, and changing it to `1.0` moved the first handoff from 57.6 m to 96 m. That change passed a focused policy test and a prior replay was judged clean, but the merged result was later rejected and reverted. Treating band distance alone as the root cause is therefore falsified.

## Current root-cause question

The code already defines `SurfaceLodCoverageState` and `SurfaceLodActiveCoverage`, whose invariant is atomic hierarchy ownership: keep a complete coarse parent until all eight finer children are current-complete, then replace the parent with the complete child set. Production visibility currently ignores that logical owner set: each ring independently collects in-band ready entries and `VoxelSurfaceScheduler.CollectVisibility` appends every ring's entries directly to `_visibleSolids`.

The previous visibility fix inferred ownership from only the currently visible entries, which cannot know whether all eight finer children are complete and therefore can choose the coarse parent even after refinement should own the area.

## Required isolation before another production change

The earlier investigation exceeded three implementation attempts, so first add a bare-bones EditMode reproduction with only one coarse parent, its eight finer children, completion state, and candidate draw keys. It must prove the desired handoff behavior independently of scene streaming, materials, GPU extraction, and camera/frustum complexity:

1. Partial finer completion keeps the coarse parent as the only drawable owner.
2. All eight current-complete children atomically retire the parent and become the drawable owners.
3. Known-empty children count as complete coverage but are not emitted as geometry.
4. Stale child completions cannot retire a current coarse parent.

If the existing active-coverage primitive satisfies the isolated invariant, the production fix should wire that same authority into scheduler visibility rather than create a second ownership heuristic.

## Verification

- Establish a behavioral red regression against current production visibility integration.
- Implement the smallest scheduler integration that uses the atomic active coverage as the source of draw ownership while preserving the existing one-chunk ring overlap for residency/build convergence.
- Run the exact focused regression through `ci-test/fixes/agent-4`.
- Replay `ShowcaseSceneIssue032832ReplayTests.SavedFixtureIsConfiguredForExactReplay` through the same targeted CI branch and inspect the saved 1364x836 standalone evidence for all three marked regions.
- Do not close on test success alone. Only after the exact replay is visually/structurally clean, record terminal `issue.json` fields, move the whole capture to `SceneIssues/closed/`, push the bookkeeping commit, and stop without starting another capture.
