# Experiment 013 — generic raster fast-path reuse

## Reason
`PrimitiveRasteriser` contains generic whole-block optimizations used by Mountain Dragon baking, but comments still described them as "Mountain headroom" and "Mountain support" and the clearest regression lived inside a Mountain Dragon test class. The implementation must remain engine-generic.

## Change
- Reworded the Box-carve and Frustum-fill fast-path comments in `PrimitiveRasteriser` in storage/geometry terms only; no executable behavior changed.
- Added `PrimitiveRasteriserWholeBlockFastPathTests` outside Mountain Dragon authoring. It exercises:
  - a full Box carve over a Uniform storage block and requires one whole-cell replacement, zero partial-cell mutation opens, exact 512-write accounting, and canonical Empty result;
  - a full constant-radius Frustum fill over a canonical Empty block and requires one whole-cell replacement, zero partial-cell mutation opens, exact 512-write accounting, and Uniform material result.

## Validation
The generic regression is committed but not yet exact-CI green. Do not mark the raster reuse task complete until it passes an exact targeted request through `ci-test/fixes/agent-4` after the currently queued traversal-profile run finishes.
