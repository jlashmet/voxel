# Plan — SceneIssue 20260825-192751-413 VoxelShowcase

## Evidence / marked region
- The saved capture has one red circle over the top-left FPS/telemetry region (center `0.0281,0.0259`, radius `0.0369`) at the player-driven captured pose; report: `when moving around i get 3 fps`. Repro/acceptance therefore uses sustained production movement, not a stationary/editor-only proxy.
- Failed exact-SHA product CI on `2af9088aca0b04b9b8cbf051546daaa3576b734a` measured moving p95 `91.445 ms` vs the unchanged `<18 ms` gate. Worker/admission spikes correlated (`46.408 -> 46.409 ms`, `98.201 -> 98.203 ms`), while GPU arena geometry upload was only ~`0.345 ms`; water separately spiked ~`48.707 ms`.
- Targeted CI for `85c3b6a3c0d2f1ecc7a977efde0c33c7af82caa0` and then `0b881e5f3228164dd4c4fd4f29d93f21debc374a` both used Apple M4 Max Metal and allocated production GPU backends, yet the 210 m behavioral traversal recorded exactly `0` GPU completions. The second run still streamed roughly `199 -> 236` generated regions. Its built player passed replay; after startup, one-second windows were typically ~129–350 FPS with p95 ~3.8–11.5 ms.

## Competing hypotheses / conclusion
- Watchdog/Metal/harness-only failure: rejected; both failures ran on Metal with resident GPU backends, and the built player replayed successfully.
- Geometry upload/readback: rejected as the remaining admission blocker; geometry remains GPU-resident and measured arena upload is sub-ms after the batched mirror work.
- Water: contributing historically but insufficient; it cannot explain zero solid GPU completions.
- Recovery scheduling alone: rejected as sufficient. Advancing the bounded 128-record journal slice and 2048-block recovery slice in the same frame did not change the zero-completion result.
- Global-generation admission: supported by code plus runtime streaming. Version-safety added `world.Storage.Version == chunkSnapshotGeneration` before shared-mirror admission. Worldgen advances the global Storage version for unrelated regions between snapshot and stage, so every moving chunk can be refused even when all regions it samples are unchanged. Backend allocation therefore rises while completion remains zero, matching both CI runs.

## Fix
- Keep the world-scoped persistent GPU brick mirror and compact directory introduced for this issue, with bounded journal/recovery progress and stale no-payload protection.
- Synchronize the shared mirror to the current authoritative generation, but validate chunk staleness per covered region: reject if the snapshot predates the known journal-history floor or if any covered region had a solid-affecting change after that snapshot. Unrelated streamed-region changes no longer invalidate the chunk.
- Preserve the no-mutation-while-active rule, while allowing multiple extractions to share an already-current immutable mirror generation.

## Regression / acceptance
- EditMode: `GpuBrickSlotTableTests` covers stale mixed->empty release and slot-version behavior; `GpuLod2CutoverPolicyTests` covers production GPU admission policy.
- PlayMode behavioral gate: `ShowcaseGpuMigrationTests.MovingShowcaseCompletesGpuSurfaceBuildsAndPreservesCoverage` traverses 210 m through the production scene while streaming, requires >=8 GPU completions and >=5% GPU share, preserves visible coverage/no-hole and zero blocking frame-path completions, and keeps stationary p95 `<8 ms`, moving p95 `<18 ms`, moving p99 `<25 ms`.

## Blast radius / cost
- Runtime change stays inside shared solid-GPU admission/mirror bookkeeping; water, HLOD, visibility, Storage writes, collision, worldgen and scene content are unchanged. Worst-case per-frame maintenance caps remain 128 change records, 2048 recovered blocks and 64 resident scan slots.
- Per-region safety adds one `int3 -> ulong` dictionary entry only for regions with observed solid-affecting changes since the current history floor; retention overrun/no-journal rebuild clears it and conservatively rejects older snapshots.
- One shared mirror uses at least 96 MiB payload budget (or `16x` the requested worker budget) plus ~4% lookup-directory overhead, replacing roughly eight duplicated worker mirrors (~98 MiB aggregate).
- Do not weaken timing or correctness gates. Final closure requires green exact-SHA targeted CI and captured-pose production verification.
