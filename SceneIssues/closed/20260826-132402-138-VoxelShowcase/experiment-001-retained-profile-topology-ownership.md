# Experiment 001 — retained-profile topology ownership

## Hypothesis
The doorway triangle is continuous Transvoxel topology that should be replaced by the retained arch profile, but the ownership test misses a triangle that spans the clear opening.

## What was performed
Source-traced the recorded VoxelShowcase doorway through `ArchFeatureDefinition` / `ArchBayFeatureDefinition`, the retained profile tests, and `CpuTransvoxelChunkCache` on source commit `b176ad960c908b596486a33846232a6e5ed385f3`. Compared the current ownership predicate with its introducing commit `708dc9eefb38a67b568c05b4d3efede05be34133` and the focused arch-profile regressions.

The original PNG is present and preserved as `screenshot-001.png`; the connected repository API exposes the binary only as blob metadata, so no pixel-derived claim is made in this experiment. The recorded fixture is a single 1928x836 VoxelShowcase frame at camera position `(180.9103, 35.2501, 23.1808)` with the circled region centered over the doorway.

## Result
`CpuTransvoxelChunkCache.RetainedProfileOwnsTriangle` classifies a continuous triangle using only its centroid against the annular-sector profile. `ArchProfileStitchTests.RetainedProfileOwnsOnlyCoveredContinuousTopology`, `ArchCapLayerDiagnosticTests`, and `ArchCrossingStabilityTests` cover matching topology, cap endpoints, depth, materials, and wedge bounds, but none cover a triangle whose centroid is in the clear aperture while an edge/vertex crosses the retained annulus.

That leaves exactly the reported failure mode possible: a coarse/large continuous triangle can bridge the doorway, have its centroid inside the intrados, escape suppression, and survive in front of the retained arch geometry.

## What was learned
Hypothesis confirmed at the ownership predicate: centroid-only classification is insufficient for a topology triangle that crosses the retained profile boundary. The authored arch opening/profile dimensions themselves are already covered by focused tests and are not the smallest responsible subsystem.

## Next
Add a regression to `ArchProfileStitchTests` for a same-material triangle that bridges from the clear aperture into the retained annulus while its centroid remains inside the aperture. Then extend ownership narrowly to detect that inner-boundary crossing without granting profile ownership to unrelated material, out-of-depth, or out-of-wedge topology.

## Follow-up
This was an early narrowing experiment, not the final root cause. `experiment-002-projected-surround-carve.md` provides the discriminating baseline: the opening carve stopped at the wall body while the decorative surround projected farther forward. The retained-profile hypothesis is therefore superseded for this capture.
