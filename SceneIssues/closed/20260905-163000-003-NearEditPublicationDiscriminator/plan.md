# R03 — Near-field edit publication and coverage

Investigation only; 45–60 minutes active work. Start from current `origin/master`; initial audit: `513ae04ca`. Independent of the other R issues. Follow [review protocol](../../rendering-review/protocol.md) and the canonical SceneIssue workflow.

## Observed behavior

CPU/GPU share scheduling and logical coverage. Immutable snapshots, generations and retained old geometry are useful safeguards, but isolated coverage tests do not prove end-to-end replacement after destruction.

## Hypotheses and next experiment

H1: a boundary edit misses a neighboring chunk or LOD invalidation. H2: invalidation is correct but staging/publication/coverage delays or drops replacement.

Perform one deterministic production edit crossing one chunk boundary with a stationary camera and fixed backend. Trace authoritative revision through desired build, consumed snapshot, pending upload and visible publication. Repeat once across the adjacent LOD handoff only if the first trace fits the timebox. Do not diagnose all destruction systems.

## Ownership and scope

Read these production/evidence owners (paths relative to repository root):

- `Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs`
- `Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/SurfaceLodCoverageState.cs`
- `Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/SurfaceLodActiveCoverage.cs`
- `Assets/VoxelEngine/Rendering/Tests/EditMode/SurfaceLodActiveCoverageTests.cs`

Write only this issue’s findings/evidence. No product, test, scene, configuration, budget or CI implementation changes in this first investigation wave. Existing runners/diagnostics may be used under repository rules. If a missing fixture/instrumentation prevents the experiment, identify the smallest prerequisite and remain open; do not construct a substitute or claim completion. No module scene is being changed by this documentation/evidence task.

## Acceptance and remaining gates

Produce a reproducible result that distinguishes the hypotheses, or a precise unresolved blocker. Record source/transport/run SHA where applicable, paths to durable evidence, limitations, falsified hypotheses and a narrow recommended next step in `findings.md`. Inspect player images for any visual claim. A complete negative result can close the investigation; an unperformed required experiment cannot. Architecture/repair tasks wait for coordinator review. Results: pending.
