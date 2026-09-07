# R01 — Renderer identity and moving-frame baseline

Investigation only; 45–60 minutes active work. Start from current `origin/master`; initial audit: `513ae04ca`. Independent of the other R issues. Follow [review protocol](../../rendering-review/protocol.md) and the canonical SceneIssue workflow.

## Observed behavior

The user reports artifacts in both backends and historical 400 FPS. Local eb13c3e3b forces GPU cutover off; master uses an environment switch. Static FPS does not measure extraction during movement or edits.

## Hypotheses and next experiment

H1: settled rendering is cheap but extraction/upload backlog dominates moving frames. H2: persistent drawing/presentation cost dominates even after builds settle.

Use one unchanged production VoxelShowcase standalone build, one fixed camera interval and one short repeatable traversal. Compare CPU-disabled-GPU and normal policy only through existing switches; record actual GPU/CPU completion and visible-backend counters. Include one existing scripted edit if the harness supports it. Stop if input, build or backend selection is broken; retain the failure as a blocker, not a performance result.

## Ownership and scope

Read these production/evidence owners (paths relative to repository root):

- `Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/VoxelSurfaceScheduler.cs`
- `Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs`
- `Assets/VoxelEngine/Rendering/Runtime/GpuVoxel/GpuSurfaceDrawDispatcher.cs`
- `Assets/VoxelEngine/Rendering/Runtime/RenderFeature/VoxelRenderPass.cs`
- `tools/showcase-player-capture.sh`

Write only this issue’s findings/evidence. No product, test, scene, configuration, budget or CI implementation changes in this first investigation wave. Existing runners/diagnostics may be used under repository rules. If a missing fixture/instrumentation prevents the experiment, identify the smallest prerequisite and remain open; do not construct a substitute or claim completion. No module scene is being changed by this documentation/evidence task.

## Acceptance and remaining gates

Produce a reproducible result that distinguishes the hypotheses, or a precise unresolved blocker. Record source/transport/run SHA where applicable, paths to durable evidence, limitations, falsified hypotheses and a narrow recommended next step in `findings.md`. Inspect player images for any visual claim. A complete negative result can close the investigation; an unperformed required experiment cannot. Architecture/repair tasks wait for coordinator review. Results: pending.
