# Plan — SceneIssue 20260825-192751-413 VoxelShowcase

## Evidence / marked region
- The saved capture has one red circle over the top-left FPS/telemetry region (center `0.0281,0.0259`, radius `0.0369`) at the player-driven captured pose; report: `when moving around i get 3 fps`. Repro/acceptance therefore uses sustained production movement, not a stationary/editor-only proxy.
- Failed exact-SHA product CI on `2af9088aca0b04b9b8cbf051546daaa3576b734a` measured moving p95 `91.445 ms` vs the unchanged `<18 ms` gate. Worker/admission spikes correlated (`46.408 -> 46.409 ms`, `98.201 -> 98.203 ms`), while GPU arena geometry upload was only ~`0.345 ms`; water separately spiked ~`48.707 ms`.
- Targeted CI on `85c3b6a3c0d2f1ecc7a977efde0c33c7af82caa0` and `0b881e5f3228164dd4c4fd4f29d93f21debc374a` used Apple M4 Max Metal with production GPU backends, yet the 210 m traversal recorded `0` GPU completions while streaming grew roughly `199 -> 236` regions; built-player replay passed. Diagnostic `6344d47a77b1e002b01795d456b8448b41f349d8` added per-region generation validation but retained global recovery admission and also recorded `0` GPU completions, isolating recovery admission as the remaining blocker.

## Competing hypotheses / conclusion
- Watchdog/Metal/harness-only failure: rejected; failures ran on Metal with resident GPU backends and successful built-player replay.
- Geometry upload/readback: rejected as the remaining admission blocker; geometry remains GPU-resident and measured arena upload is sub-ms after batched mirror work.
- Water: historically contributing but insufficient; it cannot explain zero solid GPU completions.
- Recovery scheduling alone: rejected as sufficient. Advancing the bounded 128-record journal slice and 2048-block recovery slice in the same frame did not change the zero-completion result.
- Global admission barriers: supported by code plus runtime streaming. Per-region generation validation alone still failed because GPU admission required `RecoveryComplete` for every resident region. Unrelated streamed-region recovery can therefore suppress locally unchanged, fully mirrored chunks indefinitely.

## Fix
- Keep the world-scoped persistent GPU brick mirror and compact directory, bounded journal/recovery progress, and stale no-payload protection.
- Synchronize the mirror to current authoritative generation, but validate chunk staleness per covered region: reject snapshots before the known journal-history floor or when any covered region changed afterward.
- Treat resident recovery as bounded background work, not a global gate. Admit only when the requested footprint is in `s_ReadyRegions`; unrelated recovery remains CPU fallback without blocking locally ready GPU chunks. Preserve no-mutation-while-active.

## Regression / acceptance
- EditMode: `GpuBrickSlotTableTests` covers stale mixed->empty release and slot-version behavior; `GpuLod2CutoverPolicyTests` covers production GPU admission policy.
- PlayMode behavioral gate: `ShowcaseGpuMigrationTests.MovingShowcaseCompletesGpuSurfaceBuildsAndPreservesCoverage` traverses 210 m while streaming, requires >=8 GPU completions and >=5% GPU share, preserves visible coverage/no-hole and zero blocking frame-path completions, and keeps stationary p95 `<8 ms`, moving p95 `<18 ms`, moving p99 `<25 ms`.

## Blast radius / cost
- Runtime change stays inside shared solid-GPU admission/mirror bookkeeping; water, HLOD, visibility, Storage writes, collision, worldgen and scene content are unchanged. Per-frame caps remain 128 change records, 2048 recovered blocks and 64 resident scan slots.
- Per-region safety adds one `int3 -> ulong` entry only for regions with observed solid-affecting changes since the history floor; retention overrun/no-journal rebuild clears it and rejects older snapshots.
- One shared mirror uses at least 96 MiB payload budget (or `16x` requested worker budget) plus ~4% directory overhead, replacing roughly eight duplicated worker mirrors (~98 MiB aggregate).
- Do not weaken timing/correctness gates. Final closure requires green exact-SHA targeted CI + captured-pose production verification.
