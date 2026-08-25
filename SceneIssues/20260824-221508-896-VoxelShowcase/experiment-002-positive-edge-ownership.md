# Experiment 002 — positive-edge ownership at Market Square

## Baseline

- Source branch: `fixes`
- Baseline commit: `937bfd19736e6e6f889b1c85af8715e7fe8b32a7`
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

## Verdict

Confirmed boundary-contract mismatch. The saved seam sits on the exact authored positive-Z row omitted by both plaza footprints.

## Regression

Add a focused `KentridgeMarketPiazzaTests` assertion that both the hard piazza and the graded Market Square:

- start at the semantic authored minimum,
- end at the semantic authored maximum after converting footprint count back to an inclusive coordinate (`origin + footprint - 1`), and
- therefore own the same positive X/Z endpoints.

The regression is expected to fail on the baseline at +X/+Z because both footprints are one voxel short.

## Candidate production fix

For both plaza builders, preserve the authored minimum placement and convert inclusive semantic extents to voxel counts:

- `width = plaza.SizeDm.X * scale + 1`
- `depth = plaza.SizeDm.Y * scale + 1`

This matches the road-span convention and does not add camera- or screenshot-specific geometry.

## Validation sequence

1. Run the focused regression against this baseline and require a red result.
2. Apply the candidate fix as production attempt 1 / 3.
3. Rerun the same regression and the smallest affected Kentridge suite.
4. Replay the exact saved camera from a fresh committed bake and verify the exposed strip is gone.
5. Reassess the market-stall marked location after the underlying plaza boundary is corrected.
