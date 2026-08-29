# Plan — SceneIssue 20260825-192751-413 VoxelShowcase

## Observed defect / acceptance
- The single capture marks the top-left performance telemetry at `Showcase Camera` `(77.953941,24.550051,-3.345814)`, FOV 70. Capture metadata records sub-100 FPS while moving, slow fill, and transient/missing geometry.
- Final acceptance is the exact built `VoxelShowcase`: visible near/mid geometry survives traversal and settles without holes or runtime exceptions; supported step-1/2 work stays on GPU with zero eligible CPU fallback/blocking completion and existing frame-time gates unchanged.

## Runtime evidence / competing hypotheses
- Demand-scoped mirror recovery removed the original ~0.65–0.77 s global-mirror stall. Recovery fairness plus bounded 512-descriptor/64-mixed publication slices restored forward GPU progress.
- Exact candidate `b650932f7b35323948d75b92bc65a1a34c6ec194` ran both focused tests and the built player. Liveness passed; built-player harness reached 45 s with zero harness assertions. The migration traversal failed only because 3 of 92 GPU-eligible attempts fell back (`gpuCompleted=89`, expected fallback 0).
- Built-player replay is severely incomplete at 15.4 s but visually coherent by 25.4/35.5 s, so this candidate no longer shows a permanent renderer/world-data failure.
- Arena exhaustion is disfavored by prior `leaseFail=0` evidence. The remaining discriminator is late completion: transient readback/device failure versus a valid zero-geometry count being sent through a redundant write/readback verification path.

## Current fix
- Feature is synchronized with `origin/master` through merge `81557283b3c8a73983a6a00b2a597115aca10882`.
- Candidate `cdd6674797f12be190a6254d20dd30ffbf2ba283` keeps the authoritative zero count staged, uses only the existing tiny caller sizing token, skips the redundant write dispatch/readback for zero geometry, and returns `Ready/0` to the existing publication path. Non-empty writes, retry limits, arena sizing, shaders, Storage, topology, recovery budgets, and performance thresholds are unchanged.
- Cost/blast radius: only GPU-eligible empty completions change. They remove one compute dispatch/readback each; non-empty GPU and all CPU/HLOD/water paths are unchanged.

## Regression / remaining gates
- `GpuSurfaceMirrorRecoveryLivenessTests.DemandRecoveryCannotBeStarvedByCoveredGpuWork`: exact 96 m recovery liveness.
- `ShowcaseGpuMigrationTests.MovingShowcaseCompletesGpuSurfaceBuildsAndPreservesCoverage`: exact 210 m traversal, sustained GPU completions, zero eligible fallback/blocking completion, unchanged moving/stationary frame budgets and settled coverage.
- Next: final exact-head targeted CI + built-player replay. Only green exact-SHA gates permit pending/closed metadata and master push.
