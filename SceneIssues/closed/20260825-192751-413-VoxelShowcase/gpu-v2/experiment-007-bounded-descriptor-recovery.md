# Experiment 007 — bounded descriptor recovery

## Trigger evidence
Experiment 006 proved that the mirror can make progress when all pending workers register demand, but the 210 m production traversal then produced `gpuCompleted=0` before losing every visible voxel draw. The exact test started from a fully generated scene, so this is not a world-startup artifact. The exact built player also remained incomplete at 45 s (`~103 visible / 661 missing`) despite high frame-rate headroom.

Source inspection shows why a bounded single footprint is still slow. A step-2 chunk samples an 18³ brick-cache footprint: 5,832 logical 8³ blocks. `ProcessRecovery` previously charged every queued coordinate against the same 64-block slice even though `PublishRegionBlock` already distinguishes two very different costs:
- Empty/uniform: classify from an already borrowed `RegionReadView`; empty is canonical directory absence and uniform is compact metadata only.
- Mixed: pin authoritative Storage and copy the 512-voxel material/surface/boundary payload into the GPU mirror.

The mirror batches directory metadata through a 4096-entry delta buffer, so descriptor classification does not require one GPU driver call per coordinate.

## Competing hypotheses
1. **The 64-block slice is dominated by cheap descriptor-only coordinates.** Recovery latency is therefore proportional to the entire 18³ footprint instead of the sparse mixed payload that actually carries voxel data.
2. **Mixed payload publication itself dominates.** If true, separating descriptor and mixed budgets will not materially increase completions or preserve coverage; the next discriminator must move to count/write latency/fallback reasons rather than increasing payload work.

## Change
- Commit `4a51638a080bfdd6d226257b1dd4da5c235ea168` restores one-footprint-at-a-time demand discovery. The experiment-006 all-worker union is removed, while the recovery fairness gate remains: queued recovery must drain before covered work can reacquire extraction.
- Commit `70c00dc14aabe20fa056f246ce118fbd4e24b7ee` separates the recovery slice into two bounded budgets:
  - at most **512 descriptor classifications** per preparation slice;
  - at most **64 mixed payload publications** per preparation slice.
- The first mixed coordinate beyond the 64-payload ceiling is requeued and the slice stops, so the expensive path cannot exceed the previously exercised cap.
- Empty/uniform coordinates advance readiness without consuming mixed-payload quota. Directory updates remain batched by the mirror's existing delta uploader.
- Journal replay remains capped at 128 records per slice. Mirror mutation remains forbidden while any GPU extraction is active.

## Behavioral discriminator
The existing focused regression remains the direct liveness gate: `GpuSurfaceMirrorRecoveryLivenessTests.DemandRecoveryCannotBeStarvedByCoveredGpuWork` must still traverse 96 m, exercise recovery plus optional nonresident halo semantics, sustain additional GPU completions, and never hit its >180-frame no-progress overlap assertion.

The production discriminator remains unchanged: `ShowcaseGpuMigrationTests.MovingShowcaseCompletesGpuSurfaceBuildsAndPreservesCoverage` must cross 210 m at the shipped fly-speed cap, keep visible geometry throughout, complete at least eight GPU builds, produce zero eligible CPU fallback and zero blocking completion, meet moving p95 `<18 ms` / p99 `<25 ms`, settle to zero missing visible chunks, and meet stationary p95 `<8 ms`.

The exact built-player replay at the issue camera must independently show restored near/mid geometry without a persistent missing-chunk plateau.

## Expected result
If hypothesis 1 is causal, a bounded footprint should reach coverage in substantially fewer rendered frames without recreating the old CPU stall: GPU completions should occur early enough to survive traversal, the player should converge within the 45 s visual gate, and frame-time percentiles should remain under the existing thresholds.

If coverage remains red while admission/frame time stays cheap, reject hypothesis 1 and instrument the GPU count/write/fallback path next rather than increasing either bounded recovery limit.

## Blast radius / cost
Scope is only solid step-1/step-2 GPU mirror recovery scheduling. No water path, HLOD algorithm, visibility policy, Storage write behavior, collision, world generation/content, shader layout, geometry arena size, or CPU fallback policy changes.

Worst-case expensive work is unchanged at 64 mixed payload publications per slice. The new CPU-only descriptor ceiling is 512 exact demanded coordinates, one quarter of the historical 2048-block global sweep and, unlike that sweep, restricted to the waiting chunk footprint. Directory metadata is already coalesced into bounded GPU delta batches. No new per-frame collection allocation is introduced.

## Result
Pending exact-SHA targeted CI and exact built-player evidence.
