# Plan

## Acceptance
Improve the authoritative `WorldRoadNetwork` presentation without changing route/topology authority: coherent curved/diagonal edges; formed carriageway/shoulders and bounded cut/fill; deterministic shared wear; topology-aware junctions; stable chunk/LOD continuity; preserved terrain/vegetation/collision/destruction semantics and budgets; and production-quality exact built-player validation in `KentridgePlayableSlice`.

## Evidence and discriminating results
The original road was a union of segment-local fields; presentation refinement plus exact resolved-vertex junction preservation addresses that reusable geometry defect without rewriting route authority. Generic `Trail`/custom-profile fixtures prove the shared path independently of Kentridge.

Two materially different generic fixes still left the same built-player trench. A production repro then isolated Kentridge composition: run `33362012059` proved plot surfaces could overwrite the wider road grading envelope. Deferring organic road rasterization until after plots passed focused/module/player validation in run `33369999858`. Human inspection still rejected the trench, so a second minimal repro was added instead of changing generic geometry again. Run `33370815109` measured an 8 dm raised plot feather on the real public approach for role 14, isolating the remaining defect to Kentridge parcel frontage authoring.

The narrow frontage correction keeps twelve 1 dm side/rear terraces but finishes only canonical `z=0..PadFor(...).Z` at core elevation. It rotates with semantic `FrontageDirection`; no role coordinates, cover meshes, parallel road geometry, shader island, schema change, or route change are introduced.

Merge `4b7675202b9b164b8f9a7a33e8d394443b3725aa` incorporated master `8d8fccd1198e36d164c92fc80760580de12efe51`. Exact post-merge run `33375836636` failed `OverlappingPlotLandformsMustRunBeforeRoadGrading`: master’s reservation-aware composition had moved the solved organic road write back before plots (road index 6, plot index >257). The standalone player still built/replayed, so this was a product regression, not infrastructure. Production commit `cdc0a5755edbf72a65f5cd0b2e336606df81661a` preserves master’s single solved `organicRoadNetwork` and reservation snapshot while deferring only `BuildResolvedRoadNetwork(...)` until after plot surfaces.

## Blast radius / cost
The frontage correction adds at most three bounded landform primitives per affected plot definition; worst-case `MaxPrimitives` is 42 vs 39. The compatibility fix only reorders the existing solved road catalogue and adds no primitives, voxels, materials, vertices, allocations, or persisted data. Existing storage/vertex/instruction budgets remain unchanged and must still be measured/validated.

## Remaining gates
Exact-SHA rerun the Kentridge composition regression through `ci-test/fixes/agent-3`; require derived module validation and built-player integration. Then validate the broader road regression suite/budgets, inspect full-resolution exact player evidence for curve/diagonal edge, both uneven shoulders, a real junction, non-flat cross-section, medium/far continuity, vegetation recovery, and CharacterMotor traversal. Re-fetch/merge current master before final promotion and revalidate if that merge affects the work.
