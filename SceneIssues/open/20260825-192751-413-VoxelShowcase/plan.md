# Plan — SceneIssue 20260825-192751-413 VoxelShowcase

## Evidence / marked region
- The saved capture has one red circle over the top-left FPS/telemetry region (center `0.0281,0.0259`, radius `0.0369`) at the player-driven captured pose; report: `when moving around i get 3 fps`. Repro/acceptance therefore uses sustained production movement, not a stationary/editor-only proxy.
- Failed exact-SHA product CI on `2af9088aca0b04b9b8cbf051546daaa3576b734a` measured moving p95 `91.445 ms` vs the unchanged `<18 ms` gate. Worker/admission spikes correlated (`46.408 -> 46.409 ms`, `98.201 -> 98.203 ms`), while GPU arena geometry upload was only ~`0.345 ms`; water separately spiked ~`48.707 ms`.
- Targeted CI for feature SHA `85c3b6a3c0d2f1ecc7a977efde0c33c7af82caa0` built/replayed the real player at roughly 129–518 FPS after startup, but the behavioral traversal failed because GPU completions stayed `0`. Its log shows residency growing `199 -> 231` regions while generation remained pending.

## Competing hypotheses / conclusion
- Watchdog/Metal/harness-only failure: rejected; CI used the Apple M4 Max Metal device and the production GPU backend was allocated.
- Geometry upload/readback: rejected as primary cause; geometry remains GPU-resident and measured arena upload is sub-ms.
- Water: contributing but insufficient; it does not explain the independent solid worker/admission stall.
- Shared-mirror recovery starvation: supported. `PrepareFromBridge` drained at most 128 journal records and returned before its 2048-block recovery slice whenever more changes remained. Sustained streaming could therefore keep `RecoveryComplete` false forever, making every near-ring `Covers(...)` reject GPU extraction despite allocated GPU backends.

## Fix
- Keep the world-scoped persistent GPU brick mirror and compact directory introduced for this issue.
- Advance bounded journal replay and bounded resident recovery in the same frame while no GPU extraction is active; GPU admission still waits until both queues are exact/current, preserving generation safety and CPU fallback coverage.
- Reject stale empty/uniform no-payload deltas before slot release so an older edit cannot evict a newer mixed brick.

## Regression / acceptance
- EditMode: `GpuBrickSlotTableTests` covers stale mixed->empty release and slot-version behavior; `GpuLod2CutoverPolicyTests` covers production GPU admission policy.
- PlayMode behavioral gate: `ShowcaseGpuMigrationTests.MovingShowcaseCompletesGpuSurfaceBuildsAndPreservesCoverage` traverses 210 m through the production scene while streaming, requires sustained GPU completions/share, preserves visible coverage/no-hole and zero blocking frame-path completions, and keeps stationary p95 `<8 ms`, moving p95 `<18 ms`, moving p99 `<25 ms`.

## Blast radius / cost
- Runtime change is confined to ordering two already-bounded shared-mirror maintenance slices; water, HLOD, visibility and scene content are unchanged. Worst-case per-frame caps remain 128 change records, 2048 recovered blocks and 64 resident scan slots.
- One shared mirror uses at least 96 MiB payload budget (or `16x` the requested worker budget) plus ~4% lookup-directory overhead, replacing roughly eight duplicated worker mirrors (~98 MiB aggregate).
- Do not weaken timing or correctness gates. Final closure requires green exact-SHA targeted CI and captured-pose production verification.
