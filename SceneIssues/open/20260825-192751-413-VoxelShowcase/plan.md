# Plan — SceneIssue 20260825-192751-413 VoxelShowcase

## Evidence / marked region
- One capture/circle marks the top-left FPS telemetry; replay pose is `Showcase Camera` at `(77.953941,24.55005,-3.345814)`, FOV `70`.
- Earlier Apple M4 Max Metal runs proved the GPU backend exists but completed `0` eligible builds; CPU fallback could still hide that defect.
- Exact feature SHA `db1230b...`, targeted run `33226493129`: requested PlayMode test was skipped because a user Unity editor held the project (infrastructure), but the always-run built-player capture reproduced a product failure. After ~20 s it fell to ~`1.3–1.5 FPS`, stayed at `visible=4 / missing=757`, step-1/2 resident chunks stayed `0`, and solid admission consumed ~`0.65–0.77 s/frame` while arena upload and water admission were negligible.

## Competing hypotheses / discriminator
- Metal/watchdog/harness failure rejected: real OSX player launches on Metal and the stall is inside solid worker admission.
- GPU upload/readback rejected as the primary stall: no count dispatch completes and arena upload is ~zero while the 700 ms plateau occurs.
- Stale snapshot generation remains a liveness edge guarded by covered-region history, but it cannot explain the measured CPU time before dispatch.
- **Supported:** `GpuSurfaceMirrorCoordinator` globally scanned resident regions and called `ProcessRecovery()` for up to 2048 logical blocks from the admission path. The runtime plateau tracks that work while the requested near-ring footprint waits behind unrelated world recovery.

## Fix
- Preserve the shared persistent mirror, journal/version safety, and no mutation during active extraction.
- Make recovery demand-driven: `Covers` queues every missing resident region in the actual GPU chunk footprint; no resident-world table sweep runs at attach/admission.
- Bound foreground recovery to 64 logical blocks/frame. Classify empty/uniform blocks through the borrowed zero-copy `RegionReadView`; only mixed blocks pay Storage pin/COW payload publication.
- A change during recovery restarts that region; unrelated never-demanded regions remain lazy. GPU-eligible work stays pending instead of silently falling back to CPU.

## Regression / acceptance
- Existing behavioral regression `ShowcaseGpuMigrationTests.MovingShowcaseCompletesGpuSurfaceBuildsAndPreservesCoverage`: exact `VoxelShowcase`, 210 m traversal, >=8 GPU completions, **zero eligible fallbacks**, no holes/blocking completion, moving p95 `<18 ms`, p99 `<25 ms`, stationary p95 `<8 ms`.
- Built-player replay must restore full near geometry and show sustained high-FPS headroom at the captured scene/pose.

## Blast radius / cost
- Solid GPU mirror/admission only; water, HLOD, visibility, Storage writes, collision, worldgen/content unchanged.
- Recovery work drops from global 2048-block + resident scan to demand-only 64 blocks/frame. Shared mirror allocation remains >=96 MiB (or `16x` worker budget) plus compact directory; no new persistent allocation.
- Closure requires green exact-SHA targeted CI and built-player captured-pose evidence; no gate weakening.
