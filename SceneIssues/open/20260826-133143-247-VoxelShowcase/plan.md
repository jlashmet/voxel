# Plan — 20260826-133143-247-VoxelShowcase

## Evidence / acceptance
The saved VoxelShowcase pose has no circle annotations, so the full frame is the marked region; the capture note says the houses remain very cramped. Acceptance: adjacent anonymous houses on one authored Kentridge frontage leave at least 20 dm (2 m) between their 72-dm production safety envelopes, without breaking named-plot reservations or pedestrian court access.

## Competing hypotheses
1. **General anonymous packing is too dense — supported.** Baseline `57eab9da86a4ea751f8dcd0d18bd659a2951558f` uses an 80-dm module pitch; exact `SettlementPlotLayout.PackFrontage` math produces 42 sites and overlap on long runs. Raising Kentridge-local pitch to 160 reduces this to 26 sites.
2. **Coverage uplift alone is causal — rejected.** Removing only the `+14` uplift leaves the problematic packed relationships.
3. **Named plots cause the crowding — rejected.** Reservation filtering only removes anonymous sites.
4. **160-dm pitch fully fixes spacing — rejected by runtime CI.** Exact run `33093457984` measured 13 dm at orientation South / cross-axis 218. That maps to `civic-east-block-south`: its 150-dm frontage is split by a 20-dm court-access gap; `PackFrontage` guarantees one site per positive segment, so pitch cannot remove the two flanking sites.

## Selected fix / regression
Keep shared packing semantics unchanged. Retain Kentridge `ModulePitchDm=160`, and widen only `civic-east-block` court access from 20→34 dm. With centre 1315, the gap becomes `[1298,1332]`; the two 58-dm segments place centres at 1269 and 1361, yielding `1361 - 1269 - 72 = 20 dm` envelope clearance. `AccessWidthDm` is also the real stair/gateway width, so the content correction improves the pedestrian opening instead of depending on higher-precedence carving.

The PlayMode regression builds production `KentridgeUrbanFabricCatalogue`, groups placements by authored frontage orientation/cross-axis, sorts adjacent envelopes, and requires >=20 dm. The earlier aligned-catalogue version was rejected because facade-normal alignment mixed unrelated parallel runs.

## Blast radius / cost / gates
Changes are Kentridge-local: no shared `SettlementPlotLayout`, named structures, grammar, or other settlements change. Anonymous sites stay at the reduced 26-site scale; widening one existing gateway adds no primitives and retains both civic-east flanking houses.

Remaining gates: refresh from current `origin/master`, run one exact-SHA targeted PlayMode request plus 45-second saved-pose replay/player build, inspect the green artifact, commit `verification-final.png` and pending metadata, then close and non-force integrate exactly as assigned.
