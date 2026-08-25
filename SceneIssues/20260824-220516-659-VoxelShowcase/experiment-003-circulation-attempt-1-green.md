# Experiment 003 — circulation production attempt 1, structural green

**Hypothesis** — The lower-town stair bundle is partly caused by two redundant circulation representations: a secondary lower-west stair chain beside the already-walkable main spine, and dedicated `kentridge-stair-*` geometry over the continuously ramped main road. Removing those duplicate routes while preserving the coherent upper-west route and hillside architecture should satisfy the circulation ownership regressions.

**Production changes** — Production attempt 1 removed the `lower-west-stair-street` from `KentridgeUrbanCirculation`, stopped `KentridgeVerticalConnectorCatalogue` from adding four dedicated main-road stair flights, and retired the instantiated `WestMarketLanding` plus its two lower-town `SecondaryStair` links from `KentridgeUrbanSkeleton`. `KentridgeUrbanNodeId.WestMarketLanding = 9` remains reserved so enum values are not renumbered. The upper-west stair/contour chain remains authored.

**Red evidence** — Actions run `32833556126` executed the initial two regressions against broken production code: the lower-west stair had only 6 dm clearance from the main road versus the 12 dm town spacing policy, and `kentridge-stair-south-rise` was still emitted. After geometry removal, run `32834010482` executed three tests: both geometry/spacing tests passed, while the new semantic regression failed solely because `WestMarketLanding` still survived in the urban skeleton. This isolated the remaining stale semantic ownership.

**Inconclusive intermediate run** — Run `32833790423` failed at compilation before tests because the first edit removed lower-west coordinate constants while `KentridgeUrbanSkeleton` still referenced them. This exposed the deeper semantic dependency; it was not evidence against the circulation hypothesis.

**Green evidence** — Actions run `32834260971`, source `fdf2406a93a7f799e6ee72ea95b2a4e5a679cf8a`, executed exactly three EditMode tests in 63 seconds and all passed:

- `LowerTownSkeletonDoesNotAdvertiseDuplicateSecondaryStairChain`
- `SecondaryParallelStairStreetsKeepTownSpacingFromMainSpine`
- `VerticalInfrastructureDoesNotOverlayDedicatedStairsOnContinuousMainRoad`

The run retained the upper-west secondary stair route, retaining-wall architecture, and civic campanile as explicit assertions. Test artifact `9557998101` (`scene-220516-circulation-tests`) has digest `sha256:c685c7af3049b8d63c212814f1db8301d6b9e5b9c5eadd03bdc433f6a11b96d5`.

**Result** — Structural hypothesis confirmed. The two duplicate lower-town circulation mechanisms are removed without deleting the intended upper-west alternate route or vertical hillside architecture.

**Next** — Remove the now-dead private main-road stair-builder helpers and temporary lower-west coordinate constants without changing behavior, then replay the original saved camera. The broad SceneIssue remains open until that exact-view render is inspected for remaining building overlap, unsupported geometry, and any legitimate local-access stairs that still read incoherently.
