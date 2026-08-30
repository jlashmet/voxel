# Plan — WorldBuilder Road Network Integration

## Acceptance
Promote Kentridge organic roads into a reusable WorldBuilder contract: semantic intent/provenance first, deterministic terrain-aware resolution, one continuous influence for voxel grading/surface transition/ecology, reusable keep-clearance, and a bounded physical representation. Kentridge is the proving consumer; the generic module must not depend on Kentridge. Final proof is the built `KentridgePlayableSlice` application with endpoint continuity, walkability, natural shoulders/vegetation recovery, and no chunk/LOD/runtime failure.

## Discrimination result
The representation gap is resolved. `Game.WorldBuilder.Api` owns reusable road profile/intent/resolution/influence/network data; Kentridge `SettlementPlan.Routes` and macro hard connections author it. Physical lowering is one bounded `EmitTerrainCorridor` primitive per piece, not historical carve/core/ten-strip shoulders. `TerrainCorridorRasteriser` grades destructible terrain and persists the same 0..31 road influence used by semantic/ecology consumers.

Earlier CI `33281599556` exposed incoherent per-sample edge hashes and immutable authored vegetation anchors colliding with seeded organic buildings; those were repaired with coherent 64dm edge variation and bounded deterministic anchor relocation. Focused EditMode run `33284733815` then passed all 7 road regressions.

## Full-player discriminator
The sanctioned combined request is the same `ci-test/fixes/agent-1` transport with `PlayMode`, `KentridgePlayableScenePlayTests`, this issue's `scene_issue`, and a 60-second replay. Run `33285741354` proved the harness works: the real player built successfully, ran 60 seconds, captured four screenshots, and exited 0. Its canonical PlayMode acceptance nevertheless failed on an in-scope startup path: `WorldRoadNetworkVoxelCatalogue.Build` attempted to construct `FixedString64Bytes` from `world-road-macro:overworld-moordell->overworld-to-rossdam-s0p0`. The helper incorrectly budgeted 63 characters; `FixedString64Bytes` stores at most 61 UTF-8 content bytes. Repair feature-name truncation against the actual Unity capacity, preserve the segment/piece suffix, and add a regression using the observed macro ID before rerunning the combined gate. The diagnostic player capture cannot satisfy closure because the PlayMode half of the same exact-source gate failed.

## Terrain flags
The generic resolver supports Blocked/Water/Reserved/Pass flags and crossing policy. Current production terrain/Kentridge/top-down route sources provide no authoritative water/reserved/barrier map, so adapters return `None` rather than fabricate crossings; deterministic fixtures prove the generic policy.

## Blast radius / cost
Lowering enforces `FeatureBudget` definition/footprint caps and emits one primitive/explicit placement per bounded piece. `Primitive` gained no fields, so NativeArray stride is unchanged. No road GameObjects, dense world masks, or per-frame generation path were added. Coherent edge variation is integer-only query/raster work; blocked authored-anchor relocation is planning-only and bounded to 48 candidates. The name repair changes only managed catalogue naming and adds no runtime geometry/residency cost. Preserve the coordinator's `SceneIssues/README.md` clarification already appended to this feature branch.

## Remaining gates
Implement and regress the `FixedString64Bytes` capacity repair, refresh current master, then rerun the same combined PlayMode + 60-second exact-scene player request from the repaired exact feature SHA. Inspect test logs, player log, screenshot evidence, road continuity/shoulders/vegetation/LOD, and dynamic cost before promotion. Only then complete metadata, move open→pending→closed, merge current master again, and push the exact feature head non-force to master.
