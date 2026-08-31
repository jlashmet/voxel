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
- Run `33339810737` was green end to end on exact feature SHA `c7ce42cd107ac908bf619f4a3521868afa1ed35f`; direct Water review still rejected far-field clipmap gaps and fallback presentation.
- Repeated visual-symptom minimal repro established the production-path error: `VoxelFarTerrain` is a distant clipmap, while `VoxelRenderPass` owns production near-field Water/Cascade through `CpuWaterSurfaceChunkCache`. The validation composition now binds authoritative storage through `RenderingWorldBinding` and the normal production near renderer; no reflection/frozen clipmap proxy remains.
- Exact-SHA run `33340598083` exposed an illegal direct `Game.Materials.Runtime` dependency. Water now obtains simulation definitions and installs presentation through the existing semantic `Game.Composition.Materials.GameMaterialComposition` facade.
- Exact-SHA run `33342127118` passed focused Water regression, automatic planning, Water player readiness, and Kentridge. It selected exactly `water` + `kentridge-integration` and measured 162.08s total (10.78s focused test, 46.61s Water player, 104.70s Kentridge player).
- A first scene-only framing repair kept the same production renderer. Exact-SHA run `33343070296` remained green and measured 168.20s total, but direct review still rejected the atlas-like overview because required Water behaviors were visually compressed.
- A materially different three-shot camera-tour approach was then tested in exact-SHA run `33343722288`. The focused test and automatic planner passed, Kentridge passed, but Water failed closed before readiness with `visible=0, missing=0`. Minimal repro/root cause: the new shot coordinates mistakenly used unscaled authored-cell coordinates, while the working renderer/camera uses voxel world scale (0.1m per cell plus region origin). The camera therefore moved outside the resident Water surfaces. The narrow repair converts all three scene-policy shots to the same scaled world coordinate convention and leaves camera movement observable so the renderer can update.
- Exact-SHA run `33344561645` validated the scaled camera tour at feature SHA `23a21e1daaa034162d76c71c5bdf663e74627ebb`: focused Water passed, automatic planning selected exactly Water + Kentridge, both standalone players passed readiness, and the automatic flow measured 168.01s total (11.74s focused Water, 47.78s Water player, 108.49s Kentridge player). Direct inspection still rejected the Water screenshots. Frame 0 at 2.2s is pre-readiness (readiness logs immediately afterward), while frames 1 and 2 expose the intended geometry but are severely green/magenta/cyan over-saturated.
- The first presentation defect had a concrete production-composition cause: `RenderingComposition.ConfigureEnvironment` calls its first parameter `surfaceDebugTint`, but the Water tableau supplied `(1.2, 1.16, 1.08)` as if it were a light colour. Repair `e048943ad55834e491c6d00491215b523d97d657` changed only that scene-owned presentation value to neutral `Color.white`.
- Exact-SHA run `33345825163` passed the focused Water test, automatic module validation, real-player gates, and artifact upload after the neutral-tint repair. Direct artifact review rejected the tableau again, this time for materially underlit/low-contrast presentation rather than false colour.
- A second discriminating comparison isolated the fixture lighting defect: Water's scene-owned key-light direction had a strongly negative Y component (`-0.93`) while established readable production presentation uses a positive-Y light vector. Repair `c4fdf7d7e596e935f1e63e64175ef0a252d7fe17` changes only Water validation light direction plus modest background/ambient values; geometry, production renderer semantics, discovery metadata, generic player harness, and camera tour remain unchanged. Targeted transport run `33352798832` is queued for direct built-player review. Do not add Water-specific capture-delay policy merely to suppress the early screenshot; visual acceptance depends on the later production-ready captures being good enough on their own.

## Blast radius / cost
CI/orchestration, validation assets, tests, docs, and one composition-assembly reference only; no authoritative gameplay runtime behavior changed. The latest successful automatic Water + Kentridge flow measured 168.01 seconds. Current visual work remains confined to Water validation composition and existing production rendering/composition APIs; the latest repair changes only scene-owned Water validation lighting/background/ambient presentation.

## Current commit
Water validation lighting repair: `c4fdf7d7e596e935f1e63e64175ef0a252d7fe17` (documentation evidence follows on `fixes/agent-8`).

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
