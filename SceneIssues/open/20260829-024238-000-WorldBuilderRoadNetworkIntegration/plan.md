# Plan — WorldBuilder Road Network Integration

## Acceptance
Promote Kentridge organic roads into a reusable WorldBuilder contract: semantic intent/provenance first, deterministic terrain-aware resolution, one continuous influence for voxel grading/surface transition/ecology, reusable keep-clearance, and a bounded physical representation. Kentridge is the proving consumer; the generic module must not depend on Kentridge. Final proof is the built `KentridgePlayableSlice` application with endpoint continuity, walkability, natural shoulders/vegetation recovery, and no chunk/LOD/runtime failure.

## Discrimination result
The representation gap is resolved. `Game.WorldBuilder.Api` owns reusable road profile/intent/resolution/influence/network data; Kentridge `SettlementPlan.Routes` and macro hard connections author it. Physical lowering is one bounded `EmitTerrainCorridor` primitive per piece, not historical carve/core/ten-strip shoulders. `TerrainCorridorRasteriser` grades destructible terrain and persists the same 0..31 road influence used by semantic/ecology consumers.

CI `33281599556` exposed two real defects: incoherent per-sample edge hashes and immutable authored vegetation anchors colliding with seeded organic buildings. Semantic and physical influence now use the same deterministic 64dm bilinear variation at the nearest centerline point; blocked non-residential vegetation anchors use bounded deterministic <=120dm relocation while clear anchors remain exact.

## Terrain flags
The generic resolver supports Blocked/Water/Reserved/Pass flags and crossing policy. Current production terrain/Kentridge/top-down route sources provide no authoritative water/reserved/barrier map, so adapters return `None` rather than fabricate crossings; deterministic fixtures prove the generic policy.

## Blast radius / cost
Current feature-only diff against master is limited to WorldBuilder road integration, voxel-structure support, EditMode regressions, and this assignment metadata. Lowering enforces `FeatureBudget` definition/footprint caps and emits one primitive/explicit placement per bounded piece. `Primitive` gained no fields, so NativeArray stride is unchanged. No road GameObjects, dense world masks, or per-frame generation path were added. Coherent edge variation is integer-only query/raster work; blocked authored-anchor relocation is planning-only and bounded to 48 candidates.

## Validation path
Focused EditMode request `33284733815` passed all 7 `KentridgeRoadShoulderRegressionTests` on production/test source `b5cac79f1ff4f289d643edeef3019e4c1d75a806` with Unity peak RSS 5119 MB. That request intentionally lacked scene replay and therefore did not run the built player. Follow-up investigation of a recently closed Kentridge feature confirmed the sanctioned full-app path uses the same `ci-test/fixes/agent-N` / `tests-single.yml` transport with `platform: PlayMode`, a focused PlayMode test, `scene_issue`, and `replay_seconds`; `scene_issue` makes the workflow build and launch the real player for the issue's exact `scenePath`. Use `KentridgePlayableScenePlayTests` plus this issue and a 60-second replay from the refreshed current feature SHA, then inspect its player log/screenshots/evidence before promotion. No extra branch/workflow/transport is required.
