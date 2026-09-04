# Experiment 024 - Gallery render convergence synchronization

## Trigger

Exact-SHA run `33842982484` validated source `a269b36f44093f1aafccf7daab4bbdcb36bf397b` through request commit `639ff0c2aa31be2e1df3f85895697c1a771210a2`.

The run completed `failure` because automatic module validation stopped on the Showcase publication regression. The standalone SceneIssue replay itself reported semantic PASS and produced both requested screenshots, so both the failing regression and the built-player artifact were inspected before making another change.

## Competing hypotheses

1. **The stronger post-bake publication regression disproves content-dirty publication.**
   - Discriminator: reach the regression assertion against the change feed.
   - Result: **not exercised**. `LoadBake` failed first because the test used `brickPoolCapacity: 196608` while the production `WorldbuildingGalleryShowcase.unity` scene uses `m_BrickPoolCapacity: 800000` with load radius 4 / unload radius 6.

2. **Content-dirty publication is present, but the SceneIssue capture happens before the production renderer has converged around the underground camera.**
   - Discriminator: inspect renderer diagnostics at the authored-breakable capture instead of changing notification semantics again.
   - Result: **supported**. The player log reports `SURFACE t=19.4 visible=48 ... missingMax=647` immediately before `SECRET_DISCOVERY_ACCEPTANCE frame=authored-breakable-boundary`. The renderer then continues converging: approximately 350 visible chunks by `t=25.4`, while the missing-visible count falls steadily. The full-resolution authored-breakable screenshot at the early capture still shows the world underside/void and floating vegetation.

3. **The camera is physically inside solid terrain or the cave/pocket was never authored.**
   - Discriminator: prior exact physical occupancy regression plus semantic clue counts.
   - Result: **rejected by earlier experiments and unchanged here**. The exact acceptance still reports `boundaryClueVoxels=31` and `naturalClueVoxels=30`; the existing exact-eye occupancy regression already proves the authored breakable eye lies in authoritative empty space.

## Root cause selected

The built-player evidence harness was treating a fixed `1.25s` wall-clock delay as renderer readiness. On this scene the blocking Gallery bootstrap can complete before URP has populated the camera's visible solid surface set. An underground screenshot taken during that cold convergence interval faithfully captures missing render geometry, even though authoritative secret voxels are present.

This is a synchronization/evidence defect, not permission to try another storage-notification variant. Experiment 023's content-dirty publication remains the production semantic fix for already-rendered worlds; this experiment changes only how exact built-player evidence waits for the real production renderer to consume current state.

## Changes

- Match `WorldbuildingGallerySecretDiscoveryPublicationTests` to the production Gallery footprint by using `brickPoolCapacity: 800000` while retaining the production radius 4 / unload radius 6. This lets the regression reach its content-dirty assertion instead of failing during bake restore.
- For the authored-breakable SceneIssue frame only, pin the real Gallery camera first and wait on `VoxelRenderBridge.SurfaceMetrics` until:
  - at least one production surface pass has run after the pin,
  - visible solid chunks are nonzero, and
  - `MissingVisibleSolidChunks == 0` for two consecutive frames.
- During that SceneIssue/offline wait only, temporarily raise the renderer's existing documented offline-capture convergence budgets. Restore every budget in `finally`.
- If the renderer does not converge within 10 seconds, emit `SECRET_DISCOVERY_RENDER_CONVERGENCE result=FAIL` and do not save the authored-breakable screenshot. The final capture-count acceptance therefore fails rather than accepting another void image.
- Natural-route evidence keeps its existing short wait because exact artifacts already show that surface view rendering correctly.

## Expected discriminator on the next exact run

The next exact run must show all of the following before this hypothesis is accepted:

1. Showcase EditMode publication regression reaches and passes the content-dirty assertion.
2. Player log contains `SECRET_DISCOVERY_RENDER_CONVERGENCE result=PASS frame=authored-breakable-boundary` with `missing=0`.
3. Full-resolution `02-authored-breakable-boundary.png` visibly shows the authored cave/false-wall clue instead of the underside/void.
4. Automatic module validation advances through CaveWorldBuilder, Showcase, WorldBuilder and Kentridge as selected by `ModuleValidation/plan.json`.

If convergence reports PASS but the screenshot still shows the void, the renderer-ready predicate is falsified and the next investigation must inspect the production render pass/chunk ownership at the target location; do not resume camera or change-publication guessing.
