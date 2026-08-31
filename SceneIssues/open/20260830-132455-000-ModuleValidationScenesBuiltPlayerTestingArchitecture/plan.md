# Plan

## Acceptance
- CI deterministically maps production diffs to owning modules, focused tests, player-visible module validation targets, and the canonical built-player Kentridge gate.
- Module validation scenes/scenarios are module-owned; scene and scenario are distinct metadata.
- One generic standalone-player harness executes all visual targets; no test-name/scene-name feature policy remains in shared infrastructure.
- Water demonstrates the migration and automatic discovery path.
- Exact-SHA validation fails closed on missing/zero-match/skipped required targets and remains practical for routine iteration.

## Results / selected approach
- Reused the existing standalone-player build/capture path; added declarative `*.module-validation.json`, separate `*.player-scenario.json`, and diff-driven planning with shared/core expansion and Kentridge fallback. An independent Structures fixture proves reuse without planner changes.
- Required focused tests/player targets fail closed on missing/zero-match/skipped/failed tests, missing scene/scenario, failed required/forbidden logs, or insufficient captures.
- Water visual failures were isolated instead of hidden: production Water belongs to the near-field `VoxelRenderPass`/`CpuWaterSurfaceChunkCache`, not the far clipmap. Camera scale, tint, and scene lighting were corrected separately.
- Root production draw defect: Water indices are chunk-local but vertices occupy nonzero shared-arena leases. Water draw submission now supplies semantic `_SurfaceVertexBase = _liveLease.VertexStart`; the shader adds it only when dereferencing local indices.
- Correct exact-source run `33362755513` proved that fix: Water became visible while focused tests, automatic Water + Kentridge players, previews, and artifact upload passed.
- Visible Water still failed production-quality as broad floating/cutting slabs. First scene-local correction `d48c5c2315c96b7a1d9cc33f662c267634c8b6d1` added immediate beds and stepped edge shoulders. Exact run `33367217621` passed focused regression plus automatic Water + Kentridge validation, but direct frames still showed the same blockout symptom; grounding improved contact without fixing oversized slab composition, and the first capture remained blank while the large tableau converged.
- Second materially different correction `582f290a09dbb8b2b45d12cbb500f209ae079247` reduces authored footprint and vertical exaggeration, narrows/shortens the river and cascade, compacts both pools, softens apron relief, removes the decorative stone slab, reduces near-ring scope, and reframes the three shots. Shared Water rendering/API policy is unchanged.
- If exact standalone evidence after this second correction still shows the same slab/blockout symptom, stop presentation changes and isolate a minimal repro/root cause before another fix.

## Blast radius / cost
CI/orchestration, validation assets/tests/docs, composition-owned Water tableau/probes, semantic Water vertex addressing, and scene-local Water validation composition. Gameplay/storage truth is unchanged. Verified automatic Water + Kentridge path costs 163.0s.

## Current commit
Scaled-tableau implementation begins at `582f290a09dbb8b2b45d12cbb500f209ae079247`; task/plan evidence commits follow on the same feature branch.

## Remaining gates
- [x] Implement generic metadata/discovery, fail-closed execution, Water migration, docs, and independent reuse proof.
- [x] Demonstrate automatic focused Water -> built-player Water -> built-player Kentridge flow and record cost/evidence.
- [x] Isolate and fix the production Water shared-arena vertex-addressing defect.
- [ ] Run exact-SHA CI for the scaled tableau and visually accept still pool, shallow shoreline, river flow, cascade, receiving pool, and terrain contacts as production-quality; if the same symptom remains, isolate minimal repro/root cause before another fix.
- [ ] Review all 18 acceptance criteria, complete metadata, move open -> closed, merge current master, rerun required exact-head gates, and promote exact head.
