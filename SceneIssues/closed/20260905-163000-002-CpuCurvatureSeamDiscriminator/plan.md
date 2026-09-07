# R02 — CPU curvature versus LOD seam discriminator

Investigation only; 45–60 minutes active work. Start from current `origin/master`; initial audit: `513ae04ca`. Independent of the other R issues. Follow [review protocol](../../rendering-review/protocol.md) and the canonical SceneIssue workflow.

## Observed behavior

CPU extraction mixes density reconstruction, authored boundaries, faceted surfaces and coarse block HLOD. CpuTransvoxelChunkCache deliberately omits transition geometry for step-8 HLOD and step-4 feature-preserving fallback. Existing predicate tests do not prove rendered continuity.

## Hypotheses and next experiment

H1: scalar/authored-boundary reconstruction is already wrong within one LOD. H2: individual meshes are sound but adjacent reconstruction/LOD paths disagree at their shared boundary.

Select one production-authored curved surface crossing a step-4/step-8 boundary, with a same-LOD view as control. Trace its real sampling and transition ownership; compare the identical surface before/after crossing the ring boundary. A defect present away from a boundary falsifies a seam-only explanation. Do not build a replacement mesher.

## Ownership and scope

Read these production/evidence owners (paths relative to repository root):

- `Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs`
- `Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/Transvoxel/TransvoxelDensityJob.cs`
- `Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/Transvoxel/TransitionMeshJob.cs`
- `Assets/VoxelEngine/Rendering/Tests/EditMode/CoarseLodDensityReconstructionTests.cs`
- `Assets/VoxelEngine/Rendering/Tests/EditMode/Step4FalseEmptyRegressionTests.cs`

Write only this issue’s findings/evidence. No product, test, scene, configuration, budget or CI implementation changes in this first investigation wave. Existing runners/diagnostics may be used under repository rules. If a missing fixture/instrumentation prevents the experiment, identify the smallest prerequisite and remain open; do not construct a substitute or claim completion. No module scene is being changed by this documentation/evidence task.

## Acceptance and remaining gates

Produce a reproducible result that distinguishes the hypotheses, or a precise unresolved blocker. Record source/transport/run SHA where applicable, paths to durable evidence, limitations, falsified hypotheses and a narrow recommended next step in `findings.md`. Inspect player images for any visual claim. A complete negative result can close the investigation; an unperformed required experiment cannot. Architecture/repair tasks wait for coordinator review. Results: pending.
