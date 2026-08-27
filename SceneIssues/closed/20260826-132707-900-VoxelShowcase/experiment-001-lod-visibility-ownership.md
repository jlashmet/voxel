# Experiment 001 — cross-ring visibility ownership

## Question
Can the shipped scheduler expose both a coarse terrain chunk and finer descendants while finer LOD coverage is only partially ready?

## Runtime evidence
`VoxelSurfaceScheduler.CollectVisibility` traverses every ring independently, lets each worker collect visible entries, then appends each ring's `Visible` entries directly into `_visibleSolids`. There is no cross-ring ownership check in that path. The repository already defines `SurfaceLodActiveCoverage`, whose tests require a complete parent to remain active until all eight current children are complete.

## Discrimination
- This mechanism is time-dependent: as child chunks publish, the set changes without any shader/material change, matching the capture's transient low-resolution-looking patches.
- It predicts coarse/fine overlap over broad patch interiors, unlike a transition-normal defect that should be localized to LOD boundaries.
- A static shader/material hypothesis does not explain why readiness convergence changes the symptom.

## Result
Confirmed a production integration gap: the atomic coverage model exists and is tested, but the renderer-visible scheduler handoff bypasses it. Proceed with a visibility-selector regression and the minimal cross-ring filter.
