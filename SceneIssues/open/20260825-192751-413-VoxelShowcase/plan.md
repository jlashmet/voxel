# Plan — SceneIssue 20260825-192751-413 VoxelShowcase

## Evidence / marked region
- One capture/circle marks top-left FPS telemetry (center `0.0281,0.0259`, radius `0.0369`); acceptance uses sustained production movement.
- `2af9088...` moving p95 was `91.445 ms`; worker/admission spikes tracked the frame while arena upload was ~`0.345 ms`; water separately reached ~`48.707 ms`.
- Apple M4 Max Metal runs `85c3b6a...`, `0b881e5...`, `6344d47...`, and exact `5338252...` all recorded `0` GPU completions while real streaming/rendering continued. The `5338252...` built player still converged to 516 visible / 0 missing, later ~160–380+ FPS, with intact final castle/terrain, proving CPU fallback masks the GPU admission failure.

## Competing hypotheses / conclusion
- Watchdog/Metal/harness failure rejected: Metal backend exists and built-player replay succeeds.
- Geometry upload/readback rejected as the zero-completion cause: no GPU build reaches completion; arena reports no lease failures and CPU publication continues.
- Water can spike but cannot explain zero GPU completions.
- Recovery scheduling/global gating alone rejected: bounded interleaving and local-ready admission still yielded zero.
- Version-domain mismatch supported: `GpuSurfaceMirrorCoordinator` compares against Storage/change-journal versions, but the caller supplied renderer-local `_build.SourceVersion`. Once those counters diverge, `PrepareFromBridge` rejects before dispatch. Mirror-not-ready was also converted immediately into CPU fallback instead of GPU backpressure.

## Fix
- Keep the world-scoped persistent mirror/directory, bounded journal/recovery, per-region history safety, and no mutation during active extraction.
- Capture authoritative `world.Storage.Version` at the immutable snapshot's GPU handoff; retain that generation for covered-region validation.
- If the requested mirror footprint is not ready, keep the eligible GPU stage pending and retry bounded admission on later worker slices while the old mesh remains visible; do not silently route supported work to CPU.
- GPU-eligible fallback remains a product failure. CPU is allowed only for genuinely unsupported geometry/rings or explicit emergency disable.

## Regression / acceptance
- EditMode: `GpuBrickSlotTableTests`, `GpuLod2CutoverPolicyTests`.
- PlayMode `ShowcaseGpuMigrationTests.MovingShowcaseCompletesGpuSurfaceBuildsAndPreservesCoverage`: 210 m streaming traversal, >=8 real GPU completions, **zero GPU-eligible fallbacks**, no holes/blocking completion, stationary p95 `<8 ms`, moving p95 `<18 ms`, p99 `<25 ms`.
- Implemented smooth/planar/rounded/sharp/cubic reconstruction must not silently use CPU when otherwise GPU-eligible.

## Blast radius / cost
- Change is confined to solid GPU admission/mirror bookkeeping; water, HLOD, visibility, Storage writes, collision, worldgen, content unchanged. Caps remain 128 changes, 2048 recovery blocks, 64 resident scan slots/frame.
- Pending admission retains one worker snapshot/pins longer but adds no new mirror allocation or per-frame scan; shared mirror remains >=96 MiB (or `16x` worker budget) + ~4% directory, replacing ~8 duplicated mirrors (~98 MiB aggregate).
- Final closure requires green exact-SHA targeted CI plus built-player captured-pose verification; no timing/correctness/adoption gate weakening.
