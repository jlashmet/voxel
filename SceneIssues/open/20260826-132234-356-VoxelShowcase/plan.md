# Plan — VoxelShowcase dirt/grass seam

## Observed / acceptance
The saved camera has two marked dirt/grass transitions. Fresh built-player replays reached `missingVisible=0`; the lower mark is clean, while the upper circle still showed a rectangular moss/grass tongue at about `X=91.0..93.8m, Z=28.6..30.4m`. Acceptance: both original circles are clean in a fresh built-player replay with no rectangular/notched parcel edge.

## Hypotheses / discriminators
1. **Stale bake / streaming:** falsified by fresh fully resident replays.
2. **Road shoulder quantization:** explained only the lower mark.
3. **Civic terrace/court ownership:** falsified by pixel-identical replays and inactive placement evidence.
4. **Organic plot grading:** supported. With VoxelShowcase seed `1592594996`, production planning places `MayorHouse` (`WideHouse`) at `X=91.0..104.2m, Z=25.0..38.2m`; its frontage target is `221`, natural terrain at `(910,295)` is `223`, and the generic twelve-step plot feather claimed that parcel edge with moss-capped grading.

## Selected fix / regression
Keep plot grading inside each archetype's real `PadFor` building envelope and leave parcel-edge terrain natural; the shallow foundation-skirt catalogue still owns structural support. Retain the proven `market-main` lower-transition taper and its bounded primitive budget. Remove tests/wiring from falsified upper-shoulder/civic experiments.

`VoxelEngine.Tests.PlayMode.KentridgePlotSurfaceSceneIssueRegressionTests.SceneIssue20260826132234356MayorHousePlotEdgePreservesNaturalTerrain` builds the production catalogue with seed `1592594996`, resolves the actual WideHouse placement at `(910,250)`, proves `(910,295)` lies outside every grading primitive, and verifies natural/target heights `223/221`.

## Blast radius / cost
The plot change is generic to Kentridge plot surfaces but only reduces spatial ownership; building-envelope grading, precedence, foundation skirts, and the 39-primitive plot program remain unchanged. `market-main` keeps its existing 40-primitive cap. No per-frame work is added.

## Gate
Production source before promotion cleanup: `4a6a103fc562699974a642784a6850b2d752e299`. Use only `ci-test/fixes/agent-8`. Exact-SHA PlayMode CI must run the focused regression in under five minutes and the built-player harness must replay the saved VoxelShowcase pose for 45 seconds, reach full residency, and show both original marks clean.
