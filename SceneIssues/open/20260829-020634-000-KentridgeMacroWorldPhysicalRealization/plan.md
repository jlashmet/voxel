# Plan

## Acceptance and ownership
`issue.json` is the contract (`captures: []`). Preserve the source-backed Mounting Force macro graph while delivering physical settlements, contiguous terrain-aware hard routes, reusable regional geography, Rossdam Lake, Southern Ridge/pass, real CharacterMotor traversal, durable built-player evidence, and bounded CPU/GPU/memory/streaming cost.

Owned validation surfaces:
- WorldBuilder: `Validation/MacroPhysicalWorld` through production catalogue/rendering.
- Showcase: `Validation/FeatureResidency` through production residency/readiness.
- Kentridge Playable: `Validation/KentridgeMacroWorld` through the real slice/evidence driver.
- Rendering: `Validation/GpuSurfaceMirrorRelocation` through production GPU mirror/extraction/publication.

## Latest evidence and root cause
Exact run `33955763704` validated source `9734c920d1a08aba0175faaf2d46c49922ac3d42` through request commit `eb2a912efe0d6cd22da63319d634c3b9baea0663`. Repository-derived persistent validation was green (`PASS=90 SKIP=0 FAIL=0 INCONCLUSIVE=0`) and the requested production `GpuSurfaceMirrorRelocationRequestedValidationTests.DistantUnrelatedChangeChurnExecutesProductionGpuLivenessRegression` passed. The standalone 180-second SceneIssue player remained process-clean but product-red for this feature contract: it still did not produce strict multi-settlement/geography/network evidence through Rossdam, Fairy Village, Orc Village, Southern Ridge, and the macro network within the acceptance window.

The phase-order correction from `9734c920...` was effective but incomplete. Production diagnostics show phase-2 GPU owners retiring intermittently while other phase-2 owners can remain live for several seconds near the end of the replay. The focused order regression proves phase-9 workers are placed before ordinary workers, so stale cursor ordering is no longer the demonstrated failure.

The remaining scheduler gap is the shared solid deadline itself. `VoxelSurfaceScheduler` computes the deadline before entering the worker loop. If earlier frame work has already consumed that budget, the old `remainingMs <= 0` guard breaks before visiting even the first priority worker. A phase-9 worker owns a paged GPU result whose completion poll is bounded/non-blocking; denying that retirement solely because ordinary admission budget is exhausted can strand ready publication until a later frame and recreate the observed seconds-long tail.

Selected correction is implemented without changing any acceptance or runtime budget. `SurfaceGpuCompletionPollOrder.CanVisit` permits phase-9 completion owners to receive their bounded retirement poll even at zero/negative remaining ordinary-admission budget while all ordinary worker phases still require positive budget. Focused EditMode coverage proves both sides of that boundary. `VoxelSurfaceScheduler` now asks that policy before breaking. Build ceilings, convergence scaling, streaming radius, solid deadline value, one-dispatch Metal backpressure, cursor fairness, and all acceptance thresholds remain unchanged.

Current correction commits are `d51799eec6b85aecb9fd3137064bbbebf6e1edbc` (production visit policy), `c4e6b9f7f59e39f8df00a95f0197c4e860d4028d` (focused exhausted-budget regression), and `ef8e99b12a91244ead444527d75e090264282eac` (scheduler integration). Plan/task bookkeeping follows on the same feature branch; the next exact-SHA request must use the resulting branch head rather than any intermediate implementation SHA.

## Remaining gates
1. Exact-SHA targeted CI: run the phase-9 exhausted-budget completion regression plus existing GPU relocation/liveness coverage, repository-derived module validation, and the 180-second SceneIssue replay.
2. Inspect full-resolution built-player evidence; require all settlements, Rossdam water/constrained route, Southern Ridge/pass, network overview, differentiated terrain, and real CharacterMotor traversal.
3. Record per-target convergence plus FPS/CPU/GPU/streaming and process/managed/native/GPU memory against existing budgets.
4. Merge then-current `origin/master`, revalidate the exact merged feature SHA as required, complete every task/acceptance item, move only this issue `open -> closed`, then promote through PR + auto-merge and monitor the required PR gate until merged.
