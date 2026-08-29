# Plan — WorldBuilder Road Network Integration

## Acceptance
Promote Kentridge organic roads into a reusable WorldBuilder contract: semantic intent/provenance first, deterministic terrain-aware resolution, one continuous influence for voxel grading/surface transition/ecology, reusable keep-clearance, and a bounded physical representation. Kentridge is the proving consumer; the generic module must not depend on Kentridge. Final proof is the built `KentridgePlayableSlice` application with endpoint continuity, walkability, natural shoulders/vegetation recovery, and no chunk/LOD/runtime failure.

## Discrimination result
The representation gap is resolved in the branch. `Game.WorldBuilder.Api` owns reusable road profile/intent/resolution/influence/network data; Kentridge `SettlementPlan.Routes` and macro `TopDownWorldRouteSpec` hard connections author it. Physical lowering is one bounded `EmitTerrainCorridor` primitive per piece, not historical carve/core/ten-strip shoulders. `TerrainCorridorRasteriser` grades destructible terrain and persists the same 0..31 road influence used for presentation; Kentridge vegetation consumes that scalar.

Current final targeted CI (`33281599556`, source `2e3574af`) executed seven road regressions and exposed two product/test-contract failures. Shoulder sampling increased `19 -> 26`; leading hypotheses are (A) aggregate `WorldRoadNetwork.TrySample` switched to a stronger overlapping route near a junction, or (B) per-coordinate edge jitter makes a single route non-monotonic. Discriminator: sample one isolated route/influence on a perpendicular cross-section, then compare the aggregate at the failed area. Do not weaken the monotonic shoulder requirement. The vegetation regression fails earlier because authored point `(900,455)` intersects current settlement geometry; compare feature/master ownership and the production planner before deciding whether authored layout or test setup is wrong.

## Terrain flags
The generic resolver supports Blocked/Water/Reserved/Pass flags and profile crossing policy. Current production terrain/Kentridge/top-down route sources provide no authoritative water/reserved/barrier map, so adapters return `None` rather than fabricate crossings; deterministic fixture coverage proves the generic policy.

## Blast radius / cost
No per-frame road generation, road GameObjects, dense world masks, storage-width changes, or surface-vertex stride changes. Road work is deterministic planning/catalogue generation; each bounded physical piece budgets one primitive under `FeatureBudget`. Final player evidence must still quantify generated definitions/primitives, build/runtime residency health, and visible streaming/LOD continuity.

## Remaining gates
Resolve both failed behavioral invariants and update their regressions, refresh/merge current master, then obtain green exact-SHA focused CI through the assigned CI ref. Run the repository built-application harness against `Assets/Scenes/KentridgePlayableSlice.unity` and inspect required road/shoulder/vegetation/LOD evidence. Only then complete pending metadata, move open→pending→closed, merge current master again, and non-force push the exact feature head to master.
