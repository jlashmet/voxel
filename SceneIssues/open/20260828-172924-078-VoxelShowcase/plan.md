# Plan — VoxelShowcase missing water

## Observed / acceptance
Capture `20260828-172924-078` marks five regions across one broad grass shelf and notes that the whole patch should be water. The recorded camera is `(59.45, 20.35, -1.55)` in `VoxelShowcase` with five circles. Acceptance: all five marked sightlines expose the intended receiving water; the waterfall/cascade survives; nearby dry shore and the opposite bank do not become water; startup from the checked-in bake remains usable.

## Competing hypotheses and discriminator
- **Moat / stale bake:** a disabled moat or pre-water startup image omitted intended water.
- **Lower-river bank:** the waterfall-facing bank rises through the intended receiving-water footprint.

Discriminator: project every marked region into the deterministic showcase castle layout and compare against moat, waterfall/pool/outlet, lower-river cross-section, and bake chronology.

## Results
`experiment-001-lower-river-bank.md` maps the five marked regions to the lower river's north/receiving side at representative channel offsets `+39,+50,+55,+67,+79` voxels. The compatibility moat is disabled and not on those rays. The bake postdates waterfall/pool authoring. `LowerRiverGorge` waters only `|dz| <= 42` while its bank profile rises toward a grass terrace beyond that core: confirmed shared owner.

## Selected fix / blast radius / cost
Add a reusable castle lower-river receiving-bank repair, invoked after site authoring and after baked-showcase restore. It repairs only `x = waterfallStreamX ±120` and north-side offsets `dz=35..80`, preserves cascade voxels, leaves the south bank and outer 10-voxel dry shore unchanged, and republishes baked resident state. Startup repair has a 1,000,000-write guard; the bounded footprint is 241×46 columns and runs once, not per frame.

Regression `CastleLowerRiverWaterRepairTests.ShowcaseMarkedReceivingBankBecomesWaterAndStopsBeforeOuterShore` covers all five offsets plus dry-shore/cascade negatives and bounded writes.

**Current source:** `fixes/agent-7` head containing repair, bake compatibility path, regression, and this evidence. **Remaining gates:** exact-SHA targeted regression CI; exact-SHA built-application VoxelShowcase replay of the assigned capture; then pending/fixed metadata, final master merge, and non-force promotion.
