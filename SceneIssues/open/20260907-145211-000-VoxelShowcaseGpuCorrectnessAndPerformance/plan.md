# VoxelShowcase GPU correctness and performance handoff

## Observed state and acceptance

This continuation owns remaining GPU renderer correctness, full-scene quality, performance, presentation migration and lifetime/budget work after PR #316. Correctness gates optimization. Never reduce content, distance, quality, device budgets or deterministic integer CPU world truth to pass.

Baseline request `fd092c40e8d19ddc5f67d7a1f2165ae74f865864` / run `34155859120` completed the real cold build and 180-second VoxelShowcase replay on Apple M4 Max/Metal. It ended with `missingVisible=63`, mixed mirror `40270/40270`, `NoSlot=130550`, oldest fine request ~151.7 seconds, zero page-allocation failures/pressure evictions, and late ~150–220 FPS versus early ~500–600 FPS.

## Proven repair and remaining correctness defect

H1 source/admission starvation is supported. Fine steps previously retained unrestricted exact source leases. The selected repair capacity-bounds immutable ownership against the actual 40,270-slot shared mirror: worst-case reservations are step 1 `1000`, step 2 `5832`, step 4 `39304`; step 8 is exclusive HLOD with copied bounded summaries. Different LOD modes drain before switching. No content, distance, quality or device budget changed.

Request `90b77cf525b74b4ba5dd0e8a2e6c38ac4312c4c2` / run `34173008582` then exposed a separate coordinator-demand leak: `GpuSurfaceSourceAdmission.Release` returned before releasing coverage held by production-faithful queue paths. `9bbf41d00d7b3976ec378832ec19abf4ec7b5f4c` repaired lifetime separation and `676709da2c66225d10dd9b5c31b08b712cf0b85f` added regression coverage.

Exact source `cacb7b0b1858bc00f7b04e19e5a4e61227b8808a`, request `9f1f4ee42f7389066bced55f30aa34d998673a81`, run `34183296361` proves that leak is fixed: Rendering EditMode is now 573 passed / 1 failed. The sole failure is `FineStepFourPublishesRealMixedStorageThroughGpuReadiness`; step 1/2 publication and step-4 edit/retirement cases pass, and the 180-second VoxelShowcase phase completes. The step-4 lane reaches real summary submission but not paged completion within the existing five-second bound.

Because the same step-4 symptom survived two materially different repairs, no further speculative renderer patch is allowed. Experiment 008 adds a bounded module-local production-path repro that reports summary/recovery, submission/outcome, fence, arena-wait and source-demand state without changing behavior or deadline. Diagnostic implementation is through `53813fb7a5c35074d7c25ab3d22dd815e5c987bd`; this plan update advances the feature head.

## Remaining gates

Run the focused step-4 phase repro on its exact SHA and make only the causal repair it identifies. Then finish annotated castle/traversal reproduction, bounded cell-to-draw diagnostics, owning Rendering players plus full VoxelShowcase correctness, matched stationary/traversal benchmarks and Metal trace, Water presentation migration, device budgets/lifetime cycles, canonical Kentridge integration, exact-SHA closure bookkeeping, current-master merge, PR and auto-merge. The current numeric task target remains 400 FPS at 1920x1080 scale 1 on M4 Max/Metal; historical 1,000 FPS stays context until coordinator reconciliation.
