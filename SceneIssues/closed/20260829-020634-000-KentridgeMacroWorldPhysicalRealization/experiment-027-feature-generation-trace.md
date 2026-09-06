# Experiment 027 — Structure Generation Publication Trace

## Question
Where do authored Kentridge settlement structures disappear between source-backed physical planning and the final authoritative voxel field observed by Experiment 026?

## Constraint
Experiment 026 reproduced the same acceptance symptom after two materially different remediation attempts. Per assignment rules, no further geometry fix is permitted until a minimal runtime discriminator selects the failing boundary.

## Instrumentation
Shared `FeatureGenerationTrace` emits low-volume `FEATUREGEN_TRACE` records for `FeatureKind.Structure` only:
- `candidate`: placement reached a streamed region after footprint intersection.
- `rejected`: instance evaluation failed, with exact `EvaluationResult`.
- `accepted`: instance evaluation succeeded, with primitive count.
- `completed`: rasterization/mutation finished, with `rasterised` and per-instance `voxels` delta.

Tracing is enabled by the existing `-voxel-scene-issue` diagnostic flag (or explicit `--feature-generation-trace`) and remains silent for ordinary player launches.

## Discriminator
- No candidate for an authored building: investigate catalogue/rule selection or region scheduling.
- Candidate then rejected: investigate evaluator/footprint/shape semantics.
- Accepted then completed with `voxels=0`: investigate primitive rasterization or mutation writes.
- Accepted then completed with `voxels>0` while `MACROEVIDENCE end-frame-survey` still reports terrain at the same settlement building: investigate later terrain overwrite, store replacement, or publication ordering.

## Validation
Pending exact-SHA CI replay on the post-documentation feature head. The run will use `VoxelEngine.Tests.PlayMode.KentridgeMacroWorldPhysicalStorageAcceptanceTests.PhysicalMacroWorldReachesProductionStorageWithSettlementShellAndRoof` as the focused production-storage gate and the 60-second SceneIssue built-player replay so compile/storage behavior, runtime trace logs, and final authoritative survey come from one exact source SHA.

## Result
Pending.
