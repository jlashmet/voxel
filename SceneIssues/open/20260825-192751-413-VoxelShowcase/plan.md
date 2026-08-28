# Plan — SceneIssue 20260825-192751-413 VoxelShowcase

## Evidence / marked region
- The saved capture has one red circle over the top-left FPS/telemetry region (center `0.0281,0.0259`, radius `0.0369`) at the player-driven captured pose; report: `when moving around i get 3 fps`. Repro/acceptance therefore uses sustained production movement, not a stationary/editor-only proxy.
- Failed exact-SHA product CI on `2af9088aca0b04b9b8cbf051546daaa3576b734a` measured moving p95 `91.445 ms` vs the unchanged `<18 ms` gate. Worker/admission spikes correlated (`46.408 -> 46.409 ms`, `98.201 -> 98.203 ms`), while GPU arena geometry upload was only ~`0.345 ms`; water separately spiked ~`48.707 ms`.

## Competing hypotheses / conclusion
- Watchdog or harness-only failure: rejected; captured stalls align with real solid worker/admission timing.
- Geometry upload/readback: rejected as primary cause; geometry remains GPU-resident and measured arena upload is sub-ms.
- Water: contributing but insufficient; it does not explain the independent ~98 ms solid worker/admission stall.
- Supported cause: the old GPU-candidate path still performed a dense `_brickCacheEdge^3` CPU `TryPin` neighborhood walk per chunk, repeatedly publishing/looking up/pinning brick data before compute.

## Fix
- Replace per-chunk dense staging with one world-scoped persistent GPU brick mirror fed by bounded resident-region recovery plus the canonical storage change journal.
- Resolve mirrored bricks by world coordinate on GPU; apply compact directory deltas with `VoxelBrickDirectoryUpdater.compute`. Empty bricks are lookup misses rather than resident payloads.
- Fence generation changes while older extraction is active. Missing/not-ready/exhausted/stale/unsupported cases stay on the existing CPU fallback instead of producing holes.
- Reject stale empty/uniform no-payload deltas before slot release so an older edit cannot evict a newer mixed brick.

## Regression / acceptance
- EditMode: `GpuBrickSlotTableTests` covers stale mixed->empty release and slot-version behavior; `GpuLod2CutoverPolicyTests` covers production GPU admission policy.
- PlayMode: `ShowcaseGpuMigrationTests` must exercise real GPU builds while traversing the production scene, preserve coverage/no-hole assertions and zero blocking frame-path completions, with unchanged stationary p95 `<8 ms`, moving p95 `<18 ms`, moving p99 `<25 ms`.

## Blast radius / cost
- Scoped to step-1/step-2 GPU surface admission/mirroring plus targeted tests; water, HLOD and visibility architecture are unchanged.
- One shared mirror uses at least 96 MiB payload budget (or `16x` the requested worker budget) plus ~4% lookup-directory overhead, replacing roughly eight duplicated worker mirrors (~98 MiB aggregate). Recovery is bounded to 2048 blocks/frame, 64 resident scan slots/frame and 128 change records/frame.
- Do not weaken timing or correctness gates. Final closure requires green exact-SHA targeted CI and captured-pose production verification.