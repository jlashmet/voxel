# Plan — SceneIssue 20260825-192751-413 VoxelShowcase

## Observed defect / acceptance
- One capture marks the top-left performance telemetry at the recorded `Showcase Camera` pose `(77.953941,24.550051,-3.345814)`, FOV 70. The issue reports sub-100 FPS while moving, slow scene fill, and transient/missing geometry.
- Final acceptance is the exact `VoxelShowcase` built player: visible near/mid geometry must survive traversal and settle without holes, with no startup/runtime exception. Focused traversal must keep the implemented GPU path live with zero eligible CPU fallback or blocking completion and meet the existing frame-time gates.

## Runtime evidence / competing hypotheses
- Global resident-world mirror recovery was causal for the original ~0.65–0.77 s/frame stall; demand-scoped recovery removed it.
- Whole-region recovery was also causal; exact demanded 8³ blocks restored early GPU completions.
- Optional nonresident halo mismatch was causal but insufficient; accepting halo-only nonresident blocks changed permanent freeze into slow convergence.
- Recovery starvation was real: allowing every pending worker to discover demand made the focused liveness regression pass, but exact run `33254303476` then over-batched the union and the 210 m traversal failed with zero GPU completions. That strategy is rejected.
- Current hypotheses: (1) a single 18³ step-2 footprint is still slow because the old 64-block slice spends most of its budget on cheap empty/uniform descriptors; (2) mixed payload publication/count-write latency is actually dominant.

## Selected discriminator / fix
- Keep one-footprint-at-a-time demand discovery and the fairness rule that a queued recovery backlog drains before covered work reacquires extraction.
- Split recovery into bounded work: at most 512 descriptor classifications and at most 64 mixed payload publications per preparation slice. The expensive mixed ceiling is unchanged; journal replay remains 128 records/slice; mirror mutation remains forbidden during active extraction.
- Empty/uniform blocks use borrowed region views and compact directory metadata; only mixed blocks pin/copy the 512-voxel payload. No wider world scan, CPU fallback, blocking GPU wait, larger arena, shader-layout change, or per-frame collection allocation.

## Regression / remaining gates
- `GpuSurfaceMirrorRecoveryLivenessTests.DemandRecoveryCannotBeStarvedByCoveredGpuWork`: exact scene, 96 m traversal, direct mirror liveness, optional-halo coverage, sustained GPU completions.
- `ShowcaseGpuMigrationTests.MovingShowcaseCompletesGpuSurfaceBuildsAndPreservesCoverage`: exact scene, 210 m traversal, >=8 GPU completions, zero eligible fallback/blocking completion, moving p95 <18 ms / p99 <25 ms, settled missing=0, stationary p95 <8 ms.
- Current feature includes `origin/master` through `bc059307`. Remaining gates: one fresh exact-head targeted CI request plus exact built-player replay/artifact inspection at the assigned scene/pose; only then pending/closed metadata and final master merge/push.

## Blast radius / cost
Solid step-1/step-2 GPU mirror recovery only. Water, HLOD, visibility, Storage writes, collision, world content, and arena sizing are unchanged. Worst-case mixed payload work stays capped at 64/slice; cheap descriptor work rises to 512 exact demanded coordinates/slice, one quarter of the historical 2048-block global sweep.
