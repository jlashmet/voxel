# Plan — restore validated GPU surface extraction for the Showcase

## Problem

The capture reports sub-100 FPS traversal, slow terrain fill, and visible holes while chunks are meshed in `Assets/Scenes/VoxelShowcase.unity`, with a goal of leaving enough frame budget for game logic. Current production code contains the existing GPU Transvoxel implementation but `CpuTransvoxelChunkCache.GpuCutoverDisabled` hard-disables it for every ring, forcing exact terrain back through CPU extraction and CPU-to-GPU geometry upload.

Historical player profiling already showed that settled drawing is not the dominant cost: the renderer can submit the full solid set cheaply, while active CPU surface builds consume the frame and delay visible coverage. The GPU extractor has CPU-oracle parity coverage, writes directly into the shared surface arena, uses asynchronous counter readback, and supports exact steps 1 and 2; step 4 and block HLOD intentionally remain CPU/coarse paths.

## Approach

1. Capture a red policy regression proving that production currently hard-disables the validated GPU backend for exact rings.
2. Restore GPU cutover as the production default while retaining the existing CPU fallback for unsupported devices, failed GPU-context creation, ineligible chunks, step 4, and step 8. Keep an explicit emergency environment override rather than another unconditional compile-time rollback.
3. Run the focused cutover regression first, then GPU density/geometry oracle and arena-bridge coverage so the restored path is checked against authoritative CPU output and shared-arena publication.
4. Run broader rendering/runtime CI required by the repository workflow.
5. Replay/benchmark the assigned VoxelShowcase path through the repository CI harness. Require visible coverage to converge without missing geometry and record measured frame behavior; do not claim the aspirational 1000-FPS target unless the player evidence actually demonstrates it.
6. Only after verified production/test work is pushed, update `issue.json` and move the entire capture to `SceneIssues/closed/` in a separate bookkeeping commit.

## Constraints

- Do not introduce a second renderer or bypass `VoxelSurfaceScheduler` / `SurfaceGeometryArena`.
- Do not enable GPU extraction for step 4 or block HLOD until their dedicated parity contracts support it.
- Preserve CPU fallback for hardware without compute/async-counter support and for content the GPU path intentionally declines.
- Never edit `.github/test-request.json` on `fixes/agent-2`; targeted requests belong only on `ci-test/fixes/agent-2`.
