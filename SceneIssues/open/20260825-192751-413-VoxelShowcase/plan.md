# Plan — SceneIssue 20260825-192751-413 VoxelShowcase

## Observed defect / acceptance
- Capture `screenshot-001.png` marks the performance telemetry at `Showcase Camera` `(77.953941,24.550051,-3.345814)`, FOV 70: sub-100 FPS while moving, slow fill, and transient/missing geometry.
- Pass requires sustained step-1/2 GPU completion, zero eligible CPU fallback/blocking waits, no visible holes, moving p95 <18 ms/p99 <25 ms, stationary p95 <8 ms, plus exact built-player pose inspection.

## Runtime evidence / hypotheses
- Demand-scoped mirror recovery, exact footprints, optional-empty halo handling, and cancellation removed earlier global recovery stalls.
- Compact 64-record payload scatter now compiles on Metal but is **falsified as the complete fix**. Exact request `562616c0…` (parent `0a1ee326…`, run `33279094247`) passed focused recovery liveness but migration lost all visible voxel draws at traversal frame 3 (`gpu=8/0`, waits=248). Built player remained incomplete at 51.4 s and still showed ~191–198 ms solid-admission spikes; `leaseFail=0`.
- Leading remaining cause was admission/readback head-of-line pressure plus repeated exact-footprint work, not arena capacity or payload-call fragmentation.

## Selected integration
- Current head `4754860892cf3ddd81a7d2fc8130d02fc58355f3` integrates snapshotless GPU admission, raw persistent-mirror classification, reference-counted demand footprints, 128-block incremental coverage checks, inactive-undemanded mirror eviction, one new count dispatch per frame, and concurrent recovery progress that skips blocks still used by active extractions.
- Unsupported decorated/faceted/profile content explicitly returns to the CPU fidelity path. CPU world truth, collision, replication, HLOD/water, stale-build rejection, arena budgets, and performance thresholds are unchanged.
- Local evidence recorded on the takeover lineage: compile succeeds; policy 5/5, mirror slot/catalogue 21/21, arena/no-geometry-readback 4/4 pass. The liveness PlayMode run exceeded the mandated 6 GB local ceiling before its first assertion, so it provides no product verdict.

## Remaining gate
- Current head has no exact-SHA Actions run. The only final targeted-CI transport already issued (`562616c0…`) is product-red and predates the current integration.
- Assignment explicitly forbids extra CI transports or replacement of the consumed request. Therefore the required green exact-SHA targeted CI and exact-head built-player validation cannot be obtained within the allowed workflow.
- Keep this capture under `open/`; do not set pending/fixed metadata, move folders, or push this branch to `master` without a newly authorized exact-SHA gate.
