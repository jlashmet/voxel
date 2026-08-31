# Plan

## Acceptance
Improve authoritative `WorldRoadNetwork` presentation without changing route/topology authority: coherent curved/diagonal edges; formed carriageway/shoulders with bounded cut/fill recovery; deterministic shared wear; topology-aware junctions; stable chunk/LOD continuity; preserved vegetation/material/collision/destruction semantics; existing budgets; and exact built-player AAA validation in `KentridgePlayableSlice`.

## Root cause and evidence
The original road geometry was a union of straight segment-local influence fields. That prevented continuous turn/junction shaping before `SmoothSurface`; shader work could not reconstruct missing physical semantics. Presentation refinement and explicit resolved-vertex junction preservation now address that reusable geometry defect without rewriting resolved route authority.

Run `33340511339` at `f30c398c17b925fd331f57c8c8c70d44f383779e` exposed `TooManyDefinitions` after over-subdividing turns. Ordinary turns are now bounded to one entry/exit chamfer. Run `33345875621` at `4cb2625f1696ebfe4bbb26d044eed08b0e25e21e` subsequently built/replayed Kentridge successfully with no catalogue overflow, proving that symptom removed.

That same built-player evidence still failed the AAA visual bar: roads read as narrow trenches with steep/stepped banks. Production tracing showed `WorldRoadNetworkRoute` already separates `SurfaceRadiusDm` from broader `GradeRadiusDm`, but physical lowering discarded the latter and reused surface/material coverage for density grading.

## Selected fix
Keep the shared profile -> presentation path -> bounded generic `EmitTerrainCorridor` -> voxel surface path. The current branch carries `GradeRadiusDm` as the physical corridor radius while packing the narrower authored surface radius plus scale into the existing final corridor operand. `TerrainCorridorRasteriser` now exposes independent grading and visible-surface coverage: grading stays fully formed through the authored shoulder and blends back to source terrain across the wider bounded envelope; material/detail stops at the authored surface radius. Plain-scale legacy corridor programs retain the prior single-envelope behavior. Instruction length remains 17 and primitive/vertex storage does not grow.

Independent `TerrainCorridorGradingEnvelopeTests` cover packed dual-envelope sampling, production road-catalogue encoding/bounds, legacy compatibility, and packing round-trip. Existing `WorldRoadPresentationRegressionTests` cover route authority, topology, curves, slopes, vegetation, wear, non-road isolation, and storage/vertex budgets.

## Current state / remaining gates
`fixes/agent-3` includes current `origin/master` via merge `b3cd098a1910ff23ebee96bf76270c24cd2823f4`; observed master is `2ea5f5c95f89fbf0403dbefb50b782829583d304`. The dual-envelope implementation is present but has not yet been validated at this merged head. Run exact-SHA focused/module-derived CI, measure relevant definition/footprint/voxel/player-memory costs, then inspect exact built-player Kentridge curve/shoulder/junction/non-flat/far-field/vegetation/traversal evidence. If the same trench symptom remains after this materially different fix, isolate a focused repro/root cause before another geometry change. Complete metadata/close/promotion only after all exact-SHA gates are green.
