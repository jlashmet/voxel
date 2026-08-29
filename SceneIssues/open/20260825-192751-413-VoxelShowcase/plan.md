# Plan — SceneIssue 20260825-192751-413 VoxelShowcase

## Observed defect / acceptance
- The single capture marks the top-left performance telemetry at `Showcase Camera` `(77.953941,24.550051,-3.345814)`, FOV 70. Capture metadata records sub-100 FPS while moving, slow fill, and transient/missing geometry.
- Acceptance: exact built `VoxelShowcase` preserves near/mid geometry through traversal and settles without holes/runtime exceptions; supported step-1/2 work stays on GPU with zero eligible CPU fallback/blocking completion and existing frame-time gates unchanged.

## Runtime evidence / hypotheses
- Demand-scoped mirror recovery removed the original ~0.65–0.77 s global-mirror stall; fairness plus bounded recovery restored GPU progress.
- Exact run `33275543571` on feature `4722b747…`: liveness passed; migration reached `gpu=154/0` but failed settle at `visible=43 missing=579 dirty=1927 jobs=12`. Built captures: t15.4 almost empty; t25.4/t35.4 largely recovered; final incomplete. Late admission/worker `Prepare` spikes ~190–195 ms and FPS ~5–18.
- Arena exhaustion is falsified by `leaseFail=0` and unused capacity. Instrumented residency/selection sections remain small during whole-`Prepare` spikes.
- Leading hypothesis: fragmented mixed-brick mirror payload uploads. A 64-brick recovery can map to arbitrary LIFO-reused slots; the old flush could issue four synchronous `SetData` calls per contiguous run, up to 256 driver uploads.

## Current attempt / result
- `GpuVoxelBrickMirror` compacts dirty payloads into fixed 64-brick transfer batches; `VoxelBrickDirectoryUpdater.compute` scatters one contiguous transfer into existing live buffers.
- Exact final request `0f7c958f…`, parent feature `33ae17d7…`, run/job `33277135240` / `99165718210`, artifact `9721973168`, was product-red before the performance discriminator: Metal rejected local identifier `linear` in `VoxelBrickDirectoryUpdater.compute` as a shader keyword. Both requested tests failed on that compile error.
- Correction `18d72133342daa56ecfaa3c6d1f09e4a194cf205` renames only `linear` to `linearIndex`. No extra CI transport is permitted by the assignment, so this correction is not exact-SHA validated and cannot promote the issue.
- Cost/blast radius remains bounded to mixed-brick GPU publication: fixed 132,352-byte GPU staging plus same-size CPU staging; one ~132 KB upload + ~32.8k-thread scatter per full batch replaces up to 256 fragmented uploads. World truth, CPU topology, HLOD, water, arena sizing, recovery budgets and performance thresholds are unchanged.

## Regression / remaining gate
- `GpuSurfaceMirrorRecoveryLivenessTests.DemandRecoveryCannotBeStarvedByCoveredGpuWork`: 96 m recovery liveness.
- `ShowcaseGpuMigrationTests.MovingShowcaseCompletesGpuSurfaceBuildsAndPreservesCoverage`: 210 m traversal, sustained GPU completion, zero eligible fallback/blocking completion, settled coverage and unchanged frame budgets.
- Required green exact-SHA targeted CI is absent. Keep this capture in `open/`; do not set pending/fixed metadata, merge to master, or claim completion.
