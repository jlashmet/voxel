# Plan

## Acceptance and ownership
`issue.json` is the contract (`captures: []`). Preserve the source-backed Mounting Force macro graph while delivering physical settlements, contiguous terrain-aware hard routes, reusable regional geography, Rossdam Lake, Southern Ridge/pass, real CharacterMotor traversal, durable built-player evidence, and bounded CPU/GPU/memory/streaming cost.

Owned validation surfaces:
- WorldBuilder: `Validation/MacroPhysicalWorld` through production catalogue/rendering.
- Showcase: `Validation/FeatureResidency` through production residency/readiness.
- Kentridge Playable: `Validation/KentridgeMacroWorld` through the real slice/evidence driver.
- Rendering: `Validation/GpuSurfaceMirrorRelocation` through production GPU mirror/extraction/publication.

## Latest evidence and root cause
Exact run `33962213806` validated feature source `870c6bd0b9fed9005586945a328a9e5a8ed2f1dd` through request commit `51dd1ded1e1c3778fc8cfcec178b28c7c04dee8e`. Repository-derived persistent test phases pass, the focused exhausted-budget `SurfaceGpuCompletionPollOrderTests` pass, and the requested production `GpuSurfaceMirrorRelocationRequestedValidationTests.DistantUnrelatedChangeChurnExecutesProductionGpuLivenessRegression` passes `1/1` in 41.6 seconds. The phase-9 completion-consumption correction is therefore validated and should be retained.

Feature acceptance remains red. The required 120-second Kentridge module player reaches the real `KentridgePlayableSlice`, records local CharacterMotor traversal, and begins Moordell demand, but it does not emit the required Moordell content/capture readiness before the harness ends. The 180-second SceneIssue replay is process-clean and reaches `content-ready target=moordell columns=4`, but never reaches strict `capture-ready target=moordell` or any later Rossdam/Fairy/Orc/ridge/network evidence. Renderer diagnostics still report `jobs=8 missing=89` at t=180.

The stronger discriminator is pre-gameplay renderer publication, not another phase-9 worker-poll defect. `KentridgePlayableSlice.TickOpeningPreload()` intentionally refuses to enter the authored opening until `RenderingComposition.HasCompletePublishedNearSurfaceCoverage()` is true. In the latest module run that strict opening coverage consumes roughly the first high-90 seconds before the validation sequence can restore ordinary time scale, exercise the CharacterMotor, and begin Moordell demand; the standalone replay shows the same shape with startup variation and begins Moordell demand only after roughly 108 seconds. Moordell content then becomes ready around 161 seconds. Historical exact evidence in experiment 012 reached gameplay around t=15 and captured Moordell, so the current opening tail correlates with the present GPU production renderer path rather than 160 seconds of macro feature generation.

The capture driver independently and correctly requires complete production near-surface publication for four stable frames. Do not weaken opening/capture readiness, widen residency, raise streaming/device budgets, force-generate acceptance content, or substitute storage-only evidence.

Current `GpuSurfaceMirrorCoordinator` has four two-record count lanes but deliberately enforces one global graphics-fence submission at a time for Metal backpressure. The latest replay shows publication making steady but acceptance-insufficient progress under that policy. A further agent-6 GPU throughput redesign would now overlap the actively changing `fixes/agent-1` GPU mirror/page-arena/publication work. Agent 1's open `GpuRendererProductionRestoration` plan remains visual-acceptance red and is actively repairing CPU-authoritative pending/live publication ownership. This SceneIssue's `master-sync-required.md` already defines that validated renderer work as an external prerequisite.

`fixes/agent-6` already contains published renderer restoration commit `f5593cc1236ba3963fc5713a11df35292628e97d`; the five commits currently ahead on `master` are unrelated residency/AI/house work and do not supply the pending renderer correction. Preserve the current Kentridge implementation and phase-9 fix while Agent 1 owns that renderer boundary. Experiment 050 records the exact evidence and ownership discriminator.

## Remaining gates
1. When Agent 1's current GPU renderer correction is validated and merged to `master`, merge then-current `origin/master` into `fixes/agent-6` as required by `master-sync-required.md`; re-evaluate the strict opening/publication blocker before any further agent-6 production renderer change.
2. Re-run exact-SHA targeted CI through the sole `ci-test/fixes/agent-6` transport. Require repository-derived tests, all required module-local players, the production GPU liveness regression, and the 180-second SceneIssue replay to pass on the exact synchronized source.
3. Inspect full-resolution built-player evidence; require all settlements, Rossdam water/constrained route, Southern Ridge/pass, network overview, differentiated terrain, and real CharacterMotor traversal.
4. Record per-target convergence plus FPS/CPU/GPU/streaming and process/managed/native/GPU memory against existing budgets.
5. Complete every task/acceptance item, move only this issue `open -> closed`, then promote through PR + auto-merge and monitor the required PR gate until merged.
