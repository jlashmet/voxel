# Experiment 021 — Physical worker visibility isolation

## Hypothesis
The moving frame-5 zero is either (A) a destructive aggregation/LOD-selection step that erases already-valid worker-visible geometry, or (B) an earlier ring/worker visibility failure where no physical entries survive band/frustum/current-state filtering.

## Action
No production code change. Keep `ShowcaseGpuMigrationTests.MovingShowcaseKeepsLegacyGpuV1OffAndPreservesCoverage` as the exact behavioral gate and preserve every existing coverage/performance assertion. Only when `VisibleSolidChunks == 0`, reflect the already-active production `VoxelRenderPass` → `VoxelSurfaceScheduler` → rings and record, per source step:

`known / inBand / frustum / ready / empty / physical worker.Visible`

Reflection runs only on the failing frame and does not alter scheduler state or runtime APIs.

The historical control `1ddb80f...` used the same direct showcase-transform traversal, so test-control drift is already falsified.

## Falsification / interpretation
- `physicalTotal > 0` while aggregate `VisibleSolidChunks == 0`: worker visibility is healthy; cross-ring aggregation/selection is the causal deletion point.
- `physicalTotal == 0`: aggregation is not the immediate deletion point; use the ring funnel to identify whether ownership, frustum, readiness, or residency dropped physical visibility first.

## Current evidence
Exact source `06e37c5526d0a6f9e16496f9f102073c5fbd36a6`, transport `06ea24bc32994d230facac57883aa919c51dcb25`, run `33142076200`, artifact `9674586172` still lost all aggregate solids at moving frame 5. Its real-player replay later converged, so the defect remains transient movement/convergence coverage.

## Next step
Run the unchanged end-to-end PlayMode filter on the test-only diagnostic source. Do not change production code until the first zero frame identifies which side of the physical/aggregate boundary failed.
