# Experiment 010 — cold Editor/Metal batch pipeline compilation

## Hypothesis

Experiment 009 matched the legacy fixture's second shared context. If that matched diagnostic still passes while the legacy test remains the only failure, shared-context lifetime is falsified. Compare first-use shader activity with later successful full publications before changing renderer behavior or the five-second transaction deadline.

## Exact input

- Feature/source SHA: `e03d7b60ff14eaffe3940c23d7ce158a228e6ed1`
- Exact request SHA: `152b97d3365a05117b848f456fb6d4f46b4de009`
- Run/job: `34194247464` / `101958423704`
- Artifact: `single-test-34194247464`, id `10044112801`
- Artifact digest: `sha256:b12be3542ff3e3c49dd81871774137d88b508811194ffb5ab9bd93239a07f30a`
- Unity 6000.5.6f1 / Apple M4 Max / Metal

## Evidence

Persistent Rendering EditMode completed 575 tests: 574 passed, 1 failed, 0 quarantined and 0 inconclusive. The only failure remained `GpuQueuedBatchCancellationTests.FineStepFourPublishesRealMixedStorageThroughGpuReadiness`, which took 14.448 seconds and failed the unchanged five-second publication deadline.

The matched two-context diagnostic `GpuStepFourPublicationPhaseTests.StepFourMixedPublicationReportsItsTerminalPhaseWithinBound` passed in 0.034 seconds in the same persistent editor. It completed in 14 scheduler iterations with `maxPrepareMs=4.446`, saw real summary submission and full submission, produced real geometry and released all demand. Therefore an idle second shared-mirror consumer does not reproduce the failure.

The failing legacy step-4 case is the first successful full `VoxelBrickMesher` batch publication in that fixture. During that one test the editor emitted 714 compute-shader warnings, including 640 `VoxelBrickMesher` warnings across `CSBatchSampleDensity`, count, faceted, decoration, transition and write kernels. The immediately following real mixed publications passed in 0.029 seconds (step 1) and 0.026 seconds (step 2), with zero `VoxelBrickMesher` shader warnings. Earlier step-4 edit and retirement cases complete before the full mesher publication chain and emitted no mesher warnings.

This isolates the 14-second wall-clock overrun to one-time Editor/Metal batch-kernel compilation on first successful dispatch, not source readiness, shared-context ownership, GPU outcome readback, fence completion or steady-state step-4 publication. Standalone player startup/performance remains a separate acceptance surface and is not inferred from this editor-only result.

## Repair

Add a module-level EditMode `SetUpFixture` that executes the exact mixed-storage step-4 production coordinator path once before bounded tests. The warm-up must itself reach real summary submission, real full batch submission, non-failed paged completion and nonzero geometry within a 30-second cold-compiler setup ceiling, then release all demand/resources. It drains only coordinator-issued async GPU readbacks. Existing per-transaction five-second deadlines are unchanged.

Implementation through `893b08c8de345e1af22991bf7202fa215073f844` changes tests only; renderer/runtime code, device budgets, content, distance, quality and CI limits are untouched.

## Verdict / next step

H1 shared-context lifetime is falsified for the remaining module red gate. The demonstrated gate cause is cold Editor/Metal shader compilation being charged to a steady-state liveness assertion. Validate the warm-up fixture on exact SHA. If the module suite becomes green, return immediately to the still-open full-scene correctness work; a green module suite does not satisfy VoxelShowcase acceptance.
