# Plan — SceneIssue 20260825-192751-413 VoxelShowcase

## Evidence / marked region
- One capture/circle marks the top-left FPS telemetry; replay pose is `Showcase Camera` at `(77.953941,24.55005,-3.345814)`, FOV `70`.
- Initial built-player evidence isolated a ~`0.65–0.77 s/frame` solid-admission stall while arena upload/water were negligible. Removing global resident-world mirror recovery eliminated that cost.
- Region-demand recovery then failed because one 512³ Storage region contains 262,144 logical 8³ blocks; at 64 blocks/frame a demanded region needed 4,096 frames before readiness.
- Exact-block recovery materially improved runtime to ~`194–218 FPS` with solid admission around `~2 ms`, but exact-SHA run `33234469456` still stopped after three GPU completions. The player plateaued at `26 visible / 744 missing` for ~20 seconds while `jobs=12`, proving a liveness failure rather than slow frame execution.
- The prior live-Storage-generation fix was exercised by that exact run and rejected: the plateau was unchanged.

## Competing hypotheses / discriminator
- Original global recovery CPU stall: **fixed**; admission is now milliseconds, not ~700 ms.
- Whole-region readiness: **fixed**; exact-block recovery produces early GPU completions without scanning the full 512³ Storage region.
- Pending stage retains an obsolete Storage generation: **rejected by exact-SHA runtime evidence**; refreshing the live mirror generation did not change the three-completion plateau.
- Storage/change-journal version-domain mismatch: **rejected by source evidence**; `RegionReadSource.Version` is exactly `_changes.CurrentVersion`.
- Successful GPU write leaks the shared extraction lease: **rejected by source evidence**; phase 10 releases `_gpuExtraction` as soon as write polling leaves `Pending`.
- **Supported current hypothesis: recovery admission starvation.** `PrepareFromBridge` processed only one bounded 64-block recovery slice but could still return admission-ready with a non-empty backlog. `Covers` could then discover more demand after that successful result was cached for the frame. Already-covered workers could reacquire the shared extraction lease while queued demand remained, and mirror mutation is prohibited whenever any extraction is active. With multiple workers, covered work can therefore leapfrog recovery indefinitely.

## Fix
- Preserve the shared persistent mirror, exact-block demand queue, journal replay, 64-block recovery slice, and the rule that mirror mutation never occurs while an extraction is active.
- Report `PrepareFromBridge` ready only when both the mirrored world version is current **and** `RecoveryComplete` is true.
- When `Covers` queues newly demanded blocks, invalidate the cached same-frame successful prepare result so later workers cannot bypass the new backlog.
- Do not add CPU fallback, blocking GPU waits, new allocations, larger buffers, or wider recovery scans.

## Regression / acceptance
- Focused behavioral regression `GpuSurfaceMirrorRecoveryLivenessTests.DemandRecoveryCannotBeStarvedByCoveredGpuWork`: exact `VoxelShowcase`, 96 m traversal, observes the shared mirror directly, requires demand recovery to be exercised and sustained GPU completion, and fails if a recovery backlog plus active extraction makes no ready-block/completion progress for 180 rendered frames.
- End-to-end regression `ShowcaseGpuMigrationTests.MovingShowcaseCompletesGpuSurfaceBuildsAndPreservesCoverage`: exact `VoxelShowcase`, 210 m traversal, >=8 GPU completions, zero eligible fallbacks, no blocking completion, moving p95 `<18 ms`, p99 `<25 ms`, stationary settle with no missing chunks, stationary p95 `<8 ms`.
- Exact built-player replay must restore near/mid geometry at the captured pose, eliminate the persistent missing-chunk plateau, and retain substantial high-FPS headroom.

## Blast radius / cost
- Scope is only solid GPU mirror admission fairness. Water, HLOD algorithms, visibility policy, Storage writes, collision, world generation/content, shader layout, and geometry arena allocation are unchanged.
- Recovery remains capped at 64 demanded blocks per preparation slice and journal replay at 128 records per slice; no per-frame allocation is introduced.
- Expected tradeoff: while recovery is pending, already-covered compute work may intentionally idle for the bounded number of slices needed to drain demanded blocks. This converts an unbounded starvation failure into bounded admission latency; exact traversal/frame percentiles remain the cost gate.
- Closure requires green exact-SHA targeted CI plus green exact built-player visual/runtime evidence; no gate weakening.
