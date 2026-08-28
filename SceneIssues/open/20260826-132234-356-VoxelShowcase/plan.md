# Plan — VoxelShowcase dirt/grass seam

## Observed / acceptance
The saved camera contains two marked dirt/grass transitions. Fresh built-player replays reached `missingVisible=0`; the lower mark is clean, while the upper circle still shows a hard rectangular moss/grass tongue. Correct camera projection places the upper envelope at about `X=91.0..93.8m, Z=28.6..30.4m`. Acceptance: both original circles remain clean in a fresh built-player replay with no rectangular/notched parcel edge.

## Hypotheses / discriminators
1. **Stale bake / streaming:** falsified by fresh fully resident replays.
2. **Road shoulder quantization:** affected the lower mark only; upper persisted.
3. **Civic terrace/court ownership:** falsified. The civic-west court has no active placement, and built replays before/after the civic repair were pixel-identical throughout the marked ground region.
4. **Organic plot grading:** supported by minimal reproduction after the failed attempts. With the exact VoxelShowcase seed `1592594996`, production planning places `MayorHouse` (`WideHouse`) at `X=91.0..104.2m, Z=25.0..38.2m`, directly covering the upper annotation. Its frontage target is `221`, natural terrain at `(910,295)` is `223`, but the generic twelve-step plot feather claims the parcel edge with moss-capped grading up to roughly `233–234`.

## Selected fix / regression
Keep plot grading inside each archetype's real building-envelope `PadFor` core and leave parcel-edge terrain natural. The separate shallow foundation-skirt catalogue continues to own structural support. Remove the falsified civic/court experiment while retaining the earlier effective `market-main` lower transition.

`SceneIssue20260826132234356MayorHousePlotEdgePreservesNaturalTerrain` builds the production plot catalogue with scene seed `1592594996`, resolves the actual WideHouse placement at `(910,250)`, proves the marked edge `(910,295)` is outside every grading primitive, and verifies its deterministic natural/target heights (`223`/`221`).

## Blast radius / cost
The change is generic to Kentridge plot surfaces, but only reduces spatial ownership: existing building-envelope grading, precedence, foundation skirts, and the established 39-primitive program budget remain unchanged. No per-frame work is added.

## Gate
Use only `ci-test/fixes/agent-8`. Exact-SHA targeted CI must run the focused regression in under five minutes and the built-application harness must replay the saved VoxelShowcase pose for 45 seconds, reach full residency, and show both original marks clean.