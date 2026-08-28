# Plan — SceneIssue 20260825-192751-413 VoxelShowcase

## Acceptance surface
The single marked region is the top-left FPS/surface telemetry at the saved VoxelShowcase pose. Acceptance uses the production path: during a 210 m traversal at the scene-serialized 18 m/s fly speed, step-1/step-2 GPU extraction must complete visible builds, cross >=4 regions, retain a visible voxel draw every moving frame, keep near/far fallback gaps <=5 cm, perform zero frame-path blocking completions, and remain below 18 ms p95 / 25 ms p99. After convergence, stationary p50/p95 and effective FPS quantify headroom.

## Evidence / hypotheses
- **Historical GPU cutover lost geometry.** Source `2d4c0a090f37cefc6d60db1ccd1ca68c04190815` / run `33125988697` lost all visible solid draws at frame 8, but predates the raw-mirror semantic classifier and packed-semantic decoder.
- **Current unsupported GPU semantics can publish a hole — falsified.** The compute classifier returns zero counts; phase 9 releases the staged GPU state and schedules the CPU continuous-density chain. The previous ready lease remains live until a count/write-agreed GPU lease is atomically published.
- **CPU raw-voxel eligibility scanning was avoidable streaming work — supported.** Current step-1/step-2 classification runs from the GPU mirror; decorated/non-continuous semantics still fall back to CPU.
- **30 s traversal failure proves GPU performance failure — falsified by run `33208209953`, retry job `98975972027`.** The regression reached 209.5/210 m after 1238 frames, then its liveness watchdog fired before GPU-adoption or p95/p99 assertions. Movement is capped at 0.5 m per rendered frame, so slow frames cannot catch up to wall time. The same retry's 45 s real-player replay completed successfully and captured three screenshots.

## Selected fix / blast radius
Production GPU cutover remains default-on only for source steps 1/2, with `VOXEL_DISABLE_GPU_CUTOVER=1` as the emergency/A-B CPU fallback. Step 4, step 8 HLOD, Storage, collision, gameplay/worldgen, profile geometry, and unsupported/decorated semantics are unchanged. Extend only the test's traversal liveness watchdog from 30 s to 45 s; the 210 m distance, 0.5 m/frame cap, GPU completion/share, coverage, blocking-completion, moving p95/p99, and stationary p95 gates are unchanged. Showcase bake caching excludes tests/evidence, so this change should reuse the existing semantic-world bake.

## Final gate
`ShowcaseGpuMigrationTests.MovingShowcaseCompletesGpuSurfaceBuildsAndPreservesCoverage` must show a resident GPU backend, >=8 GPU-completed chunks, >=5% GPU share, all coverage/performance limits, and log moving/stationary telemetry. The same exact-SHA CI request must also complete the built-app saved-pose replay. Inspect both telemetry and replay before promotion.
