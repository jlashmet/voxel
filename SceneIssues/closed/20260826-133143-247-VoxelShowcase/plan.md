# Plan — 20260826-133143-247-VoxelShowcase

## Observed defect / acceptance
The single saved VoxelShowcase pose has no circle annotations, so the whole frame is the evidence region. The note says the anonymous houses are still cramped. Acceptance: adjacent anonymous houses on the same authored Kentridge frontage leave at least 20 dm between their 72-dm production envelopes while named-plot reservations and pedestrian access remain intact.

## Competing hypotheses / evidence
1. **Anonymous packing density is too high — supported.** Baseline 80-dm module pitch produced 42 pre-reservation sites and overlapping envelopes. Raising Kentridge-local pitch to 160 dm reduces this to 26 sites.
2. **The `+14` coverage uplift alone is causal — rejected.** Removing it does not materially change the cramped key placements.
3. **Named gameplay reservations cause crowding — rejected.** They only remove anonymous sites.
4. **160-dm pitch alone is sufficient — rejected by exact CI.** Source `b19345f1…` still produced 13 dm clearance on the south frontage at z=218. That coordinate is `civic-east-block`; its 150-dm frontage is split by a centered 20-dm court-access gap, and `PackFrontage` emits at least one site per non-empty segment.

## Selected fix / regression
Keep shared packing semantics unchanged. Use 160-dm Kentridge module pitch and widen only civic-east `AccessWidthDm` 20→34. The two 72-dm flanking envelopes then leave exactly 20 dm, while the real court stair/gateway receives the same wider semantic opening.

Regression: `VoxelEngine.Tests.PlayMode.KentridgeUrbanFabricSpacingPlayModeTests.ProductionAnonymousFrontagesLeavePedestrianClearanceBetweenHouses` builds the production urban-fabric catalogue, groups true same-frontage placements, and requires >=20 dm between neighbours.

## Blast radius / cost / verification
Changes are Kentridge-local: no shared `SettlementPlotLayout`, named structures, other settlements, or per-building primitive budgets change. Anonymous definitions/placements fall 42→26 (~38% fewer), reducing generation/render cost.

Exact source `00ca989651ebe5228d065d39135af4b6aaeb8a45` passed final targeted CI request `8f8aed4476bef7ffbc32bfa43b3b793b409afe3a`, run `33095828697`: exact regression, saved-pose real-player replay, artifact upload, and `ci/single-test` all succeeded. `verification-final.png` is the inspected replay evidence. Remaining work is bookkeeping only.
