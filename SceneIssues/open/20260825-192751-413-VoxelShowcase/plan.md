# Plan — SceneIssue 20260825-192751-413 VoxelShowcase

## Evidence / marked region
- One source capture/circle marks the extreme top-left FPS telemetry; replay pose is `Showcase Camera` at `(77.953941,24.55005,-3.345814)`, FOV `70`. Repository metadata and annotation geometry have been inspected. Exact built-player captures remain the runtime visual authority.
- Initial built-player evidence isolated a ~`0.65–0.77 s/frame` solid-admission stall while arena upload/water were negligible. Removing global resident-world mirror recovery eliminated that cost.
- Region-demand recovery then failed because one 512³ Storage region contains 262,144 logical 8³ blocks; at 64 blocks/frame a demanded region needed 4,096 frames before readiness. Exact-block recovery eliminated that whole-region wait.
- Exact-SHA run `33240075886` showed recovery-admission fairness was a real contributor: GPU completions rose from three to eight, but traversal still lost all visible draws and the built player froze at `23 drawn / 747 missing` through 45 s.
- Optional-halo request `33241309873` is a product failure, not infrastructure. Both requested tests executed. The focused test spent almost all of its 30.45 s in scene/world startup (`Showcase castle complete` at 27.4 s) and then failed its frame-count warmup with zero visible chunks; that warmup is not a valid liveness discriminator for this scene and must become wall-clock/startup-aware without weakening post-startup assertions.
- The same request's 210 m production traversal failed at frame 129 / camera `(70.91,24.55,-15.24)` with `gpuCompleted=8`, `gpuFallback=1`, `gpuWaitSlices=2055`, `jobs=12`, `visible=0`, and `missing=724`. This is still a product liveness failure.
- The exact built-player replay changed materially versus the prior frozen plateau: t15.7 is sparse, t25.7 has substantial castle recovery, and t35.7 has still more near/mid geometry. Telemetry near the end reports roughly `348 drawn / 351 missing`, so optional-halo handling restored convergence but did not make convergence fast enough for traversal or fully settle by 45 s.

## Competing hypotheses / discriminator
- Original global recovery CPU stall: **fixed**; admission is now milliseconds, not ~700 ms.
- Whole-region readiness: **fixed**; exact-block recovery produces early GPU completions without scanning a full 512³ Storage region.
- Pending stage retains an obsolete Storage generation: **rejected by exact-SHA runtime evidence**; refreshing the live mirror generation did not change the plateau.
- Storage/change-journal version-domain mismatch: **rejected by source evidence**; `RegionReadSource.Version` is the change-source version.
- Geometry arena exhaustion: **rejected by exact-player telemetry**; `leaseFail=0` with substantial unused capacity.
- Frame saturation: **rejected by exact-player telemetry**; the damaged scene still runs at high FPS for most of the replay.
- Optional nonresident halo mismatch: **confirmed contributor but insufficient sole cause**. Aligning GPU halo semantics with the CPU exact snapshot changed the built player from a persistent freeze to progressive recovery, yet the moving traversal still loses coverage.
- **Supported current hypothesis: recovery demand is serialized before it can be coalesced.** `BeginPersistentStage` short-circuits `PrepareFromBridge(...) || Covers(...)`: after the first pending worker queues a missing footprint, `PrepareFromBridge` closes admission while that queue drains, so the other pending workers do not call `Covers` and cannot register their missing blocks. When the first queue finally drains, that worker begins an extraction; the next worker then discovers its own missing blocks, but mirror mutation is forbidden while the extraction is active. With 12 workers pending this repeats recovery → one extraction → recovery instead of recovering the union of already-waiting footprints and then launching covered GPU work together.
- Discriminator: make demand discovery independent of admission readiness. Every pending stage may call read-only/queue-only `Covers` after world attachment even when `PrepareFromBridge` reports that journal/recovery admission is not ready. Admission still requires **both** prepare and coverage. If this is causal, the 12 waiting workers will register overlapping demand before the next drain point, recovery will process the union once, GPU completions will continue beyond the prior eight-completion ceiling, and visible coverage will survive the 210 m traversal. If the plateau remains, reject this hypothesis and instrument the single observed GPU fallback/count-write failure separately.

## Fix under test
- Preserve the shared persistent mirror, exact-block demand queue, journal replay, 64-block recovery slice, and the rule that mirror mutation never occurs while any extraction is active.
- Keep optional nonresident halo semantics: core-intersecting nonresident blocks still reject admission; halo-only nonresident blocks remain canonical empty-by-absence.
- In pending-stage admission, evaluate `PrepareFromBridge` and `Covers` separately rather than short-circuiting. `Covers` may only validate history/readiness and enqueue exact missing resident blocks; it must not mutate published GPU mirror storage.
- Admit/dispatch count only when both preparation and exact coverage succeed.
- Do not add CPU fallback, blocking GPU waits, new per-frame allocations, larger buffers, shader changes, wider world scans, or larger recovery/journal budgets.
- Make the focused test's initial-coverage wait wall-clock/startup-aware so it tests post-startup mirror liveness rather than the variable number of frames consumed by exact showcase generation. Preserve all existing recovery/completion/stall assertions.

## Regression / acceptance
- Focused behavioral regression `GpuSurfaceMirrorRecoveryLivenessTests.DemandRecoveryCannotBeStarvedByCoveredGpuWork`: exact `VoxelShowcase`, isolated 320x180 render target, 96 m traversal, startup-aware initial coverage, observes the shared mirror directly, requires demand recovery, sustained additional GPU completions, no >180-frame stalled recovery/active-extraction overlap, and `OptionalNonResidentHaloBlocksAccepted > 0`.
- End-to-end regression `ShowcaseGpuMigrationTests.MovingShowcaseCompletesGpuSurfaceBuildsAndPreservesCoverage`: exact `VoxelShowcase`, 210 m traversal, >=8 GPU completions, zero eligible fallbacks, no blocking completion, moving p95 `<18 ms`, p99 `<25 ms`, stationary settle with no missing chunks, stationary p95 `<8 ms`.
- Exact built-player replay must restore near/mid geometry at the issue scene, eliminate the persistent missing-chunk plateau, and retain substantial high-FPS headroom.

## Blast radius / cost
- Scope remains solid step-1/step-2 GPU mirror admission plus the focused test harness. Water, HLOD algorithms, visibility policy, Storage writes, collision, world generation/content, shader layout, and geometry arena allocation are unchanged.
- `Covers` is read-only with respect to the GPU mirror; on missing resident blocks it only deduplicates coordinates into the existing recovery queue. Calling it while prepare/admission is blocked therefore discovers demand earlier without mutating memory an active extraction can read.
- Demand remains exact-block and deduplicated (`HashSet<int3>`), recovery remains capped at 64 blocks per preparation slice, and journal replay remains capped at 128 records per slice. Coalescing may increase the queued union at a drain point, but it removes repeated serialized drain/extraction cycles and does not expand work beyond blocks already demanded by pending GPU chunks.
- Closure still requires green exact-SHA targeted CI plus green exact built-player visual/runtime evidence; no gate is weakened.
