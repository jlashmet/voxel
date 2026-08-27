# Plan — 20260826-132234-356-VoxelShowcase

## Acceptance
At the exact saved VoxelShowcase camera, both annotated Dirt/grass contacts must read as continuous authored terrain: no metre-scale stair-step edge, rectangular Grass tongue, or missing/streaming surface.

## Evidence and discriminators
- The latest fresh replay is stable (`visible=644`, `missingMax=0`), falsifying streaming/LOD churn as the marked defect.
- Kentridge's normal authored streets are at z=520/900 or x=1170/1490; the marked contacts resolve to the district-terrace corridor around z=300–380, falsifying the town-road catalogue as owner.
- Replacing six terrace shoulder tiers with continuous ramps improved the lower circle, proving live district-terrace geometry participates.
- Restricting the higher-precedence urban surface correction to built cores fixed an overbroad owner but fresh replay still retained the upper rectangle, so that defect was real but insufficient.
- The `market-main`→`upper-shoulder` 2 dm tapered ownership seam passes its structural regression, yet fresh replay `33035969054` still retained the upper Grass rectangle; that seam is falsified as the final cause.
- `upper-shoulder` spans 200 dm along z but its west transition originally used one natural-terrain sample at z=340 for the entire edge. The two marks therefore see different natural elevations against one fixed wedge endpoint. This uniquely explains why the lower/central contact improved while the upper contact remained proud and rectangular.

## Selected fix
Only `upper-shoulder` now samples the natural west edge every 5 dm (0.5 m) and emits a local x-axis transition ramp for each bounded strip. Other terraces retain their existing single-edge path. The one profiled terrace receives a bounded 96-primitive budget; all other district terraces remain at 40.

## Behavioral regression / blast radius / cost
`KentridgeTerraceSeamRegressionTests.SceneIssue20260826132234356UpperWestShoulderFollowsLocalTerrainWithoutMetreScaleSteps` executes the production district catalogue at seed `0x4B454E54`, requires exactly 40 contiguous <=0.5 m west-edge strips, and verifies every emitted west ramp meets `TerrainQuery` at its local sample. It also requires `upper-shoulder` budget 96 while `market-main` remains 40. Existing correction-ownership, market/upper taper, and terrace-ramp regressions remain unchanged.

Blast radius is one authored west transition only. Cost is at most 40 local baked strips / roughly 93 terrain primitives for one static feature; there is no per-frame loop or global terrain change.

## Remaining gates
- [x] Every marked region tied to captured runtime evidence.
- [x] Competing owners and falsified hypotheses recorded.
- [x] Behavioral regression added with blast-radius and cost assertions.
- [ ] Exact-SHA targeted CI.
- [ ] Fresh-bake replay from the saved camera; visually inspect both marks and telemetry.
- [ ] Commit accepted `verification-final.png` and final metadata.
- [ ] Close capture and integrate exact verified feature head to current master per user instruction.
