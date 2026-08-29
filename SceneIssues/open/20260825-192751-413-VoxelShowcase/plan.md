# Plan — SceneIssue 20260825-192751-413 VoxelShowcase

## Evidence / marked region
- One capture/circle marks the top-left FPS telemetry; replay pose is `Showcase Camera` at `(77.953941,24.55005,-3.345814)`, FOV `70`.
- Earlier built-player evidence isolated a ~`0.65–0.77 s/frame` solid-admission stall while arena upload/water were negligible. That supported removing global resident-world mirror recovery.
- Exact request `f7258c53…` (parent `f6754abf…`), run `33231204833`, then exposed a different product failure after the global stall was removed: PlayMode failed at traversal frame 134 with `gpuCompleted=0`, `gpuFallback=0`, `gpuWaitSlices=2118`, and zero visible voxel draws. The exact built player remained at ~`5 visible / 768 missing` chunks through the 45 s replay while solid admission was only ~`0.13–0.33 ms`.

## Competing hypotheses / discriminator
- Global recovery CPU stall: **fixed/rejected for the current failure** because admission is now sub-millisecond.
- GPU upload/readback/watchdog: rejected; no GPU count ever dispatches, while the player itself runs ~300 FPS once startup settles.
- Stale snapshot/history remains a guarded edge but does not explain every worker waiting from startup.
- **Supported:** recovery was demand-driven only at *region* granularity. A Storage region is `512³` voxels = `64³` = `262,144` logical 8³ blocks. At 64 blocks/frame, one demanded region requires 4,096 frames before being marked ready (~13.7 s at 300 FPS), starving actual chunk footprints.

## Fix
- Preserve the shared persistent mirror, journal/version safety, active-extraction mutation guard, and 64-block/frame foreground budget.
- Make readiness/recovery block-granular: `Covers` queues exact blocks in each GPU brick-cache footprint; empty/uniform blocks use zero-copy `RegionReadView`; only mixed blocks pin/copy payloads.
- Region changes conservatively invalidate snapshot generations but republish/requeue only previously demanded blocks. Region unload/replacement no longer scans/removes all 262k coordinates.

## Regression / acceptance
- Behavioral regression `ShowcaseGpuMigrationTests.MovingShowcaseCompletesGpuSurfaceBuildsAndPreservesCoverage`: exact `VoxelShowcase`, 210 m traversal, >=8 GPU completions, zero eligible fallbacks, no holes/blocking completion, moving p95 `<18 ms`, p99 `<25 ms`, stationary p95 `<8 ms`.
- Exact built-player replay must restore near geometry at the captured pose and eliminate persistent missing-chunk holes while retaining high-FPS headroom.

## Blast radius / cost
- Solid GPU mirror/admission only; water, HLOD, visibility, Storage writes, collision, worldgen/content unchanged.
- Shared mirror allocation remains >=96 MiB. New persistent CPU state is only demanded block queue/hash bookkeeping; no per-frame allocations are introduced. Recovery remains capped at 64 blocks/frame, but every processed block now belongs to waiting GPU work.
- Closure requires green exact-SHA targeted CI plus green exact built-player evidence; no gate weakening.
