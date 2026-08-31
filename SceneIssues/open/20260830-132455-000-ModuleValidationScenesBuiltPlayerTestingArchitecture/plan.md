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
- Selected next correction `392e59be377c48f50544b4160fddbb11f90932b8` is still scene-local but materially changes the relationship: terrain apron heights are carved down toward pool basins and the meandering channel, river banks become a narrow shoreline rather than a raised embankment, the receiving pool is lower than the upstream water with a real stepped cascade, and evidence cameras move closer to shoreline/flow/contact. Renderer, authoritative Water state/collision, generic harness, readiness, and acceptance policy remain unchanged.

## Blast radius / cost
CI/orchestration, validation assets/tests/docs, Water validation composition/probes, Water mesh addressing/topology, and presentation shader only. The latest correction is scene-local and uses existing semantic authoring APIs; no authoritative simulation/collision behavior changes.

## Current commit
Feature branch includes scene-local terrain-carving/evidence-composition correction `392e59be377c48f50544b4160fddbb11f90932b8`; plan/task bookkeeping follows before the next exact-head request.

## Remaining gates
- [x] Generic module discovery/execution, fail-closed behavior, Water migration, documentation, and independent reuse proof.
- [x] Automatic Water -> Kentridge exact-SHA path and cost evidence.
- [x] Isolate/fix Water vertex addressing and planar-top renderer root causes with behavioral regressions.
- [x] Generic post-readiness evidence-window validation.
- [ ] Run exact-SHA CI for the current carved-terrain composition correction and inspect every retained Water frame as production-quality evidence.
- [ ] Review all 18 acceptance criteria; only then complete metadata, move open -> closed, merge current master, revalidate exact head as required, and promote non-force.
