# Plan — SceneIssue 20260825-192751-413 VoxelShowcase

## Defect / acceptance
The sole marked region is the top-left FPS/surface telemetry at the saved Showcase camera. Acceptance is the production 420-frame/~210 m traversal: solids visible every moving frame, <=5 cm far fallback while near coverage is incomplete, zero frame-path blocking completions, >=4 streamed regions, p95 <18 ms and p99 <25 ms, plus the 45 s real-player replay with intact geometry.

## Evidence and competing hypotheses
1. **CPU preparation is the long-term throughput bottleneck.** Profiling measured scheduler/admission/worker prep near 9.16/6.39/5.21 ms while upload was ~0.16 ms. Supported as performance pressure, but the older CPU source `1ddb80f...` traversed without a total draw loss.
2. **Legacy GPU-v1 safely accelerates production.** Falsified twice: runs `33125988697` and `33131454442` lost all visible solids at traversal frame 8; the latter replay also showed severe convergence stalls. GPU-v1 therefore stays explicit-experiment only.
3. **Frame-count startup warmup represented convergence.** Falsified by run `33132712687`: 1200 Editor frames expired before coverage, while its independent replay later converged to 517 visible chunks. Warmup is now a 30 s wall-clock gate; moving assertions are unchanged.
4. **Cross-ring handoff can retire fallback before finer geometry is published.** Supported by exact run `33136824454`: wall-clock warmup passed, then moving frame 5 dropped `VisibleSolidChunks` to zero while the same source's stationary replay later held 517 visible chunks with no drop events. `CpuTransvoxelChunkCache` matches the older coverage-safe CPU path; the intervening selector optimization counted in-band off-frustum, unready children as complete.

## Selected correction / regressions
Keep the optimized CPU renderer in production and GPU-v1 quarantined. For atomic LOD replacement, an in-band child counts as complete only when its desired generation is current-ready or current-known-empty; off-frustum alone is not publication proof. This preserves the earlier coarse/fine overlap fix while preventing camera motion from exposing an unpublished finer subtree.

`SurfaceLodVisibilitySelectorTests.CurrentViewCompletionRequiresRingOwnershipAndPublishedProof` covers the handoff rule. The production `ContinuousPlayerTraversalNeverStuttersOrOpensNearFarGap` test remains the end-to-end behavioral gate; `ShowcaseGpuMigrationTests` continues to police CPU production selection.

## Blast radius / cost
Change is limited to LOD visibility ownership; no storage, voxel semantics, collision/gameplay authority, mesh extraction, upload, shader, or arena behavior changes. It may retain coarse fallback geometry longer for prefetched off-screen children, trading brief extra coarse residency for continuous coverage. Selector complexity and allocations are unchanged.
