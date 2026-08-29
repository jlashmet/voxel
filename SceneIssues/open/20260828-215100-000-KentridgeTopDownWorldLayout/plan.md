# Plan — Kentridge top-down world layout

## Evidence / discriminator
- Architecture issue with `captures: []`: **0 marked regions/camera poses** exist. Final visual/runtime evidence therefore comes from the exact built `KentridgePlayableSlice` replay plus its always-visible top-down inspection overlay.
- Existing WorldBuilder already authors Kentridge, Hightown and one local Kentridge↔Hightown corridor. Hypothesis “missing local Kentridge geometry” is falsified. Hypothesis “semantic graph alone is sufficient” is also falsified by acceptance requiring generated reservations/corridors and built-player inspection.
- Imported `world-procgen-clusters.yaml` (validated Warp/Portal graph), `world-inferred-geography.yaml`, map/TMX references, and original `mounting-force` story/runtime evidence establish the hard traversal spine and soft compass hints. Selected fix: reusable macro graph + deterministic hard-route planner + shared physical WorldBuilder backend; hard topology wins when soft orientation conflicts.
- CI run `33221251860` proved the first semantic regression green, then stopped before player build because this capture-less architecture issue lacked replay dimensions. Added 1600×900 metadata; no runtime failure was observed in that diagnostic run.

## Fix
- `Game.WorldBuilder.Api/Runtime`: named regions/settlements/landmarks, reserved envelopes, typed verified-vs-inferred provenance, corridor widths, deterministic placement, hard-route reachability validation, and a one-shot build selection so unrelated Kentridge consumers do not inherit world-scale cost.
- `MountingForceTopDownWorldDefinition`: 21 major outdoor/story destinations and 20 source-backed hard routes, including Stanley’s house, Radcliffe Mansion and Bandit Hideout. Five coarse route cells place Hightown exactly on its existing generated +400 m anchor; compatible sign/dialogue hints shape Moordell/Rossdam/Fairy/Orc placement.
- `TopDownWorldVoxelCatalogue`: production terrain-grounded route tiles plus small neutral destination markers. Kentridge playable composition selects the semantic layout; shared voxel generation consumes it. Existing detailed town/corridor passes override these lower-precedence surfaces.

## Regression / failure case
`KentridgeTopDownWorldLayoutTests.SourceBackedWorldLayoutIsDeterministicPhysicallyRealizedAndRejectsSeveredHardRoute` checks deterministic non-overlap, provenance, verified reachability, exact Hightown anchor alignment, continuous Kentridge exit tiles, successful production `FeatureCatalogue` realization, and rejects a deliberately severed Logan Castle hard route.

## Blast radius / cost
Planner: O(21 nodes + 20 routes). Physical plan: 803 route-tile placements + 21 neutral markers / 41 definitions, selected only by Kentridge playable startup; no extra chunks are eagerly generated and existing device/brick/LOD budgets are unchanged. Other Kentridge catalogue callers retain prior behavior/cost.
