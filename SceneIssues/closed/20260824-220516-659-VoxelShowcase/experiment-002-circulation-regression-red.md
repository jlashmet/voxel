# Experiment 002 — circulation coherence regression, red proof

**Hypothesis** — Two independent authored circulation defects contribute directly to the stair bundle in the saved lower-town view: the secondary lower-west stair street violates the town's normal spacing from the already-walkable main spine, and `KentridgeVerticalConnectorCatalogue` adds dedicated hard-stone stair flights on top of a main-road surface that `KentridgeVerticalTownSurfaceCatalogue` already realizes as a continuous supported climb.

**Regression added** — `VoxelEngine.Tests.EditMode.KentridgeCirculationCoherenceTests` contains two focused tests:

1. `SecondaryParallelStairStreetsKeepTownSpacingFromMainSpine` checks every vertical secondary `StairStreet` against the main-road corridor and requires the settlement composition policy's `MinSpacingDm` unless the route is not running parallel beside that corridor.
2. `VerticalInfrastructureDoesNotOverlayDedicatedStairsOnContinuousMainRoad` requires the vertical-infrastructure catalogue to contain no `kentridge-stair-*` definitions while still retaining retaining-wall architecture and the civic campanile.

**What was performed** — GitHub Actions run `32833556126` executed exactly those two EditMode tests against unchanged broken production source. Source commit was `9eb1c334a46bbc1ce8f95af7811e8b2ff3270839`; test artifact `9557732191` (`scene-220516-circulation-tests`) has digest `sha256:e5995586421c580184439ebe35404cda1e753c05363dd8dbb9afcfa0cd851243`.

**Result** — Red as intended. Unity executed exactly two test cases and both failed for the causal conditions:

- `lower-west-stair-street` has only **6 dm** between its east edge and the main-spine road corridor; Kentridge's density policy requires **12 dm**.
- `kentridge-stair-south-rise` is present in `KentridgeVerticalConnectorCatalogue`, proving the vertical-infrastructure stage still overlays a dedicated stair flight on the main-road climb.

Unity finished the focused run in 62 seconds with status 2. There was no compile failure and no unrelated test failure.

**Next** — Production attempt 1 will stop authoring `lower-west-stair-street` and stop adding the four `kentridge-stair-*` main-road flight builds. Retaining walls, campanile, the coherent upper-west stair/contour route, and per-block urban access remain unchanged. Then rerun these exact two tests before any visual judgement.
