# Experiment 014 — final traversal frame-time failure

## Question

Does the initial clipmap priority-discovery fix make the production moving traversal both coverage-correct and fast enough under the existing acceptance gate?

## Exact request

- CI request: `6da72281f0f741c0b254681b337fe1f807c47b29`
- source parent: `1ddb80f57d06e95e53a7f9d1317d12a33ce4dd36`
- workflow run: `33030560453`
- job: `98381856708`
- test: `VoxelEngine.Tests.PlayMode.ShowcaseTraversalPerformanceTests.ContinuousPlayerTraversalNeverStuttersOrOpensNearFarGap`
- scene replay: this capture, 45 seconds

## Result

The exact request completed and the test failed its unchanged moving-frame performance threshold:

- frames: 420
- p95: **18.71 ms** (limit `< 18.0 ms`)
- p99: **29.40 ms** (limit `< 25.0 ms`)
- max: **706.89 ms**

The earlier correctness failure did **not** recur. The traversal retained visible voxel draws, did not report synchronous frame-path geometry completion, and the test reached its final frame-time assertion. Therefore this run supports the initial-discovery fix for coverage, but does not demonstrate acceptable moving frame-time variance.

## Saved-pose replay evidence

The same workflow's real-player replay is visually complete at the capture pose. After startup/convergence, one-second windows were typically about **265–324 FPS**. The final screenshot reports about **276 FPS**. Late stationary windows commonly had p50 around 1.8–3.5 ms and p95 around 3.8–10.7 ms depending on ongoing background publication.

Representative late surface diagnostics showed no missing visible chunks and scheduler/admission work generally below 1 ms per frame, while occasional recorded upload maxima reached about 8.6 ms. These values are evidence to investigate, not proof of a single cause.

## Interpretation / next discriminator

The remaining failure is a moving-frame timing problem, not the old all-geometry-disappears failure. Aggregate frame time cannot identify its cause. Execute `bottleneck-investigation-plan.md`: first split the same traversal into player-loop versus explicit render time and correlate the slowest frames with streaming/meshing/upload/GC profiler markers. Only then select a subsystem-subtraction experiment or production optimization.
