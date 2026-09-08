# VoxelShowcase GPU correctness and performance handoff

## Observed state and acceptance

This continuation owns remaining GPU correctness, full-scene quality, performance, presentation migration and lifetime/budget work after PR #316. Correctness gates optimization. Never reduce content, distance, quality, device budgets or deterministic integer CPU world truth to pass.

Baseline request `fd092c40e8d19ddc5f67d7a1f2165ae74f865864` / run `34155859120` completed the cold build and 180-second VoxelShowcase replay on Apple M4 Max/Metal. It ended `missingVisible=63`, mixed mirror `40270/40270`, `NoSlot=130550`, oldest fine request ~151.7 seconds, zero page-allocation failures/pressure evictions, and late ~150–220 FPS versus early ~500–600 FPS.

## Proven repairs and module-gate diagnosis

H1 source/admission starvation is supported. Fine requests now capacity-bound immutable ownership against the 40,270-slot mirror: step 1 `1000`, step 2 `5832`, step 4 `39304`; step 8 is exclusive HLOD with copied bounded summaries. Different LOD modes drain before switching. No content/distance/quality/budget changed.

Run `34173008582` exposed a coordinator-demand leak. `9bbf41d00d7b3976ec378832ec19abf4ec7b5f4c` repaired coverage lifetime and `676709da2c66225d10dd9b5c31b08b712cf0b85f` added regression coverage. Experiments 008/009 reduced the remaining step-four symptom to synchronous cold Editor/Metal compilation rather than shared-context lifetime.

The attempted module-global warm-up on source `f51184569f55ddc954e63e5915e816fecb6a9b2b`, request `e296a25c4cf526f52e9d35bb1ce356aa79b1dd7d`, run `34197667062` is rejected: its setup caused 229 setup-derived failures while standalone VoxelShowcase passed.

Master `b56436f198702fa91bfaccec69a30df25a52a2cd` contains the narrow timing boundary: the unchanged five-second asynchronous window begins only after the real lane reaches `Submitted`. Merge `292c2768258e89b19f006bc6b737e278ff5c6d5a` integrated it and removed the invalid warm-up.

Exact request `00e166f7073b5c8871a45cd100bb49e5731ca1bd` / run `34202852688` validates source `909c9893bc0d491fd3676e87e2616da32e5d2933`. The requested `FineStepFourPublishesRealMixedStorageThroughGpuReadiness` test passed, persistent Rendering tests passed, all selected Rendering release players passed, and the 180-second standalone VoxelShowcase replay passed. The overall workflow failed only at mandatory canonical Kentridge/System24 integration: `WaitGameplayControl` timed out with gameplay ready/session running but `exitedPub=False` on `NetworkApproach`. Agent-3 has no owning Game/Kentridge production diff. Experiment 011 records the exact classification. Do not rerun unchanged and do not modify System24 here.

## Remaining gates

Next, independently reproduce the annotated castle stationary/traversal/return route and obtain the bounded cell/candidate → selected output → payload → draw causal trace under existing convergence budgets. Then complete full-scene correctness, matched 1920×1080 M4 Max/Metal stationary/traversal benchmarks plus separate Metal trace, Water GPU presentation migration, device budgets/lifetime cycles, and final canonical Kentridge/current-master exact-SHA validation before open→closed bookkeeping, PR and auto-merge. Numeric task target remains 400 FPS; historical 1,000 FPS remains context until coordinator reconciliation.
