# Experiment 034 — nonvisual acceptance audit

## Question
Which remaining acceptance clauses can be validated without the renderer-gated built-player replay, using the current executable-equivalent feature state and already-green focused behavioral evidence?

## Current-state prerequisite
- `fixes/agent-6` resumed at `9b30c22969b54c19da7153d0e1d0624a7a5fdf14`.
- `ci-test/fixes/agent-6` remains `a0efab2841ee7175cea6e678b0fd30ee8b724f44`; latest run `33641059051` is completed failure.
- `origin/master` remains `b18d470f66221c7cb6091249f4683c2d994bffec` with the renderer restoration.
- Experiment 033 proves the feature changes after Retry 5 source `7e6d308...` are assignment documentation/evidence only, so the executable/test state has not changed since that request.

## Acceptance (1): authoritative source-backed graph — satisfied
`TopDownWorldPhysicalPlanner.Plan/TryPlan` takes the production `TopDownWorldLayout` directly. It derives every physical node from `layout.Nodes`, validates region anchors against those graph node ids, validates route constraints against `layout.Routes`, and iterates only hard routes from that layout. `KentridgeTopDownWorldPhysicalIntent` supplies semantic region/settlement policy keyed by `KentridgeTopDownWorldLayout` ids rather than a replacement topology. The playable ownership regression was green in run `33562388717` and the shipped catalogue contained the macro towns/routes/geography.

## Acceptance (3): physical graph reachability / contiguous generated hard routes — satisfied
The focused production test `MacroGraphRealizesSettlementsTerrainAwareRoadsAndGeographyThroughProductionWorldBuilder` builds from `MountingForceTopDownWorldDefinition.Build`, requires all six settlements, all 20 hard physical routes, contiguous tile adjacency, exact graph-node endpoints, and source-layout reachability from the root to Moordell/Rossdam/Fairy/Orc/Hightown. The seven-target replay was green in `33469175243`; later ownership validation `33562388717` retained macro route generation. The renderer blocker affects publication/capture, not this graph-to-physical route plan contract.

## Acceptance (4): terrain-aware roads / explicit blocked-geography solutions — satisfied
The shared physical intent exposes `GoAround`, `PassThrough`, and `DesignatedCrossing`, with strict/default constraint handling. The planner refuses a blocked hard route when no semantic solution exists, validates blocking-region intersections after solving, and validates route slopes. The independent synthetic blocked-water fixture explicitly proves the unsolved route fails with a `no authored` error and succeeds only after a semantic `GoAround` constraint is supplied. Kentridge also authors the Rossdam lake detours and Southern Ridge designated pass through the same API.

## Acceptance (5): reusable geographic-region authoring/query — satisfied
`TopDownWorldPhysicalIntent` is shared/config-driven and covers required kinds `WaterBody`, `MountainRidge`, `ValleyPass`, `PlainsMeadow`, `ForestWoodland`, and `Generic`; semantic relationships `AnchoredAt`, `Between`, `AdjacentTo`, `Contains`, `Separates`; extents/elevation/depth intent; deterministic variation; provenance; blocking semantics; and route-region solutions. `TopDownWorldPhysicalPlanner` resolves this intent deterministically against any supplied `TopDownWorldLayout` and exposes region/settlement/route queries on the resulting physical plan. Independent non-Kentridge blocked-water and spatial-reservation fixtures already prove this is not Kentridge-only policy.

## Acceptance (8): no second scene-local graph/direct voxel hierarchy — satisfied
The feature diff contains no `.unity` scene modification and no giant static destination hierarchy. `KentridgePlayableSlice` composes production catalogues and fixes only one-shot catalogue ownership ordering; it does not define a second graph or write destination voxels directly. Kentridge-specific physical intent references authoritative graph ids and semantic offsets/extents; the shared planner/catalogues own physical realization. No feature-branch `.github/test-request.json`, alternate transport, or direct scene destination coordinates were introduced.

## Deliberately not checked
- (2), (6), (7), and (10) still require readable exact built-player evidence and therefore remain renderer-blocked.
- (11) remains incomplete because final multi-target CPU/GPU/memory/streaming cost evidence is incomplete.
- (9) remains unchecked even though the regression suite is broad and many focused runs are green: the perimeter-foundation regression was extended after the prior green hollow-shell coverage and has not executed successfully on the current executable state because repository-derived validation fails first in unrelated stale renderer tests. Do not weaken this requirement.

## Result
Acceptance clauses (1), (3), (4), (5), and (8) can be checked now without changing acceptance or relying on blocked visual evidence. No product, renderer, workflow, scene, or CI request change is justified by this audit.