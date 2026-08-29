# Plan — SceneIssue 20260825-192751-413 VoxelShowcase

## Observed defect / acceptance
- The single capture marks the top-left performance telemetry at `Showcase Camera` `(77.953941,24.550051,-3.345814)`, FOV 70. Capture metadata records sub-100 FPS while moving, slow fill, and transient/missing geometry.
- Final acceptance is the exact built `VoxelShowcase`: visible near/mid geometry survives traversal and settles without holes or runtime exceptions; supported step-1/2 work stays on GPU with zero eligible CPU fallback/blocking completion and existing frame-time gates unchanged.

## Runtime evidence / competing hypotheses
- Demand-scoped mirror recovery removed the original ~0.65–0.77 s global-mirror stall. Recovery fairness plus bounded 512-descriptor/64-mixed publication slices restored forward GPU progress.
- Exact run `33275543571` on feature `4722b74771ab2a265157d800bdf9500f7ffcb9fe` proves the empty-stage completion fix worked narrowly: recovery liveness passed and migration reached `gpu=154/0`, eliminating eligible CPU fallback. The migration still failed to settle: `visible=43`, `missing=579`, `dirty=1927`, `jobs=12` after 20 s.
- All four exact built-player captures were inspected. t15.4 is almost empty; t25.4/t35.4 recover the castle/town; final remains incomplete. FPS starts around 245–264, then late individual solid-worker `Prepare` / solid admission spikes reach ~190–195 ms and the player falls to roughly 5–18 FPS.
- Arena exhaustion is falsified by `leaseFail=0`, unused capacity and negligible relief. Internal worker sections remain small during the large whole-`Prepare` spikes, pointing at the uninstrumented GPU mirror/admission/dispatch path.
- Source discriminator: mixed-brick recovery stages up to 64 arbitrary mirror slots. LIFO slot reuse can fragment those indices; the previous payload flush issued four synchronous `ComputeBuffer.SetData` calls per contiguous dirty-slot run. A maximally fragmented recovery slice therefore permits 256 driver uploads inside one worker `Prepare`.

## Current fix
- Empty GPU results still skip redundant write/readback verification; that proven zero-fallback behavior is retained.
- `GpuVoxelBrickMirror` now compacts dirty payload slots into fixed 64-brick transfer batches. `VoxelBrickDirectoryUpdater.compute` scatters one contiguous transfer into the unchanged material/surface/boundary/metadata slot buffers before directory deltas and extraction consume them.
- Recovery/admission budgets, slot mapping, Storage generations, live payload layout, extraction shaders, geometry arena, CPU topology, HLOD, water and performance thresholds are unchanged.
- Cost/blast radius: fixed payload staging is 64 × 517 uints = 132,352 bytes GPU plus the same CPU staging. A full batch is one ~132 KB upload + one ~32.8k-thread scatter dispatch, replacing up to 256 fragmented `SetData` calls.
- Feature is synchronized with `origin/master` `922565bedd104c1795a9d13c610d4d185b65754e` through merge `b38e8d68840372243219f17c89a1c0ccfa8398fe`; the master-only paths belonged to the separate reopened town-architecture assignment and did not overlap agent-2.

## Regression / remaining gates
- `GpuSurfaceMirrorRecoveryLivenessTests.DemandRecoveryCannotBeStarvedByCoveredGpuWork`: exact 96 m recovery liveness.
- `ShowcaseGpuMigrationTests.MovingShowcaseCompletesGpuSurfaceBuildsAndPreservesCoverage`: exact 210 m traversal, sustained GPU completions, zero eligible fallback/blocking completion, unchanged moving/stationary frame budgets and settled coverage.
- Next: one exact-head targeted CI + 45 s built-player replay. Only green exact-SHA gates permit pending/closed metadata and master push.
