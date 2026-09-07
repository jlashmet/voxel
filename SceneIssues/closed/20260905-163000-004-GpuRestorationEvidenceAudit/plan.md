# R04 — GPU restoration evidence audit

Investigation only; 30–45 minutes active work. Start from current `origin/master`; initial audit: `513ae04ca`. Independent of the other R issues. Follow [review protocol](../../rendering-review/protocol.md) and the canonical SceneIssue workflow.

## Observed behavior

The open GpuRendererProductionRestoration issue already owns GPU repairs and records Metal compiler failures plus partial parity successes. Its historical successes do not establish current master acceptance. GPU accelerates source steps 1/2; CPU still owns coarser rings.

## Hypotheses and next experiment

H1: current master still has a sample/compiler/semantic failure. H2: the repaired sample path passes and the remaining failure lies in publication, production selection or missing acceptance evidence.

Reconcile the existing restoration issue, merged source and its exact-SHA workflow artifacts. Inspect executed tests, compiler logs and actual production backend proof. This is an evidence audit, not a competing repair or another full parity suite. If current-SHA evidence is absent, report that gap and the smallest required discriminator without scheduling dependent implementation.

## Ownership and scope

Read these production/evidence owners (paths relative to repository root):

- `SceneIssues/open/20260902-171853-000-GpuRendererProductionRestoration`
- `Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/GpuSurfaceProductionPolicy.cs`
- `Assets/VoxelEngine/Rendering/Runtime/GpuVoxel`
- `Assets/VoxelEngine/Rendering/Resources/VoxelBrickCacheResolver.compute`
- `Assets/VoxelEngine/Rendering/Tests/EditMode/GpuProductionPreparedBatchRuntimeTests.cs`
- `Assets/VoxelEngine/Rendering/Tests/EditMode/GpuSurfaceExtractorOracleTests.cs`

Write only this issue’s findings/evidence. No product, test, scene, configuration, budget or CI implementation changes in this first investigation wave. Existing runners/diagnostics may be used under repository rules. If a missing fixture/instrumentation prevents the experiment, identify the smallest prerequisite and remain open; do not construct a substitute or claim completion. No module scene is being changed by this documentation/evidence task.

## Acceptance and remaining gates

Produce a reproducible result that distinguishes the hypotheses, or a precise unresolved blocker. Record source/transport/run SHA where applicable, paths to durable evidence, limitations, falsified hypotheses and a narrow recommended next step in `findings.md`. Inspect player images for any visual claim. A complete negative result can close the investigation; an unperformed required experiment cannot. Architecture/repair tasks wait for coordinator review. Results: pending.
