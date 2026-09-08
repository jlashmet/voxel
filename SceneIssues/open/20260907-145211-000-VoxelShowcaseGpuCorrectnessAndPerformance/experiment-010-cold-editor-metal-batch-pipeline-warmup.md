# Experiment 010 — cold Editor/Metal batch pipeline compilation

## Hypothesis

Experiments 008/009 isolated the legacy step-4 timeout from source readiness and shared-context lifetime. The remaining question was whether one-time Editor/Metal shader compilation should be separated from the submitted batch's asynchronous publication bound.

## Exact failed warm-up experiment

- Feature/source SHA: `f51184569f55ddc954e63e5915e816fecb6a9b2b`
- Exact request SHA: `e296a25c4cf526f52e9d35bb1ce356aa79b1dd7d`
- Run/job: `34197667062` / `101968908805`
- Artifact: `single-test-34197667062`, id `10045769921`
- Artifact digest: `sha256:35fdc3ce61a497b1282e982213500c3b5d4f7d517614664fe40af9ee5f6be622`
- Unity 6000.5.6f1 / Apple M4 Max / Metal

## Evidence

The attempted module-global `SetUpFixture` warm-up is invalid. Automatic Rendering validation failed in one-time setup before the normal suite: the warm-up did not observe `SummarySubmitted`, so NUnit reported 229 parent/test failures from the failed setup. The standalone VoxelShowcase replay still completed successfully. This is a test-harness failure, not evidence of a new renderer regression.

The preceding exact run `34194247464` remains the causal discriminator: the matched two-context production-path diagnostic passed in 0.034 seconds (`maxPrepareMs=4.446`) while the legacy first full step-4 case took 14.448 seconds. That first full batch emitted 640 `VoxelBrickMesher` Editor/Metal warnings; immediately following real step-1/2 publications completed in 0.029/0.026 seconds with no mesher warnings.

Current master `b56436f198702fa91bfaccec69a30df25a52a2cd` already contains the narrower shared correction (`8bb6663324e88ae36abaf723e7141440eccbf40f`): `GpuQueuedBatchCancellationTests.ValidateSummaryLane` keeps the five-second publication contract, but starts a fresh five-second asynchronous publication window when the real lane first reaches `Submitted`. One-time synchronous Editor/Metal compilation during submission is therefore not charged as post-submission liveness, while source preparation and actual submitted-batch publication remain bounded and runtime behavior is unchanged.

## Reconciliation

Merge commit `292c2768258e89b19f006bc6b737e278ff5c6d5a` integrates current master into `fixes/agent-3`, preserves the assignment's production admission/coverage work and diagnostics, and deliberately omits the invalid global warm-up source/meta. No renderer capacity, content, distance, quality, device budget or production deadline was weakened.

## Verdict / next step

The global warm-up hypothesis is rejected. Validate the merged source on exact SHA with `FineStepFourPublishesRealMixedStorageThroughGpuReadiness`. A green Rendering gate is only a prerequisite; full-scene correctness, performance, migration, lifetime and final integration acceptance remain open.
