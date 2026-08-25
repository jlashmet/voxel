# Experiment 002 — planar backing ownership regression

**Hypothesis** — A rounded veneer halo on an empty joint cell incorrectly suppresses an adjacent
solid planar backing cell's exact exposed face, producing the marked top/right apertures.

**What was performed** — Added the minimal two-cell regression
`FacetedBoundaryOwnershipTests.PlanarBackingFaceSurvivesUnrelatedRoundedVeneerHalo` on the working
tree based at `e420fb0e24c58e7fadbc5d27d38552631b4cc92a`. The solid cell is planar masonry;
its empty +Y neighbour carries a sign-correct rounded-box halo extruded along Z, exactly matching
the veneer/backing ownership relationship. Ran only that EditMode test through
`tools/unity-run.sh`. Evidence is `verification-backing-ownership-red.{txt,xml}`.

**Result** — Confirmed: 0/1 passed. The +Y faceted face mask was zero even though the occupied
backing cell itself has no boundary that owns or displaces that face.

**What was learned** — The faceted extractor currently lets boundary metadata from the empty side
steal an exact occupancy face. That violates one-surface/one-owner at this composition: the halo
describes the separate rounded veneer, while the planar backing owns the solid→empty transition.
The same suppression exists in the live job, snapshot job, and synchronous CPU fallback.

**Next** — Make faceted ownership depend on the occupied cell's own reconstruction/boundary and
actual neighbour occupancy in all three mirrored paths, rerun the regression, then replay the full
scene as genuine fix attempt 1.
