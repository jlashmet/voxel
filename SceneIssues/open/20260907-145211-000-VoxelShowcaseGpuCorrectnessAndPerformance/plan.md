# VoxelShowcase GPU correctness and performance handoff

## Observed state and acceptance

This continuation owns remaining GPU correctness, full-scene quality, performance, presentation migration and lifetime/budget work after PR #316. Correctness gates optimization. Never reduce content, distance, quality, device budgets or deterministic integer CPU world truth to pass.

Baseline request `fd092c40e8d19ddc5f67d7a1f2165ae74f865864` / run `34155859120` completed the cold build and 180-second VoxelShowcase replay on Apple M4 Max/Metal. It ended `missingVisible=63`, mixed mirror `40270/40270`, `NoSlot=130550`, oldest fine request ~151.7 seconds, zero page-allocation failures/pressure evictions, and late ~150–220 FPS versus early ~500–600 FPS.

## Proven repair and current discriminant

H1 source/admission starvation is supported. Fine requests now capacity-bound immutable ownership against the 40,270-slot mirror: worst-case reservations step 1 `1000`, step 2 `5832`, step 4 `39304`; step 8 is exclusive HLOD with copied bounded summaries. Different LOD modes drain before switching. No content/distance/quality/budget changed.

Run `34173008582` exposed a separate coordinator-demand leak: admission release could return before releasing coverage held by production-faithful queue paths. `9bbf41d00d7b3976ec378832ec19abf4ec7b5f4c` repaired that lifetime and `676709da2c66225d10dd9b5c31b08b712cf0b85f` added regression coverage.

Run `34183296361` reduced Rendering validation to one failure: `FineStepFourPublishesRealMixedStorageThroughGpuReadiness`, while steps 1/2, step-4 edit/retirement and the standalone replay passed. Because the same symptom survived two materially different repairs, further speculative product fixes are prohibited.

Experiment 008 added a bounded step-4 production-path phase repro. Exact source `dbe6bd00bddf0666083cff2765362c2fad5e43d1`, request `42a49b66bbee3bcf4771029db927b42867d33551`, run `34188737907` ran Rendering 575 tests: 574 passed / 1 failed. The legacy step-4 case again failed after 15.386 seconds, but the diagnostic passed in 0.03 seconds. `ResetWorld` already clears queue/fairness/coverage/recovery state. The material remaining difference is the legacy fixture retaining a second shared-mirror context while replacing the first, versus the diagnostic's single consumer.

Experiment 009 changes only that diagnostic to match the two-context lifetime exactly and records maximum synchronous `PrepareFrame` duration plus summary/full-submission/fence state. If it fails, repair the demonstrated shared-context lifetime defect. If it passes, shared-context lifetime is falsified and isolate the repeatable first-full-publication cold cost before any product change or deadline adjustment.

## Remaining gates

Run the matched diagnostic on exact SHA and make only the causal repair it identifies. Then complete annotated castle/traversal cell-to-draw correctness, owning Rendering players plus VoxelShowcase, matched stationary/traversal benchmarks and Metal trace, Water presentation migration, device budgets/lifetime cycles, canonical Kentridge integration, exact-SHA closure bookkeeping, current-master merge, PR and auto-merge. Numeric task target remains 400 FPS at 1920x1080 scale 1 on M4 Max/Metal; historical 1,000 FPS remains context until coordinator reconciliation.
