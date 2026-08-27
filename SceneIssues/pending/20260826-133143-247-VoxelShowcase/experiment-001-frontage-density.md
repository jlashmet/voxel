# Experiment 001 — frontage density

**Hypothesis:** the cramped capture is caused by anonymous Kentridge frontage sites being packed closer than their production envelopes permit, rather than by named-plot placement.

**Action / source:** inspected baseline `57eab9da86a4ea751f8dcd0d18bd659a2951558f`. Replayed `KentridgeUrbanOrganizer` block/run dimensions through the exact `SettlementPlotLayout.PackFrontage` integer formula used by `KentridgeUrbanFabricCatalogue` (`72 dm` envelope, `80 dm` module pitch, capped `coverage + 14`, authored court gaps). Also inspected the combined-catalogue path and named-plot reservation stage.

**Result:** 42 anonymous sites are requested before named-plot reservation. The civic-west south frontage packs centres at 923/969 dm and 1040/1086 dm, so adjacent 72-dm safety envelopes overlap by 26 dm. Removing only the +14 uplift does not materially solve the key density pattern. Named-plot reservation already removes any anonymous 72-dm envelope entering a named plot plus 12 dm and therefore cannot create anonymous-to-anonymous crowding.

**Verdict:** supported. The Kentridge-local packing pitch is the smallest proven owner. A 160-dm pitch preserves the same blocks/gaps/coverage while yielding 26 pre-reservation sites and a conservative worst same-segment envelope clearance of 25 dm.

**Next:** change only the Kentridge module pitch, add a production-stage placement regression requiring >=20 dm clearance, then exact-SHA targeted CI and saved-pose replay.