# Experiment 043 — unrelated coverage-epoch starvation

## Trigger
Exact source `e0ba4782c148dc46799ae20e40afcfa7f01f1fb0` was exercised by transport `44ab5dfa5d466c4d01afc7d27eeb10b90caf494e` in run `33886870759`. The requested GPU-enabled relocation regression passed in about 71 seconds, while the Kentridge-owned module player and 180-second SceneIssue replay still failed acceptance by remaining on the first Moordell survey.

The validation-only dense-demand diagnostic rejects the previous capacity/bookkeeping hypothesis as the active blocker. Across the stalled Kentridge replay the mirror has 47,662 slots and reaches only about 13,480 resident mixed bricks. `refused=0`, `evictions=0`, `directoryRefusals=0`, and `pendingStale=0` throughout. The eight live admission footprints instead cycle through large ready/pending populations while macro feature publication continues in other world regions.

## Competing hypotheses

### H1 — global coverage epoch causes unrelated-edit starvation
`GpuSurfaceExtractionContext.BeginPersistentStage()` restarts its complete coverage scan whenever `GpuSurfaceMirrorCoordinator.CoverageEpoch` changes. `ApplyChange()` advances that global epoch whenever any currently-ready solid block changes. Each near-ring cache footprint contains 18^3 = 5,832 blocks and `Covers()` inspects at most 128 per poll, so a no-reset pass requires roughly 46 polls. A continuously generating macro world can therefore restart all eight scans because of ready-block changes outside a particular request's footprint.

Prediction: the already-green distant-relocation harness will become admission-stalled when a ready block hundreds of metres behind the relocated target is kept demanded and repeatedly invalidated/recovered. Mirror capacity/refusal counters should remain healthy.

### H2 — only in-footprint recovery pressure matters
The global epoch is not the material liveness boundary. Kentridge's continuing edits either overlap the actual relocated footprints or the live recovery workload itself exceeds available convergence throughput.

Prediction: the relocated GPU requests still cross admission and complete useful builds even while the distant control block repeatedly follows ready -> invalidated -> recovery through the production coordinator path.

## Discriminator
Add `GpuSurfaceMirrorRelocationLivenessTests.DistantUnrelatedReadyBlockChangesCannotStarveRelocatedCoverage` without changing production code.

The test:
1. Loads the existing production `VoxelShowcase` and primes real GPU extraction exactly like the green relocation regression.
2. Selects one already-ready block from the old location and holds a one-block coverage demand on it.
3. Relocates the showcase 384 m in X.
4. Whenever the old control block is ready, invokes the coordinator's real private `ApplyChange(VoxelChangeRecord)` path via reflection for that same old block. The held demand makes normal `ProcessRecovery` restore it, allowing sustained ready/change/recovery churn while the target remains hundreds of metres away.
5. Requires at least eight injected distant changes and retains the existing 20-second all-workers-admission-stall assertion plus four-useful-GPU-build recovery requirement.

The targeted-CI adapter clears/restores `VOXEL_DISABLE_GPU_CUTOVER` only around this GPU-only discriminator, matching the established adapter boundary.

## Selection rule
- If the discriminator fails with sustained distant changes while the unchanged relocation baseline remains green, H1 is demonstrated. Implement only a footprint-local invalidation correction; do not change budgets, concurrency, load radius, strict coverage, or Kentridge policy.
- If it passes, reject H1 and return to Kentridge-specific in-footprint recovery/throughput evidence before any renderer edit.

No production correction is selected until the exact-SHA discriminator completes.
