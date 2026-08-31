# Plan

## Acceptance
- CI deterministically maps production diffs to owning modules, focused tests, player-visible module validation targets, and the canonical built-player Kentridge gate.
- Module validation scenes/scenarios are module-owned; scene and scenario are distinct metadata.
- One generic standalone-player harness executes all visual targets; no test-name/scene-name feature policy remains in shared infrastructure.
- Water demonstrates the migration and automatic discovery path.
- Exact-SHA validation fails closed on missing/zero-match/skipped required targets and remains practical for routine iteration.

## Results / selected approach
- Reused the existing standalone-player build/capture path; added declarative `*.module-validation.json`, separate `*.player-scenario.json`, and diff-driven planning with shared/core expansion and Kentridge fallback. An independent Structures fixture proves reuse without planner changes.
- Required focused tests and player targets fail closed on missing/zero-match/skipped/failed tests, missing scene/scenario, failed required/forbidden log assertions, or insufficient captures.
- Repeated Water visual failures were isolated rather than papered over: production near-field Water is owned by `VoxelRenderPass`/`CpuWaterSurfaceChunkCache`, not `VoxelFarTerrain`; the tableau now uses authoritative storage and the normal production renderer. Camera world-scale, debug tint, and scene lighting defects were separately fixed.
- Corrected exact-SHA run `33359614839` used request commit `982ed7a15b532c9d708f26ef0365274557e71645` directly on feature SHA `75861901785acf25955064e2c77a215594b2848d`. Focused Water passed; automatic planning selected exactly `water` + `kentridge-integration`; both standalone players and artifact upload passed. Cost: 11.4s focused Water + 46.5s Water player + 105.11s Kentridge = 163.0s automatic path.
- Water's scenario requires both `WATER_VALIDATION ready` and `WATER_VALIDATION liquid-ready`. The exact player reported `resident=37, dirty=0, visible=5, completed=37`, proving production liquid authoring/build/publication and view intersection. Direct inspection of all three exact standalone frames still classifies the tableau `unacceptable`: intended liquid is visually absent and gray substrate/tiled surfaces dominate, so green readiness is not visual acceptance.
- Root-cause isolation has falsified: null/unbound Water shader, selection of the NoWater renderer, Water/Cascade entering opaque solid extraction, stale camera scale, and readiness/capture timing. `VoxelURPAsset` selects the Water-enabled renderer; its Water shader GUID is correct; `CpuTransvoxelChunkCache.IsSolidSurfaceMaterial` explicitly excludes Water/Cascade.
- Next discriminating experiment: inspect `WaterBrickMeshBatchJob` emission (position/elevation, winding/normals, material IDs, face visibility) and compare emitted liquid faces with authored bed/water geometry. Do not make another presentation tweak until this production draw-path cause is isolated.

## Blast radius / cost
CI/orchestration, validation assets/tests/docs, composition-owned Water tableau/probes, and semantic rendering diagnostics. No authoritative gameplay behavior changed. Verified automatic Water + Kentridge path costs 163.0s.

## Current commit
Feature head before this documentation refresh: `75861901785acf25955064e2c77a215594b2848d`.

## Remaining gates
- [x] Implement generic metadata/discovery, fail-closed execution, Water migration, docs, and independent reuse proof.
- [x] Rerun exact-SHA CI demonstrating automatic Water + Kentridge flow and record cost/evidence.
- [ ] Isolate/fix the production Water draw defect and visually accept exact standalone Water content at production-quality.
- [ ] Review all 18 acceptance criteria, complete metadata, open -> closed, merge current master, and promote exact head.
