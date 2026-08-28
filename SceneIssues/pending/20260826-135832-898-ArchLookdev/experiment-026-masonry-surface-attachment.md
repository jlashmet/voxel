# Experiment 026 — masonry surface attachment

**Observed falsifier.** Exact semantic-mass request `1f94b636e2295a64922abe78e62560f86ba86966`, run `33154793211`, passed the three-mass regression and 45-second replay. Direct inspection still rejects the result: the crown mass floats over the white opening below the arch ring, the lower/haunch foliage spills into the passage, and the sparse right accent is the only group that visibly reads as stone-supported.

**Discriminator.** Macro grouping is now proven; the remaining dominant defect is attachment. The semantic centres were derived from inner-edge supports but not projected outward onto the masonry face.

**Action.** Preserve exact topology, leaf/flower shapes, mass spacing, and counts. Shift lower/haunch targets 0.34 m outward onto the left stone face. For crown targets, derive an outward radial direction from the authored opening springline (`y=6.4 m`) and project each cluster/bouquet 0.34 m onto the arch ring. Leave the sparse right accent unchanged.

**Regression / falsifier.** The existing semantic-mass regression now additionally requires all lower/haunch cluster centres left of `x=-1.45`, all crown foliage/blossoms above `y=7.90`, exact target centroids, zero stem span/no slivers, unchanged envelopes/gaps, 128 leaves / 30 heads / 3 draws / <=4,096 vertices, and deterministic rebuild. Reject if the saved frame still floats over the opening or reads as paper decoration.

**Blast radius / cost.** ArchLookdev-only translation of existing vertex groups; no topology, renderer, draw, material, per-leaf object, or steady-state cost change.
