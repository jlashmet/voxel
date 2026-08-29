# Experiment 004 — Recovery admission fairness

## Question
Can exact demanded GPU-mirror blocks be permanently starved because already-covered workers reacquire the shared extraction lease before the bounded recovery queue drains?

## Runtime discriminator
The prior exact-SHA run reached only three completed GPU solid builds, then remained at `26 visible / 744 missing` with `jobs=12` for roughly twenty seconds while the real player continued near `195–218 FPS`. Admission cost stayed around a few milliseconds and arena lease failures were zero. This rules out general frame saturation and makes a scheduler/liveness discriminator appropriate.

A focused behavioral regression was committed before this production edit: `GpuSurfaceMirrorRecoveryLivenessTests.DemandRecoveryCannotBeStarvedByCoveredGpuWork`. It traverses 96 m in the exact `VoxelShowcase`, observes `RecoveryComplete`, `ActiveExtractions`, `ReadyBlockCount`, and GPU completion counters, and rejects a 180-frame period where queued recovery overlaps active extraction without ready-block or completion progress.

## Mechanism found
`PrepareFromBridge` previously called the 64-block `ProcessRecovery()` slice and then returned success whenever the mirror's version matched Storage, even if `s_QueuedRecoveryBlocks` was still non-empty. Separately, `Covers` can queue a new exact footprint after a successful `PrepareFromBridge` result has already been cached for that frame. Consequently:
1. a worker processes only part of a recovery backlog;
2. it or another already-covered worker can immediately begin extraction;
3. later preparation cannot mutate the shared mirror while any extraction is active;
4. enough covered workers can keep the active count nonzero and continuously leapfrog queued demand.

## Change
- Same-frame cached prepare success now also requires `RecoveryComplete`.
- After each bounded recovery slice, prepare success requires both current mirrored generation and an empty recovery backlog.
- Queueing a newly demanded block invalidates the cached same-frame prepare success.

The mirror still never mutates beneath an active extraction. Recovery remains exact-block and capped at 64 blocks/slice; journal replay remains capped at 128 records/slice. No eligible CPU fallback is introduced.

## Cost / blast radius
This changes only admission ordering for the solid GPU mirror. Covered GPU work can deliberately yield while demanded recovery drains, trading some bounded compute occupancy for guaranteed forward progress. There are no new allocations, scans, buffers, shader changes, Storage writes, worldgen/content changes, water changes, or collision changes. The existing 210 m traversal p95/p99 and stationary p95 gates are retained to detect an unacceptable throughput cost.

## Result — insufficient as the sole fix
Exact request `32deb42f84b421f0c7a88adb8530eb3f3c49dfba` / run `33240075886` exercised the fairness change. It improved the production traversal from the earlier three-completion plateau to eight completed GPU solid builds, so starvation was a real contributing defect. It did **not** restore coverage: at traversal frame 146 the exact test lost every visible voxel draw with `known=5966`, `resident=47`, `dirty=2068`, `missing=718`, `jobs=12`, `gpuBackends=12`, `gpuCompleted=8`, `gpuFallback=0`, and `gpuWaitSlices=1898`.

The exact built-player replay independently plateaued at `23 visible / 747 missing` from roughly 20 s through the 45 s capture while frame rate remained about `205–244 FPS`, solid admission remained around `1.6–1.9 ms`, and the shared geometry arena reported `leaseFail=0`. All four timed replay captures and `verification-final.png` show the same unresolved near/mid-field holes and disconnected geometry after the plateau begins.

The focused liveness regression did not provide a valid product verdict in this run because its direct `Camera.Render()` hit a render-pass validation mismatch (`640x480` versus `64x64`). That harness defect is isolated by giving the test its own render target in the next experiment.

## Conclusion
**Partially confirmed but insufficient.** Recovery admission fairness is required and remains in place because it measurably increases forward progress, but the exact scene proves another admission-liveness condition remains. The next discriminator is the mismatch between CPU exact-snapshot optional-halo semantics and GPU mirror coverage requirements; see experiment 005. The issue remains open.
