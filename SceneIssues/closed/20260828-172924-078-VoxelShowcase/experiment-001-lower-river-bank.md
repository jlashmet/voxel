# Experiment 001 — lower-river receiving bank

**Hypotheses**
1. The marked grass is a disabled/incorrect moat or stale startup bake.
2. The marked grass is the lower-river north bank rising through the intended waterfall receiving-water footprint.

**Action / source**
Using capture camera `(59.45, 20.35, -1.55)` and its five normalized circles, project the marked sightlines into the deterministic showcase castle layout for seed `0x5EED1234`. Compare those locations with the moat footprint, `CastleLandscapeAuthoring` waterfall/pool/outlet, `CastleSiteAuthoring.LowerRiverGorge`, and the checked-in bake chronology.

**Result**
All five marked regions resolve onto the waterfall-facing/north side of the lower river, represented by channel offsets about `+39, +50, +55, +67, +79` voxels. The compatibility moat is disabled and is not on these rays. The startup bake was refreshed after waterfall/pool authoring, so a pre-water bake is not the cause. `LowerRiverGorge` only fills water through `|dz| <= 42` while its bank profile rises toward the grass terrace outside that core, leaving the captured shelf across the receiving side.

**Verdict**
Hypothesis 2 confirmed. Repair the bounded receiving-bank cross-section, not generic terrain, moat policy, or scene coordinates. Preserve the south bank, cascade material, and dry outer shore.
