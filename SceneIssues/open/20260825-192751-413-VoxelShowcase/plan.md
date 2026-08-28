# Plan — SceneIssue 20260825-192751-413 VoxelShowcase

## Acceptance surface
The single marked region is the top-left FPS/surface telemetry at the saved VoxelShowcase pose. Acceptance uses the production path: during a 210 m traversal at the scene-serialized 18 m/s fly speed, step-1/step-2 GPU extraction must complete visible builds, cross >=4 regions, retain a visible voxel draw every moving frame, keep near/far fallback gaps <=5 cm, perform zero frame-path blocking completions, and remain below 18 ms p95 / 25 ms p99. After convergence, stationary p50/p95 and effective FPS quantify headroom.

## Evidence / hypotheses
- **Current GPU fallback can publish a hole — falsified.** Unsupported/decorated semantics release staged GPU state and continue through CPU extraction while the previous ready lease remains live until replacement publication.
- **Admission is generically too expensive — falsified by CPU control.** The same traversal with GPU cutover disabled runs near 300 FPS; shared residency/admission policy is not the 50–100 ms cost.
- **Frame-path job completion is blocking — falsified.** Solid and water paths check `IsCompleted` before `TryCompleteReady`; failed run `33209879080` reported zero blocking-completion violations.
- **GPU mirror publication performs synchronous driver work — supported.** Final-request job `98980211076` failed moving p95 at 91.445 ms. Captured worker max `46.408 ms` mapped to solid admission `46.409 ms`, and `98.201 ms` mapped to `98.203 ms`, while arena geometry upload was only ~0.345 ms. Source showed each newly admitted mixed brick issuing three immediate `ComputeBuffer.SetData` payload writes plus metadata inside that worker `Prepare`. A chunk can therefore issue hundreds of tiny Metal uploads before returning.
- **Water is the same cause — not supported.** One independent water-admission spike was 48.707 ms, but water has no GPU-mirror publication and already avoids waiting on fresh mesh jobs. Keep it separate and let the unchanged p99 gate discriminate recurrence.

## Selected fix / blast radius
Keep GPU cutover default-on only for source steps 1/2. `GpuVoxelBrickMirror.Publish` now copies changed payloads into bounded mirror-owned native staging and marks destination slots dirty; the first GPU buffer bind flushes adjacent dirty slots as bulk ranges. This preserves immediate payload ownership and slot generations while reducing the common fresh-admission case from four GPU writes per mixed brick to four per contiguous slot run. Staging is bounded by the existing `BrickCacheCount` mirror capacity; CPU fallback, step 4/8, Storage, collision, worldgen, profiles, and water are unchanged.

## Regression / final gate
`ShowcaseGpuMigrationTests.MovingShowcaseCompletesGpuSurfaceBuildsAndPreservesCoverage` remains the behavioral regression: >=8 GPU builds, >=5% GPU share, visible coverage every moving frame, zero blocking completions, moving p95 <18 ms / p99 <25 ms, and stationary p95 <8 ms. Final exact-SHA CI must also pass the built-app saved-pose replay; inspect both telemetry and screenshots before promotion.