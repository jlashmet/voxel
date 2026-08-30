# Plan — WorldBuilder Road Network Integration

## Acceptance
Promote Kentridge organic roads into a reusable WorldBuilder contract: semantic intent/provenance first, deterministic terrain-aware resolution, one continuous influence for voxel grading/surface transition/ecology, reusable keep-clearance, and a bounded physical representation. Kentridge is the proving consumer; the generic module must not depend on Kentridge. Final proof remains the built `KentridgePlayableSlice` application with endpoint continuity, walkability, natural shoulders/vegetation recovery, and no chunk/LOD/runtime failure.

## Discrimination result
The representation gap is resolved. `Game.WorldBuilder.Api` owns reusable road profile/intent/resolution/influence/network data; Kentridge `SettlementPlan.Routes` and macro hard connections author it. Physical lowering is one bounded `EmitTerrainCorridor` primitive per piece, not historical carve/core/ten-strip shoulders. `TerrainCorridorRasteriser` grades destructible terrain and persists the same 0..31 road influence used by semantic/ecology consumers.

CI `33281599556` exposed two real defects: incoherent per-sample edge hashes and immutable authored vegetation anchors colliding with seeded organic buildings. Semantic and physical influence now use the same deterministic 64dm bilinear variation at the nearest centerline point; blocked non-residential vegetation anchors use bounded deterministic <=120dm relocation while clear anchors remain exact.

## Terrain flags
The generic resolver supports Blocked/Water/Reserved/Pass flags and crossing policy. Current production terrain/Kentridge/top-down route sources provide no authoritative water/reserved/barrier map, so adapters return `None` rather than fabricate crossings; deterministic fixtures prove the generic policy.

## Blast radius / cost
Current feature-only diff against master is 23 files, limited to WorldBuilder road integration, voxel-structure support, EditMode regressions, and this assignment metadata. Lowering enforces `FeatureBudget` definition/footprint caps and emits exactly one primitive/explicit placement per bounded piece. `Primitive` gained no fields, so primitive NativeArray stride is unchanged. No road GameObjects, dense world masks, or per-frame generation path were added. Coherent edge variation is integer-only query/raster work; blocked authored-anchor relocation is planning-only and bounded to 48 candidates. Dynamic player residency/runtime remains unproven.

## Gate result / blocker
Final focused request source is `b5cac79f1ff4f289d643edeef3019e4c1d75a806`; CI request `b75fefa2d2022274cd5c810e08fb577264ee1c4e`, run `33284733815`, passed all 7 `KentridgeRoadShoulderRegressionTests` with Unity peak RSS 5119 MB. The request was EditMode, so built-player capture correctly skipped. Repository policy requires a green exact-source built application/player launch before pending promotion, and blocked work stays open. This session has no local Unity and the assignment forbids creating an extra CI transport or replacing the completed final targeted request. Remaining work is therefore the exact-scene built-player validation/evidence (endpoint traversal, uneven shoulders, vegetation recovery, chunk/LOD continuity, runtime exceptions, and player cost). Do not promote, close, or push to master until that gate is legitimately satisfied.
