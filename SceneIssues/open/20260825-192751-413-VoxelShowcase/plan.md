# Plan — SceneIssue 20260825-192751-413 VoxelShowcase

## Evidence / marked region
- One capture/circle marks the top-left FPS telemetry; replay pose is `Showcase Camera` at `(77.953941,24.55005,-3.345814)`, FOV `70`.
- Initial built-player evidence isolated a ~`0.65–0.77 s/frame` solid-admission stall while arena upload/water were negligible. Removing global resident-world mirror recovery eliminated that cost.
- Region-demand recovery then failed because one 512³ Storage region contains 262,144 logical 8³ blocks; at 64 blocks/frame a demanded region needed 4,096 frames before readiness.
- Exact-block recovery (`c3d06ab0…`, run `33232803150`) materially improved runtime: built-player ~194–200 FPS and solid admission ~2–4 ms. But editor traversal failed at frame 119 with `gpuCompleted=3`, `gpuFallback=0`, `gpuWaitSlices=1611`; the exact player plateaued at `27 drawn / 743 missing` from ~t28–t44. All three captures plus final verification visibly retain missing near/mid voxel surfaces.

## Competing hypotheses / discriminator
- Original global recovery CPU stall: **fixed**; admission is now a few milliseconds, not ~700 ms.
- Whole-region readiness: **fixed**; exact-block recovery produces early GPU completions and 27 visible draws.
- GPU readback/watchdog as primary cause: rejected by stable ~200 FPS player and permanent admission/coverage plateau rather than an expanding GPU stall.
- Raw 64-block/frame throughput alone: weak; finite exact footprints should keep converging, but telemetry and imagery become exactly flat for ~16 s.
- **Supported current hypothesis:** a phase-9 GPU stage retains the Storage generation captured at handoff. Any covered-region edit makes `Covers(oldGeneration)` permanently false while the worker continues polling. `dirty=2050` in the failed traversal supplies the required invalidation pressure.

## Fix
- Keep exact-block demand recovery, the 64-block/frame budget, shared persistent mirror, and region history safety.
- While admission is pending, refresh only the mirror's live `Storage.Version` gate before `PrepareFromBridge`/`Covers`.
- Preserve the cache's immutable renderer `_build.SourceVersion`; existing publication checks still reject any build superseded by relevant renderer invalidation. No eligible CPU fallback is introduced.

## Regression / acceptance
- Behavioral regression `ShowcaseGpuMigrationTests.MovingShowcaseCompletesGpuSurfaceBuildsAndPreservesCoverage`: exact `VoxelShowcase`, 210 m traversal, >=8 GPU completions, zero eligible fallbacks, no holes/blocking completion, moving p95 `<18 ms`, p99 `<25 ms`, stationary p95 `<8 ms`.
- Exact built-player replay must restore near geometry at the captured pose and eliminate persistent missing-chunk holes while retaining high-FPS headroom.

## Blast radius / cost
- Solid GPU mirror admission only; water, HLOD, visibility, Storage writes, collision, worldgen/content unchanged.
- Shared mirror remains >=96 MiB and recovery remains capped at 64 demanded blocks/frame. The new work is one live-world/Storage-version read per pending stage retry; no per-frame allocation.
- Closure requires green exact-SHA targeted CI plus green exact built-player evidence; no gate weakening.
