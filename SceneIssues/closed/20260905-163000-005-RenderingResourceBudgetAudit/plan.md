# R05 — Rendering resource and distance contract audit

Investigation only; 45–60 minutes active work. Start from current `origin/master`; initial audit: `513ae04ca`. Independent of the other R issues. Follow [review protocol](../../rendering-review/protocol.md) and the canonical SceneIssue workflow.

## Observed behavior

Default rings end full source-step-1 detail at 96 m, versus the matrix’s 400 m PC full-detail target. SolidArenaCommittedBytes reports the CPU geometry arena, excluding GPU arena/mirror cost. GPU page tables add substantial metadata beyond payload.

## Hypotheses and next experiment

H1: nominal arena accounting hides a total-resource budget violation. H2: resources fit, but coverage/backpressure or a mismatched distance contract causes the visible failure.

Build a count×stride allocation ledger for the actual default PC configuration and map all device-tier constants to the matrix. Separate shared versus per-worker allocations and resident versus reserved/scratch costs. Reconcile with existing runtime samples where available. Do not increase budgets or claim a short run proves long-session flatness.

## Ownership and scope

Read these production/evidence owners (paths relative to repository root):

- `Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/VoxelSurfaceScheduler.cs`
- `Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/SurfaceGeometryArena.cs`
- `Assets/VoxelEngine/Rendering/Runtime/GpuVoxel/GpuSurfacePageArena.cs`
- `Assets/VoxelEngine/Rendering/Runtime/GpuVoxel/GpuSurfaceMirrorCoordinator.cs`
- `specs/001-destructible-voxel-engine/device-matrix.md`

Write only this issue’s findings/evidence. No product, test, scene, configuration, budget or CI implementation changes in this first investigation wave. Existing runners/diagnostics may be used under repository rules. If a missing fixture/instrumentation prevents the experiment, identify the smallest prerequisite and remain open; do not construct a substitute or claim completion. No module scene is being changed by this documentation/evidence task.

## Acceptance and remaining gates

Produce a reproducible result that distinguishes the hypotheses, or a precise unresolved blocker. Record source/transport/run SHA where applicable, paths to durable evidence, limitations, falsified hypotheses and a narrow recommended next step in `findings.md`. Inspect player images for any visual claim. A complete negative result can close the investigation; an unperformed required experiment cannot. Architecture/repair tasks wait for coordinator review. Results: pending.
