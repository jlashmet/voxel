# Experiment 003 — civic-east access-gap packing

**Hypothesis:** after `ModulePitchDm=160`, the remaining 13-dm CI failure is a real short-frontage/access-gap case rather than another regression false positive.

**Runtime evidence:** exact run `33093457984` reported `orientation 0 at cross-axis 218 leaves only 13 voxels ... expected at least 20`. Orientation 0 is South; authored cross-axis 218 uniquely identifies `civic-east-block-south`, spanning x=1240..1390.

**Reproduction:** the block's 20-dm south court opening is centred at 1315, splitting the 150-dm run into `[1240,1305]` and `[1325,1390]`. `SettlementPlotLayout.PackFrontage` emits at least one site for every positive segment, so the centres are 1272 and 1357. With 72-dm safety envelopes, clearance is `1357 - 1272 - 72 = 13 dm`, exactly matching CI. Increasing module pitch cannot change this minimum-one-per-segment result.

**Discriminator / correction:** widening only this authored access to 34 dm creates `[1240,1298]` and `[1332,1390]`, centres 1269 and 1361, and `1361 - 1269 - 72 = 20 dm`. `KentridgeUrbanAccessPlanner` derives the real route `CourtWidthDm` from the same `AccessWidthDm`, so the correction also widens the civic-east stair/gateway opening rather than adding a geometry-only exception.

**Blast radius / cost:** one Kentridge block datum changes; shared packing, other settlements, named plots, grammar, and site count are unchanged. Both flanking houses remain. The access catalogue changes dimensions only; primitive count does not increase.

**Verdict:** supported. Keep `ModulePitchDm=160` for general density reduction and use `civic-east-block AccessWidthDm=34` for the proven short split-frontage exception. The existing production-catalogue PlayMode regression remains the behavioral gate.
