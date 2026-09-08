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

## Current correctness evidence and next discriminator

Experiment 012 preserves the fixed-camera castle failure from the same exact VoxelShowcase artifact: the terminal stationary frame still contains a severely incomplete castle shell, a large dark planar/triangular mass, bright cyan planar regions and detached/truncated geometry while `missingVisible` remains about 501 late in the replay.

Experiment 013 maps the late oldest step-2 source request `origin=int3(47,31,31) edge=18` to derived step-2 chunk `(3,2,2)` and its approximate world/draw bounds. It identifies source-mirror coverage acquisition as the first observed broken transition before geometry page allocation, but deliberately does not claim that this footprint is the exact missing-frustum candidate responsible for the captured hole.

Experiment 014 replays every available `RINGS` sample and tightens that discriminator. The oldest source footprint progresses only `(2,0,2) -> (3,0,2) -> (3,1,2) -> (3,2,2)`. The mirror first reaches exactly `40270/40270` mixed slots at ~17.9 seconds while the oldest footprint is `(3,1,2)`; from then through ~163 seconds mixed occupancy never falls below capacity, page-arena allocation failures remain zero, and mirror `noSlot` refusals rise from 51 to 138,954 while visible holes only slowly decline. This proves a persistent source-mirror liveness/capacity stall upstream of page allocation, but it still does not distinguish active coverage retention from recovery/inactive-residency lifetime and still does not identify the exact missing-frustum candidate.

The next non-blocked discriminator is therefore unchanged but narrower: preserve one exact missing-frustum `(step, chunk)` from the existing bounded GPU LOD-demand readback, then correlate that same coordinate with renderer-local desired generation, dirty/queue age, source admission/coverage state, extraction/count, page handle/publication generation, selected draw and bounds. Use GPU readback diagnostics only; do not consult CPU world state as a rendering oracle and do not change capacity, eviction, distance or quality policy until this first broken transition is demonstrated. Negative-coordinate traversal/return plus delayed-upload/edit/cancellation/bounded-pressure cases remain required independently.

## Remaining gates

After the exact candidate trace identifies and repairs the first in-scope break, independently complete the annotated castle stationary/traversal/return route under existing settle/convergence budgets, then complete full-scene correctness, matched 1920×1080 M4 Max/Metal stationary/traversal benchmarks plus separate Metal trace, Water GPU presentation migration, device budgets/lifetime cycles, and final canonical Kentridge/current-master exact-SHA validation before open→closed bookkeeping, PR and auto-merge. Numeric task target remains 400 FPS; historical 1,000 FPS remains context until coordinator reconciliation.
