# Experiment 002 — positive-edge ownership at Market Square

## Baseline

- Source branch: `fixes`
- Baseline commit: `937bfd19736e6e6f889b1c85af8715e7fe8b32a7`
- Regression commit: `e7789e5e28bc570ec4e2de25457b845db0c8f7fa`
- Scene issue: `20260824-221508-896-VoxelShowcase`
- Production-fix attempts before this experiment: 0 / 3

## Hypothesis

The lower saved-view seam is an endpoint-ownership bug, not a height mismatch. Kentridge's authored Market Square bounds are expressed as inclusive lattice coordinates, but both plaza catalogues pass the semantic width/depth directly to count-based box footprints. The positive X/Z endpoint is therefore omitted.

## Evidence

1. Exact-camera replay and unobscured replay collapse the three lower marked defects to one straight exposed strip near world `Z = 590 dm`.
2. The semantic Market Square is centred at `Z = 520 dm` with depth `140 dm`, so its authored positive Z endpoint is exactly `590 dm`.
3. `KentridgeVerticalTownSurfaceCatalogue.ResolveRoadSegment` explicitly converts an inclusive authored road span to a voxel count with `max - min + 1`.
4. `KentridgeVerticalTownSurfaceCatalogue.BuildPlaza` currently uses `plaza.SizeDm * scale` with no `+1` while placing at the authored minimum.
5. `KentridgeMarketPiazzaCatalogue.Build` uses the same count and origin convention.
6. The engine's feature/raster tests treat footprints as half-open counted volumes: primitive maxima must remain strictly below `origin + footprint`, and raster sub-volume maxima are exclusive. A footprint of 140 starting at Z=450 owns Z=450..589, not Z=590.
7. The canonical VoxelShowcase composition already adapts the graded town surface, hard piazza, and dressing to the same `KentridgeVerticalProfile`; the earlier height-source hypothesis is therefore rejected for this scene.

## Regression

Added `VoxelEngine.Tests.EditMode.KentridgeMarketPiazzaTests.HardAndGradedPiazzaOwnTheSameInclusiveAuthoredBoundary`, which requires both the hard piazza and graded Market Square to:

- start at the semantic authored minimum,
- end at the semantic authored maximum after converting footprint count back to an inclusive coordinate (`origin + footprint - 1`), and
- therefore own the same positive X/Z endpoints.

### Red result

- CI request commit: `2564d3d422adda9c0cb9032136dc95209ad47e9a`
- Workflow run: `32851636363`
- Source under test: `e7789e5e28bc570ec4e2de25457b845db0c8f7fa`
- Platform: `EditMode`
- Executed: exactly 1 test case
- Result: failed as predicted
- Assertion: `Hard piazza must own its authored +X endpoint.`
- Expected: `1280`
- Actual: `1279`

This is the exact one-voxel positive-edge omission predicted by the hypothesis; Unity setup, compilation, and request resolution all completed successfully before the assertion failure.

## Verdict

Confirmed boundary-contract mismatch. The saved seam sits on the exact authored positive-Z row omitted by both plaza footprints, and the focused regression independently proves the same off-by-one contract on +X.

## Candidate production fix

For both plaza builders, preserve the authored minimum placement and convert inclusive semantic extents to voxel counts:

- `width = plaza.SizeDm.X * scale + 1`
- `depth = plaza.SizeDm.Y * scale + 1`

This matches the road-span convention and does not add camera- or screenshot-specific geometry.

## Next

Apply the candidate fix as production attempt 1 / 3, rerun the same regression to green, then run the smallest affected Kentridge/worldgen checks before regenerating/replaying the saved VoxelShowcase view.
