# Plan — SceneIssue 20260825-192751-413 VoxelShowcase

## Observed defect / acceptance
- The single capture marks the top-left performance telemetry at `Showcase Camera` `(77.953941,24.550051,-3.345814)`, FOV 70. The report is sub-100 FPS while moving, slow fill, and transient/missing geometry.
- Final acceptance is the exact built `VoxelShowcase`: visible near/mid geometry survives traversal and settles without holes or runtime exceptions; supported step-1/2 work stays on GPU with zero eligible CPU fallback/blocking completion and existing frame-time gates unchanged.

## Proven results / competing hypotheses
- Demand-scoped exact-block mirror recovery removed the original ~0.65–0.77 s global-mirror stall. Optional nonresident halo acceptance and recovery fairness restored forward GPU progress; all-worker demand coalescing over-batched recovery and was rejected.
- Bounded recovery (512 cheap descriptors, unchanged 64 mixed publications/slice) made focused 96 m liveness pass. Exact run `33267842712` still failed the 210 m traversal at frame 459: `visible=0`, `missing=638`, `gpuCompleted=64`, `gpuFallback=7`.
- The same built player starts near 230–280 FPS then collapses to ~5–7 FPS with ~329 missing chunks and ~189–197 ms solid admission. Arena occupancy stayed below half and `leaseFail=0`, falsifying arena exhaustion.
- Current hypotheses: (1) transient async counter-readback failures are being misrouted into the full CPU Transvoxel chain; (2) fallbacks are instead `Ready + empty` or deterministic count/write mismatch.

## Current discriminator / fix
- Commit `ad6c8972513c5ae272ee6189b74095d41c807180` retries failed four-word count/write readbacks at most twice on the same staged request, stable mirror extraction window, and same unpublished write lease. It exposes retry counters and leaves deterministic/device fallback behavior intact.
- Recovery budgets, arena sizing, shaders, Storage, world truth, CPU topology algorithms, water/HLOD/visibility, and performance thresholds are unchanged. No per-frame collection allocation was added.

## Regression / remaining gates
- `GpuSurfaceMirrorRecoveryLivenessTests.DemandRecoveryCannotBeStarvedByCoveredGpuWork`: exact scene, 96 m demand/recovery liveness.
- `ShowcaseGpuMigrationTests.MovingShowcaseCompletesGpuSurfaceBuildsAndPreservesCoverage`: exact scene, 210 m traversal, >=8 GPU completions, zero eligible fallback/blocking completion, moving p95 <18 ms / p99 <25 ms, settled missing=0, stationary p95 <8 ms.
- Feature includes `origin/master` through `bc059307`. Next: one exact-head targeted CI + built-player replay. If fallback persists with zero readback retries, reject hypothesis (1) and fix empty/deterministic GPU completion semantics rather than raising budgets. Only green gates permit pending/closed metadata and master push.
