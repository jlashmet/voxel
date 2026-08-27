# Plan — 20260826-132234-356-VoxelShowcase

## Observed defect and acceptance
The saved VoxelShowcase pose marks two jagged Dirt/grass contacts. Acceptance requires both circles to read as continuous authored terrain with no metre-scale shoulder treads or rectangular material notch.

## Competing hypotheses
1. **Inactive road-shoulder quantization.** Disproved as complete owner: that catalogue is not on the live Showcase path.
2. **Live district terrace geometry.** Confirmed in part. Replacing six stepped shoulders with one reversible ramp materially improved the lower circle.
3. **Streaming/LOD churn.** Disfavored by stable replays; final diagnostic settles at `visible=644`, `missingMax=0`.
4. **Stale startup bake.** Confirmed/fixed: WorldBuilder inputs are now part of the Showcase bake fingerprint.
5. **Overbroad terrace correction.** Confirmed as one owner but insufficient alone. Core-only urban correction removed the prior full-patch claim, yet exact fresh replay `33035969054` still shows the upper rectangle.
6. **Market/upper plan-view seam.** Current discriminator. `market-main` is 220 dm wider west and 90 dm wider east than `upper-shoulder`, producing a 90-degree material join at their overlapping transition.

## Selected fix / discriminator
Keep urban correction solid/paving repair core-only. On `market-main` only, restore its 72 dm north transition strip to Moss and reclaim Dirt in 36 contiguous 2 dm bands that expand from the exact upper-terrace footprint to the full market footprint. This replaces the one large rectangular ownership jump with sub-metre edge changes; geometry endpoints and all other patches remain unchanged.

## Regression and blast radius
`KentridgeTerraceSeamRegressionTests.SceneIssue20260826132234356MarketToUpperDirtEdgeFeathersWithoutRectangularNotch` executes the production correction catalogue and checks exact outer/inner alignment, monotonic contiguous tapering, <=0.7 m west and <=0.3 m east band steps, and the primitive budget. Existing ramp and core-ownership regressions remain. Cost is 37 extra static PaintSurface primitives on one authored market correction (39 total, budget 40); no per-frame system changes.

## Remaining gates
- [x] Competing owners and failed discriminators recorded.
- [x] Final exact replay artifact inspected; core-only correction falsified as sufficient.
- [x] Market/upper seam discriminator and behavioral regression implemented.
- [ ] Exact targeted CI for seam regression.
- [ ] Exact fresh-bake saved-camera replay; inspect both marked regions.
- [ ] Commit accepted `verification-final.png` and final metadata.
- [ ] Per user instruction, close this capture and merge only `fixes/agent-8` to current master.
