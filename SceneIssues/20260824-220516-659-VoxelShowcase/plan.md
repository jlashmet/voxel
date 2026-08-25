# Plan — make generated Kentridge composition spatially coherent

## Goal

Resolve `20260824-220516-659-VoxelShowcase`: the saved town view contains multiple overlapping staircases, buildings packed too closely, floating/unsupported geometry, and generally weak translation from semantic town description to physical layout.

This is broader than one malformed primitive. The fix must identify which authored placement/connection stage owns the incoherent geometry and enforce a reusable spatial invariant rather than special-casing the captured camera or named coordinates.

## Initial hypotheses

1. A later urban/circulation stage is still placing stairs or access structures independently of the authoritative building reservations, allowing multiple connectors to target the same frontage or terminate without a valid destination.
2. The existing named-plot versus secondary-placement compaction fixed one class of overlap but does not constrain secondary-vs-secondary or connector-vs-structure occupancy strongly enough.
3. Some apparent floating geometry may be stale/duplicate placement rather than a support-height bug; exact replay and authoritative catalogue inspection must distinguish these before production edits.

## Investigation / validation

- [ ] Inspect the original saved screenshot and the current exact-pose replay side-by-side.
- [ ] Confirm whether the reported overlaps/floating pieces still reproduce on current `fixes`.
- [ ] Identify every visible offending object in the saved view back to its authoritative catalogue/urban stage.
- [ ] Measure the relevant placement/occupancy relationships before changing production code.
- [ ] Add the smallest regression that fails on the responsible spatial invariant.
- [ ] Implement the smallest production fix in the owning generator/planner.
- [ ] Run the focused regression plus the smallest affected Kentridge/worldgen suite.
- [ ] Replay the original saved camera and inspect the whole uncircled view for building spacing, connector destinations, and support.
- [ ] Review the final net diff and remove any temporary one-shot CI/reproduction wiring.
- [ ] Update `issue.json` only after structural regression and exact-view visual verification both pass.

## Three-attempt rule

Count only genuine production fixes as attempts. Replay, diagnostics, measurement probes, and temporary CI wiring are experiments but not production attempts. If three production fixes fail to resolve the saved view, stop editing the full scene and build the required minimal reproduction before trying another production change.

## Acceptance

- No building or connector in the reported view visibly intersects another unrelated authored structure.
- Stairs/access connectors terminate at a real intended destination rather than duplicating one another or running into empty space.
- Visible structural pieces are grounded/supported according to the owning placement contract; the fix does not merely hide them from this camera.
- Named building identity and authored town density remain recognizable; the solution must not achieve spacing by deleting large parts of Kentridge.
- A focused regression encodes the causal spatial invariant.
- A fresh standalone saved-view replay reaches the recorded camera pose and the whole reported view reads as a coherent town composition.
