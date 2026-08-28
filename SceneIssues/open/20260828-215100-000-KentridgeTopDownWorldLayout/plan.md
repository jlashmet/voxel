# Plan — Kentridge top-down world layout

## Evidence / discriminator
- Architecture issue with `captures: []`: there are **0 marked regions/camera poses** to inspect or preserve. Final evidence must therefore come from the focused production-path regression plus real `KentridgePlayableSlice` player replay.
- Existing WorldBuilder deterministically authors local Kentridge/Hightown settlement plans and their corridor, but exposes no connected world-scale topology/layout model.
- Imported legacy evidence (`References/MountingForce/guidance/world-procgen-clusters.yaml`, cross-checked with `world-inferred-geography.yaml` and the original `mounting-force` world-story graph) verifies the outdoor travel spine/branches from Kentridge through overworld, forest/graveyard, Hightown, Moordell/Rossdam, mountains, Fairy/Orc villages and Logan Castle.
- Hypothesis “local Kentridge geometry is missing” is falsified by current settlement/corridor generation. “Copy old TMX coordinates” is rejected because the handoff explicitly calls for topology/feel rather than tile fidelity. Selected: add a reusable macro graph + deterministic constraint planner, with source evidence on each traversal edge.

## Fix / evidence
- Add engine-neutral `Game.WorldBuilder.Api` macro-node/route/layout contracts and `Game.WorldBuilder.Runtime` constraint planning/validation.
- Define Kentridge’s source-backed macro traversal as content: route edges are canonical connectivity; coarse grid deltas encode inferred composition only, not literal legacy coordinates.
- The playable Kentridge composition presents the production result as a small top-down route overlay and emits a deterministic layout log, giving inspectable scene evidence without changing local voxel generation.

## Regression / failure case
`KentridgeTopDownWorldLayoutTests.SourceBackedWorldLayoutIsDeterministicConnectedAndRejectsDisconnectedTopology` executes the production planner twice, checks major verified traversal edges/reachability/non-overlap, and proves an orphaned destination is rejected instead of silently producing a disconnected map.

## Blast radius / cost
No terrain/town/corridor algorithm changes. New planning is O(nodes + routes) over 18 nodes/17 routes once at Kentridge startup; memory is a few small immutable arrays/dictionaries. Rendering adds one compact IMGUI inspection overlay; no voxel/chunk/AI budgets change.
