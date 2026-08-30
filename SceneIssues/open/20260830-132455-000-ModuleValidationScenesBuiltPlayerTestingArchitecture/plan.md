# Plan

## Acceptance
- CI deterministically maps production diffs to owning modules, focused tests, player-visible module validation targets, and the canonical built-player Kentridge gate.
- Module validation scenes/scenarios are module-owned; scene and scenario are distinct metadata.
- One generic standalone-player harness executes all visual targets; no test-name/scene-name feature policy remains in shared infrastructure.
- Water demonstrates the migration and automatic discovery path.
- Exact-SHA validation fails closed on missing/zero-match/skipped required targets and remains practical for routine iteration.

## Results / selected approach
- Reused the existing standalone-player build/capture path and removed feature/test-name policy from the shared harness instead of introducing a second harness.
- Added declarative `*.module-validation.json` ownership plus separate `*.player-scenario.json` scenario metadata and a diff-driven planner with dependent/fallback behavior.
- Targeted CI derives module tests/player targets from the exact feature diff and automatically adds Kentridge for production changes.
- Water is the representative player-visible migration; an independent Structures planner fixture proves metadata reuse without planner code changes.
- Required focused tests fail on missing/zero-match/skipped/failed cases; player validation fails on missing scene/scenario, failed execution, required/forbidden log assertions, or insufficient captures.
- Earlier exact-SHA runs isolated and fixed Kentridge PlayMode coupling, relative artifact paths, far-field coarse-lattice authoring, and a false inner-pool probe. Run `33337294126` then proved real `VoxelFarTerrain` meshes were captured, but direct review rejected rectangular masks/overhead framing as prototype blockout quality.
- The Water validation composition now keeps the production renderer unchanged while using irregular rolling terrain, an organic still-water shelf/pool, a wandering variable-width descending river, shaped rock banks/cascade contacts, and lower three-quarter framing.
- Run `33337814842` exposed a shallow-shoreline collision at `(208,112)` and motivated module-owned readiness assertions (`WATER_VALIDATION ready:` / `KENTRIDGE_WORLD_LAYOUT`) so blank captures cannot pass.
- Run `33338330606` confirmed the readiness assertions fail closed: focused Water + planner + built-player Kentridge passed, while Water failed because readiness was absent. The exact probe remained `expected 179/3, got 182/1`, falsifying the write-order hypothesis.
- Minimal root cause: structure authoring is additive by voxel height. The river-bank mask and still-pool shelf both include coarse column `(208,112)`; the bank leaves Stone at Y=182, so later Sand at Y=179 cannot become the captured surface. The repair makes the river-bank mask explicitly exclude the same ellipse used by the pool shelf, sharing one `IsInsideEllipse` predicate so semantic ownership is geometric rather than order-dependent.

## Blast radius / cost
CI/orchestration, validation assets, tests, and docs only; no authoritative gameplay runtime behavior changed. Earlier automatic Water + Kentridge validation measured 167.71 seconds. The current repair is confined to module-local Water composition plus its acceptance bookkeeping; shared renderer/harness behavior is unchanged.

## Current commit
Pool-shelf ownership checkpoint: `2038f1c5fa1a29f2297b403fbf3a49ea3da74b85`

## Remaining gates
- [x] Inspect current validation architecture and identify reusable harness boundary.
- [x] Implement metadata/discovery and shared/core fallback.
- [x] Migrate Water validation scene/scenario.
- [x] Remove generic feature-specific inference and update docs/workflow semantics.
- [x] Add automated regression coverage, including fail-closed execution and integration-only fallback behavior.
- [ ] Upgrade and visually accept representative production Water validation content.
- [ ] Rerun exact-SHA CI demonstrating automatic complete flow and record final cost/evidence.
- [ ] Review all 18 acceptance criteria, complete metadata, open -> closed, merge current master, and promote exact head.
