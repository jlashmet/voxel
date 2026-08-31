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
- Water's scenario requires both `WATER_VALIDATION ready` and `WATER_VALIDATION liquid-ready`. The exact player reported `resident=37, dirty=0, visible=5, completed=37`, proving production liquid authoring/build/publication and view intersection. Direct inspection of all three exact standalone frames still classified the pre-fix tableau `unacceptable`: intended liquid was visually absent and gray substrate/tiled surfaces dominated, so green readiness was not visual acceptance.
- Root-cause isolation falsified null/unbound Water shader, selection of the NoWater renderer, Water/Cascade entering opaque solid extraction, stale camera scale, readiness/capture timing, and Water mesh face winding/material emission.
- The production draw defect is now isolated: `WaterBrickMeshBatchJob` emits chunk-local vertex indices, while `SurfaceGeometryArena.UploadVertices` stores a chunk's vertices beginning at `SurfaceGeometryLease.VertexStart`. Water draw submission supplied the arena `IndexStart`, but `WaterSurface.shader` dereferenced the local index without adding the lease's vertex base. Thus publication/visibility could be correct while any nonzero arena lease fetched unrelated vertices.
- Minimal fix: Water `Entry.Draw` now supplies semantic `_SurfaceVertexBase = _liveLease.VertexStart`, and the Water shader adds that base only when dereferencing its local index. This keeps the generic arena contract and solid metadata path unchanged; no scene/camera/lighting policy was added to shared rendering.
- Green run `33362258628` did not validate this fix and is intentionally excluded from exact-SHA evidence. The resolver logged `SOURCE_SHA=982ed7a15b532c9d708f26ef0365274557e71645`: targeted CI derives source from the request commit's parent, and the existing `ci-test/fixes/agent-8` request had stale ancestry. Its changed-path plan omitted both production Water rendering files, explaining the unchanged stale screenshots without invalidating the isolated fix.

## Blast radius / cost
CI/orchestration, validation assets/tests/docs, composition-owned Water tableau/probes, semantic rendering diagnostics, and the demonstrated Water shared-arena addressing defect. The rendering correction is limited to Water draw addressing; authoritative gameplay/storage behavior is unchanged. Verified automatic Water + Kentridge path costs 163.0s.

## Current commit
Implementation fix: `536411b75d76c638999223986dcbc766c7fb0178` + `012302b32db9a6e1cd9c5059c2c6b0d044cfaba2`; durable blocker/evidence notes follow on the same feature branch.

## Remaining gates
- [x] Implement generic metadata/discovery, fail-closed execution, Water migration, docs, and independent reuse proof.
- [x] Rerun exact-SHA CI demonstrating automatic Water + Kentridge flow and record cost/evidence.
- [x] Isolate the production Water draw defect and implement the minimal shared-arena vertex-base correction.
- [ ] Rebase the same targeted-CI transport onto the exact current feature SHA, verify the resolver's logged `SOURCE_SHA`, then visually accept exact standalone Water content at production-quality.
- [ ] Review all 18 acceptance criteria, complete metadata, open -> closed, merge current master, and promote exact head.
