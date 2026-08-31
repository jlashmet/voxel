# Plan

## Acceptance
- Production diffs deterministically select owning modules, focused tests, module-local built-player validation, and canonical `KentridgePlayableSlice` integration.
- Module scene/scenario metadata is declarative and separate; one generic player harness executes it fail-closed.
- Water proves migration/reuse with production rendering and production-quality standalone-player evidence.
- Required missing/zero-match/skipped/failed targets fail validation and routine targeted cost remains practical.

## Results / selected approach
- Implemented repository-driven `*.module-validation.json`, separate player scenarios, shared/core expansion, mandatory Kentridge integration, generic evidence windows, and an independent Structures fixture.
- Production Water defects were isolated rather than hidden: shared-arena vertex addressing, upward-face topology, and presentation-only vertex displacement now have focused regression coverage owned by Water metadata.
- Exact run `33375145205` proved automatic focused Water -> built-player Water -> built-player Kentridge at 179.82s total, about 10.3% above the earlier 163.0s path.
- After repeated broad planar-slab failures, the renderer minimal repro proved greedy top-face merging prevented geometric waves; the production topology/shader fix addressed that root cause.
- Later scene failures isolated one-level river/cascade authoring and then authoring-budget exhaustion. `cf18ba6f75a450c9b3fb01387c8e4e38754fb832` reused `FillColumnBulk` for buried terrain while preserving visible tops and the unchanged 180,000 slow-write budget.
- Exact request `65c7dd24101fcf926ff740eb729bd6247708d78c` / run `33385476451` passed requested regression, automatic module planning, Water + Kentridge built-player gates, previews, artifact upload, and final status.
- Direct review of all retained Water frames (`t=8.2`, `14.2`, `20.2`, `26.2`) rejected production quality: narrow engineered levee/trench, abrupt walls, rectangular cascade seams, and analytic pool cuts.
- Composition correction `39d7f99bdd9e3d22ba72561edd13123f5817eb95` broadened/graded banks, varied width, irregularized pool boundaries, and staggered cascade grades. Exact request `agent-8-water-terrain-integration-4e1f366` / run `33388147850` passed all automated gates for feature `4e1f3662c0bb9f71fa779e67007c7cb62fc5d4ff`.
- Direct review of every retained Water frame from run `33388147850` still rejects production quality: the composition remains a flat test pad with broad analytic sand rims, and the channel still reads as authored above/against the surrounding terrain rather than carved through it.
- Carved-terrain correction `392e59be377c48f50544b4160fddbb11f90932b8` was exercised by exact transport run `33390924383`; requested focused regression, automatic module planning, Water player, Kentridge player, previews/artifact upload, and final status all completed successfully.
- Reuse/root-cause review after that run found a stronger boundary than another scene-local visual tweak: agent-9 owns the production `WaterRenderingShowcase` and its semantic Water/river/waterfall composition. Its feature branch currently contains a minimal top-level scene whose only scene policy is attaching `VoxelEngine.Showcase.WaterRenderingShowcase`, plus a portable production-path regression (`PortableShowcaseWorldAuthorsIndependentWaterProfilesThroughCanonicalStorage`). Maintaining an independently authored Agent-8 Water tableau would duplicate showcase policy and is the wrong reuse boundary.
- Selected integration approach: once agent-9's Water work reaches `master`, replace Agent-8's bespoke module tableau with a thin module-local validation scene beneath `Assets/VoxelEngine/Rendering/Validation` that attaches the same canonical `WaterRenderingShowcase` composition component, keep scenario/capture policy in the module-owned JSON, and point Water focused metadata at semantic production regressions rather than PlayMode visual proof. This preserves acceptance (14)'s module-local scene while reusing the production showcase implementation instead of forking it.

## External prerequisite / blocker
- As of the latest fetch, `master` is still `2edf4c2e151492f67c4a1c1b846a9b7948284aba`; agent-9's Water showcase remains on `fixes/agent-9` and is not available to Agent-8 through `origin/master` yet. Do not copy/cherry-pick another assignment or continue polishing the duplicate Agent-8 tableau. Continue only independent architecture/audit work until the canonical Water composition lands, then merge current master through the required closure flow and adapt module-local validation to reuse it.

## Blast radius / cost
CI/orchestration, validation assets/tests/docs, and the Water validation adapter/metadata. Water renderer corrections already isolated by behavioral regressions remain in scope only where acceptance/correctness requires them. The next Water scene change is intended to remove duplicated scene-specific policy by reusing the canonical production showcase composition; no authoritative simulation/collision behavior change is planned.

## Current commit
Feature head before this bookkeeping was `36d598669f02ad23310d55bd62258c5a803ad3b9`; exact transport run `33390924383` completed successfully for its direct-parent feature content. This bookkeeping intentionally advances the feature head, so another exact-head gate will be required before closure.

## Remaining gates
- [x] Generic module discovery/execution, fail-closed behavior, Water migration plumbing, documentation, and independent reuse proof.
- [x] Automatic Water -> Kentridge exact-SHA path and cost evidence.
- [x] Isolate/fix Water vertex addressing and planar-top renderer root causes with behavioral regressions.
- [x] Generic post-readiness evidence-window validation.
- [ ] After canonical `WaterRenderingShowcase` is present on `master`, reuse it from the module-local Water validation scene and select semantic focused Water regressions without PlayMode visual-acceptance semantics.
- [ ] Run exact-head CI for that reuse integration and inspect every retained Water standalone frame as production-quality evidence.
- [ ] Review all 18 acceptance criteria; only then complete metadata, move open -> closed, merge current master, revalidate exact head as required, and promote non-force.
