# Plan — SceneIssue 20260825-192751-413 VoxelShowcase

## Observed defect / acceptance
- The single capture marks top-left performance telemetry at `Showcase Camera` `(77.953941,24.550051,-3.345814)`, FOV 70. Note: sub-100 FPS while moving, slow fill, transient/missing geometry.
- Acceptance: exact built `VoxelShowcase` preserves near/mid geometry through traversal and settles without holes/runtime exceptions; supported step-1/2 work stays on GPU with zero eligible CPU fallback/blocking completion and existing frame-time gates unchanged.

## Runtime evidence / hypotheses
- Demand-scoped mirror recovery removed the original ~0.65–0.77 s global-mirror stall; fairness plus bounded recovery restored GPU progress.
- Exact run `33275543571` on `4722b747…`: liveness passed; migration reached `gpu=154/0` but failed settle at `visible=43 missing=579 dirty=1927 jobs=12`. Built captures: t15.4 almost empty; t25.4/t35.4 largely recovered; final incomplete. Late admission/worker `Prepare` spikes ~190–195 ms and FPS ~5–18.
- Arena exhaustion is falsified by `leaseFail=0` and unused capacity. Instrumented residency/selection sections remain small during whole-`Prepare` spikes.
- Leading hypothesis: fragmented mixed-brick mirror payload uploads. A 64-mixed recovery slice can occupy arbitrary reused slots; the old flush could issue four synchronous `SetData` calls per contiguous run.

## Current attempt / result
- `GpuVoxelBrickMirror` compacts dirty payloads into 64-record transfer batches; `VoxelBrickDirectoryUpdater.compute` scatters each contiguous transfer into existing live buffers. Larger journal-replay dirty sets split into multiple batches.
- Final request `0f7c958f…`, parent `33ae17d7…`, run/job `33277135240` / `99165718210`, artifact `9721973168`, was product-red before the performance discriminator: Metal rejected identifier `linear`, an HLSL keyword.
- Correction `18d72133342daa56ecfaa3c6d1f09e4a194cf205` renames only `linear` to `linearIndex`. Static review verified the C#/shader layout (`517` words/record, `513` copy threads/brick), buffer lifetime/accounting, and the coordinator's 64-mixed recovery ceiling. No second static defect found.
- Branch ancestry lost an earlier master merge during concurrent writes. Merge `86f02c43d9b244a65c98acc5fb31d3eaa894781e` repairs it with parents current feature and `origin/master` `922565bedd104c1795a9d13c610d4d185b65754e`; compare now reports `behind_by=0` and only agent-2 source/test/issue paths differ.
- Blast/cost: fixed 132,352-byte GPU staging plus same-size CPU staging; one ~132 KB upload + ~32.8k-thread scatter per full batch. World truth, CPU topology, HLOD, water, arena sizing, recovery budgets and thresholds are unchanged.

## Regression / remaining gate
- `GpuSurfaceMirrorRecoveryLivenessTests.DemandRecoveryCannotBeStarvedByCoveredGpuWork`.
- `ShowcaseGpuMigrationTests.MovingShowcaseCompletesGpuSurfaceBuildsAndPreservesCoverage`.
- Required green exact-SHA targeted CI is absent. The assignment forbids an extra CI transport, so keep this capture `open`; do not set pending/fixed metadata or push to master.
