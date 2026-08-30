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
- Earlier exact-SHA runs isolated and fixed Kentridge PlayMode coupling, relative artifact paths, far-field coarse-lattice authoring, a false inner-pool probe, and additive-height bank/shelf overlap. Module-owned readiness assertions prevent early-aborted players from passing on screenshot count alone.
- Run `33339810737` was green end to end on exact feature SHA `c7ce42cd107ac908bf619f4a3521868afa1ed35f`: focused Water regression, diff planner, built-player Water, readiness assertion, and built-player Kentridge all executed automatically. Direct Water frame review still rejected the visual as prototype/blockout quality because of clipmap ring gaps, a detached strip, coarse banks, and smeared fallback water.
- Repeated visual-symptom minimal repro established the production-path error rather than another composition defect: `VoxelFarTerrain` explicitly renders terrain beyond the voxel streaming radius as concentric clipmap rings, and its hole is intentionally governed by `RenderingComposition.HasCompletePublishedNearSurfaceCoverage`. `VoxelRenderPass` separately owns the production near-field liquid path through `CpuWaterSurfaceChunkCache` for Water/Cascade. Freezing `_ringMeshes` and destroying `VoxelFarTerrain` therefore bypassed the normal near/far ownership contract and could never be valid near-field Water proof.
- Selected fix: keep shared rendering unchanged and repair only the module validation composition. The Water scene now authors dense authoritative terrain/liquid cells, installs the game material simulation/presentation definitions, publishes resident storage, binds that storage through `RenderingWorldBinding`, enables the normal production renderer with far-field disabled for this local tableau, and waits for published near-surface coverage before emitting `WATER_VALIDATION ready:`. Reflection/frozen clipmap meshes and scene-only proxy rendering are removed.
- Exact-SHA run `33340598083` failed during Unity compilation before module planning because the validation source directly imported `Game.Materials.Runtime`, which is not a legal dependency of its compilation boundary. The fix keeps that boundary intact: Water now obtains simulation definitions and installs presentation through the existing `Game.Composition.Materials.GameMaterialComposition` facade, and the Showcase composition assembly references that semantic composition assembly rather than adding a new runtime dependency from scene policy.
- Exact-SHA run `33342127118` then passed focused Water regression, automatic diff planning, built-player Water with readiness assertion, and built-player Kentridge. The plan selected exactly `water` plus `kentridge-integration`; total automatic module-validation time was 162.08s (10.78s focused test, 46.61s Water player, 104.70s Kentridge player).
- Direct review of that run confirmed the original rendering-path defects are gone, but rejected the remaining presentation as a different composition defect: the wide camera exposes the entire artificial terrain apron as a hard-edged island and an oversized foreground pool dominates the frame. The next repair is intentionally scene-only: reduce validation-pool footprints and tighten/lower framing while keeping the same production Water renderer and readiness contract.

## Blast radius / cost
CI/orchestration, validation assets, tests, docs, and one composition-assembly reference only; no authoritative gameplay runtime behavior changed. The latest automatic Water + Kentridge flow measured 162.08 seconds. Current visual polish remains confined to Water validation composition and uses existing production rendering/composition APIs.

## Current commit
Water visual-framing checkpoint: `a82e02258e69e0de8471d5a29ec71d3f9040c6aa`

## Remaining gates
- [x] Inspect current validation architecture and identify reusable harness boundary.
- [x] Implement metadata/discovery and shared/core fallback.
- [x] Migrate Water validation scene/scenario.
- [x] Remove generic feature-specific inference and update docs/workflow semantics.
- [x] Add automated regression coverage, including fail-closed execution and integration-only fallback behavior.
- [x] Isolate repeated Water visual symptom to the incorrect far-field-only proof path.
- [ ] Upgrade and visually accept representative production Water validation content.
- [ ] Rerun exact-SHA CI demonstrating automatic complete flow and record final cost/evidence.
- [ ] Review all 18 acceptance criteria, complete metadata, open -> closed, merge current master, and promote exact head.
