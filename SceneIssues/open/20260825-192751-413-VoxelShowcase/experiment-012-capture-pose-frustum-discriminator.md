# Experiment 012 — capture-pose frustum discriminator

## Hypothesis / question
Experiment 011 failed before movement with 36 in-band surface candidates but zero frustum candidates. Either the exact saved camera projection/frustum is invalid in the diagnostic render path, or the scheduler's in-band known chunks are not spatially aligned with the captured view.

## Action / source
Starting from feature source `02e3274ed805d40c40d5e946e602994729c4d314`, verify scene wiring and add one PlayMode discriminator at the exact saved pose. The scene proves `VoxelShowcase`, `Camera`, and `Showcase Camera` share one GameObject. The discriminator asks Unity's own calculated frustum whether a small AABB 16 m straight ahead is visible, while independently waiting for production `VisibilityFrustumCandidates` and reporting `DescribeRings()` on failure. Production rendering/scheduling code is unchanged.

## Result
Pending exact-SHA targeted CI.

## Verdict
If the forward probe is rejected, the diagnostic camera/projection setup is invalid and the movement result must not drive a production fix. If the probe is accepted while production remains at zero frustum candidates, camera math is healthy and the next investigation is scheduler discovery/ring spatial ownership at the saved pose.

## Next
Run only `ShowcaseCapturePoseFrustumDiagnosticsTests.CapturePoseFrustumContainsForwardProbeAndSurfaceCandidates` through `ci-test/fixes/agent-2`, inspect the exact failure telemetry, then update this experiment before any production change.
