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
- Correct exact-source run `33362755513` used CI request `13ff0be529199b2555d3baca3446cfa4ff8cfe6c` directly on feature `66d031dd3fc34d9b0c26c57861a2c5d6da478bb3`. Focused tests, automatic Water + Kentridge players, previews, and artifact upload all passed. Direct frame review confirms Water is now visible, validating the vertex-base root cause.
- Those same frames remain `prototype/blockout quality`: broad translucent liquid sheets visibly float/cut across terrain and cascade/river contacts read as overlapping planes. Static composition inspection explains it: river water is `BaseY + 5..7` while the apron is around `BaseY - 5`, with only low banks; cascade has no immediate bed/edge support.
- Selected scene-local correction: author sand/stone directly beneath river/cascade liquid and stepped stone edge shoulders flush with the water surface. This changes only validation-tableau composition, not shared Water APIs/policy.
- Exact request `d2a93fc275a85699a0209ea5cddba3206435a37b` was admitted as run `33367217621`, directly on feature `a8cdbc45093e6aa252aa55f186271ced87e888c6`. It remains queued with no self-hosted macOS runner assigned while repository Actions has multiple queued jobs and zero in-progress jobs. Runner availability is the external blocker; do not replace the queued run.

## Blast radius / cost
CI/orchestration, validation assets/tests/docs, composition-owned Water tableau/probes, semantic Water vertex addressing, and the demonstrated scene-local grounding correction. Gameplay/storage truth is unchanged. Verified automatic Water + Kentridge path costs 163.0s.

## Current commit
Grounding implementation begins at `d48c5c2315c96b7a1d9cc33f662c267634c8b6d1`; blocker/evidence notes follow on the same feature branch.

## Remaining gates
- [x] Implement generic metadata/discovery, fail-closed execution, Water migration, docs, and independent reuse proof.
- [x] Rerun exact-SHA CI demonstrating automatic Water + Kentridge flow and record cost/evidence.
- [x] Isolate and fix the production Water shared-arena vertex-addressing defect.
- [ ] Let queued run `33367217621` execute without replacement, inspect its exact standalone Water frames, then rerun only if later feature-head changes require a new exact-SHA gate.
- [ ] Review all 18 acceptance criteria, complete metadata, open -> closed, merge current master, and promote exact head after green production-quality evidence.
