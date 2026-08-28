# Plan — SceneIssue 20260825-192751-413 VoxelShowcase

## Defect / acceptance
The sole marked region is the top-left FPS/surface telemetry at the saved Showcase camera. Acceptance is the production 420-frame/~210 m traversal: solids visible every moving frame, <=5 cm far fallback while near coverage is incomplete, zero frame-path blocking completions, >=4 streamed regions, p95 <18 ms and p99 <25 ms, plus the 45 s real-player replay with intact geometry.

## Evidence / discriminators
1. **CPU preparation is the throughput pressure, not the original coverage baseline.** Profiling measured scheduler/admission/worker prep near 9.16/6.39/5.21 ms versus ~0.16 ms upload. CPU source `1ddb80f...` retained visible solids for all 420 moving frames and failed only p95/p99.
2. **Legacy GPU-v1 safely accelerates production.** Falsified twice: runs `33125988697` and `33131454442` lost every visible solid at frame 8; GPU-v1 remains explicit-experiment only.
3. **Discovery priority or frustum-only completion closes the movement hole.** Falsified. Priority run `33140352114` lost all solids at moving frame 15; frustum-handoff run `33141268338` lost all solids at frame 5. Both replays later converged, so neither is the root cause.
4. **Cache residency / toroidal slot retirement causes the loss.** Falsified by source differential: `CpuTransvoxelChunkCache.cs` and `SurfaceChunkSlotGrid.cs` are byte-identical between coverage-safe `1ddb80f...` and the later failing CPU branch at the relevant baseline.
5. **Destructive cross-ring logical ownership causes the coverage regression.** Supported. Coverage-safe scheduler `37ea37a6...` directly accumulated each worker's physically drawable visible entries. Commit `7040882...` inserted `SurfaceLodVisibilitySelector` as a second pass that may discard those entries. Workers already enforce ring band + frustum and keep a stale ready mesh drawable while its replacement builds; therefore only the logical pass can erase a valid physical fallback after collection.

## Selected correction / regressions
Keep logical LOD ownership for overlap suppression, but make its production draw set non-destructive per subtree: a coarse drawable node retires only when its logical expansion contains an actual physical drawable replacement. If an expanded subtree contains proof-only nodes and no physical replacement, retain the coarsest drawable fallback. This restores the safety property of `1ddb80f...` without reverting modern discovery, meshing, arena, upload, or scheduler optimizations.

`SurfaceLodVisibilitySelectorTests.ProofOnlyReplacementKeepsCoarsePhysicalFallback` locks the handoff invariant. `ShowcaseGpuMigrationTests.MovingShowcaseKeepsLegacyGpuV1OffAndPreservesCoverage` remains the behavioral 420-frame production traversal and final targeted CI gate; thresholds and coverage assertions are unchanged.

## Blast radius / cost
Rendering visibility selection only. Storage/gameplay authority, worker band/frustum tests, cache residency, clipmap slots, meshing, geometry/material semantics, upload, shaders, arenas, GPU-v1 quarantine, concurrency, and acceptance thresholds are unchanged. The selector adds two reused hash sets and a bounded hierarchy walk over drawable ancestry; no per-frame managed allocation or world-residency scan is introduced. The tradeoff is temporary coarse fallback retention only when logical replacement has no drawable representative, preferring bounded overlap over a visible hole.
