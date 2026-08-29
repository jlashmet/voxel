# Plan

## Observed gap / acceptance
- `captures` is empty, so the feature note is the complete repro/acceptance contract and there are no marked poses to omit.
- Baseline was semantic graph -> deterministic grid placement -> terrain-grounded road paint + neutral node markers. It proved topology but not a physical regional world.
- Closure requires physical settlement presence for all six settlements, continuous terrain-aware hard routes, reusable geographic-region intent/constraints, a substantial lake + ridge, geography-constrained routing, real CharacterMotor traversal, durable built-player evidence, and measured cost.

## Evidence / discriminated hypotheses
1. **Larger markers and more legacy road tiles were enough.** Rejected: the legacy catalogue had no reusable water/barrier relationship model, no crossing/pass semantics, and no generic settlement blockout plan, so it could not reject or solve blocked routes.
2. **The existing source-backed macro graph should remain authoritative, with a reusable physical-plan layer between semantic layout and voxel emission.** Selected and implemented. `TopDownWorldLayout` still owns topology/provenance; new shared region/route/settlement intent is planned deterministically, then emitted as bounded WorldBuilder features.
3. **The physical implementation might be disconnected from the exact playable scene.** Rejected by static production trace: the playable compatibility `KentridgeDefinition.Build(seed)` calls `TopDownWorldLayoutSelection.Select(...)` before `KentridgeCombinedVoxelCatalogue.Build(...)`, and the catalogue consumes that selection once. The final regression additionally asserts macro content survives the combined production catalogue.
4. **A water-colored terrain patch was enough for a lake.** Rejected during review: surface repaint alone supplied no physical depth. A separate reusable streamed water-body pass now carves a bounded basin and fills it with the engine's existing non-solid water material.

## Implemented direction
- Reusable macro-region vocabulary covers water body, mountain/ridge, valley/pass, meadow, woodland, and generic regions; authored extents/elevation, deterministic variation, relationships, blocking/crossing semantics, and placement/routing queries remain above voxel code.
- Physical planner keeps verified hard topology authoritative, routes around/through geography only through authored semantic solutions, rejects a blocked hard route with no solution, and plans four non-overlapping generic building plots for unrealized settlements.
- Kentridge/Hightown remain owned by their richer generators. Moordell, Rossdam, Fairy Village, and Orc Village receive generic streamed street/building blockouts.
- Voxel emission uses ordinary bounded feature definitions/explicit placements so normal region clipping/LOD owns residency; no remote scene hierarchy or second graph is introduced.
- Rossdam water is a bounded carved basin (authored depth 45 dm, clamped by reusable water-body rules) filled with material role `Water`; the existing renderer treats configured water material 11 as non-solid presentation.
- Exact Kentridge road output is guarded by a stricter acceptance assertion of <=6 voxels rise per 30 dm route step, in addition to generic planner obstacle solving.

## Regression / cost gate
Final single CI test:
`VoxelEngine.Tests.PlayMode.KentridgeMacroWorldPhysicalProductionAcceptanceTests.PhysicalMacroWorldHasWalkableRoutesAndADeepStreamedWaterBody`

It nests the complete deterministic macro-realization regression, then adds strict actual-road rise checks, physical water-program/depth checks, and Select -> combined-production-catalogue assertions for roads, remote settlements, ridge, and carved water. The test logs route count, tile count, settlement/building count, geography-constrained route count, route-solve steps, maximum road rise, and water depth for the cost record.

Static cost shape before runtime evidence: one-shot macro selection only; roughly region definitions + 20 hard-route definitions + four generic street definitions + 16 generic building definitions, plus one water-basin definition/placement with two primitives. Every footprint remains under the existing 1280-voxel feature cap; no device budget is changed. Exact counts and built-player CPU/GPU/memory/streaming evidence remain CI gates.

## Remaining workflow gates
1. Freeze/refetch current master and final feature diff; no `.github/test-request.json` on feature.
2. Create exactly one final PlayMode CI request on `ci-test/fixes/agent-6`, targeting the wrapper regression and this SceneIssue so the same run also builds/replays `KentridgePlayableSlice`.
3. Inspect exact-SHA test logs plus built-player logs/screenshots: four remote settlements, roads, lake, ridge/pass, constrained route, overview, and CharacterMotor traversal; reject visual/collision anomalies.
4. Record exact planning/build and CPU/GPU/memory/streaming/far-field cost against existing budgets. Complete every remaining `tasks.md` checkbox and acceptance criterion only with evidence.
5. After green gates, complete pending metadata and promote only this feature open -> pending -> closed per workflow; set `status=fixed`/`resolvedUtc` only at closure.
6. Refetch/merge current `origin/master` into `fixes/agent-6`, then non-force push that exact feature head to `origin/master`, retrying if master advances.
