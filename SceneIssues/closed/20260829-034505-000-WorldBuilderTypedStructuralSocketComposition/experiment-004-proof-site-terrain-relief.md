# Experiment 004 — proof-site terrain relief

## Hypothesis
The repeated bridge/cliff visual failures were not primarily camera defects: the deterministic proof-site selectors were searching the gallery's deliberately calm inhabited valley, so the authored structures could not demonstrate the substantial gorge or steep multi-level terrain required by acceptance.

## Action / source
Inspected full-resolution frames from run `33334360953`, then reproduced `TerrainQuery.HeightAt` and both site-search loops for seed `0x5EED1234`. The prior built-player log measured bridge relief at only 12 voxels (1.2 m). The selectors were moved to the deterministic valley/mountain transition with fail-closed minimums of 40 voxels bridge relief and 80 voxels cliff rise. A focused regression now asserts those acceptance thresholds rather than merely `relief > 0`.

## Result
Independent fixed-point reproduction selects bridge site approximately `(-480,-15040)` with endpoint heights 268/265, interior minimum 217, and 48 voxels (4.8 m) natural relief. The cliff selector chooses approximately `(3120,-14760)` with low/high 264/389 and 125 voxels (12.5 m) rise. Both remain bounded by existing support/spatial limits.

## Verdict
Confirmed composition-site defect. Keep shared terrain and structural solver unchanged; terrain-site policy belongs in showcase composition. The new selectors fail closed if the acceptance terrain cannot be found.

## Next step
Exact-SHA player evidence must prove grounded bridge supports, substantial gorge/river read, a legible steep cliff settlement, and all traversal/negative contracts.
