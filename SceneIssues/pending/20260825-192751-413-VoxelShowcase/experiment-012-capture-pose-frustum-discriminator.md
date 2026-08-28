# Experiment 012 — capture-pose frustum discriminator

## Hypothesis / question
Experiment 011 failed before movement with in-band surface candidates but zero frustum candidates. Either the exact saved camera projection/frustum is invalid in the diagnostic render path, or the scheduler's known chunks are not spatially aligned with the captured view.

## Action / source
On feature source `b198802c44aafffcececfe28af2781c78b0a5041`, run `ShowcaseCapturePoseFrustumDiagnosticsTests.CapturePoseFrustumContainsForwardProbeAndSurfaceCandidates`. The test pins the exact saved camera pose, asks Unity's calculated frustum whether a 2 m AABB 16 m straight ahead is visible, and independently waits for production `VisibilityFrustumCandidates`. Production rendering/scheduling code is unchanged.

## Result
Exact request `fc22acedbecfc2a19ad3d44b972903126d726fbe`, run `33023402342`, reached runtime and failed as the discriminator intended. Unity accepted the forward probe, while production stayed at `known=170`, `inBand=38`, `frustum=0` after 1200 rendered frames. Ring telemetry showed no step-1 resident chunks and only step-8 residency (`step1 res=0 known=0`, `step2 res=0 known=20`, `step4 res=0 known=112`, `step8 res=15 known=38`).

## Verdict
Camera/frustum math is healthy. The zero-draw state is upstream: surface discovery/ring ownership has not admitted chunks aligned with the captured view. This falsifies the malformed-frustum hypothesis.

## Next
Run the existing production traversal regression before changing scheduler code. If ordinary movement reproduces zero visible coverage, fix discovery admission priority rather than camera or LOD geometry.
