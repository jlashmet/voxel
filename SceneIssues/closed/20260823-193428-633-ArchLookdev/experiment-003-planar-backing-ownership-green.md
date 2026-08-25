# Experiment 003 — planar backing ownership fix

**Hypothesis** — Selecting faceted ownership from the occupied cell's own style/boundary and
neighbour occupancy will restore the exact backing face without relying on unrelated empty-cell
halo metadata.

**What was performed** — Removed empty-neighbour boundary suppression from all three mirrored
faceted-mask paths (`FacetedMaskJob`, `SnapshotFacetedMaskJob`, and the synchronous CPU fallback)
on the working tree based at `e420fb0e24c58e7fadbc5d27d38552631b4cc92a`. Reran only
`FacetedBoundaryOwnershipTests.PlanarBackingFaceSurvivesUnrelatedRoundedVeneerHalo` through
`tools/unity-run.sh`. Evidence is `verification-backing-ownership-green.{txt,xml}`.

**Result** — Confirmed: 1/1 passed (0 failed, 0 skipped). The exact +Y planar backing face now
retains a nonzero faceted mask even when the empty neighbour carries the rounded veneer's
sign-correct in-plane halo.

**What was learned** — The smallest ownership invariant is enforceable consistently across live,
snapshot, and fallback extraction. Unit geometry now covers the proven missing-face cause.

**Next** — Rebuild the ordinary player and replay all seven marked regions. This is full-scene fix
attempt 1; its visual result determines whether the ownership defect was dominant.
