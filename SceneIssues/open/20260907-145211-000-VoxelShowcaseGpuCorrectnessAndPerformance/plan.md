# VoxelShowcase GPU correctness and performance handoff

## Observed state and acceptance

This continuation owns remaining GPU correctness, full-scene quality, performance, presentation migration and lifetime/budget work after PR #316. Correctness gates optimization. Never reduce content, distance, quality, device budgets or deterministic integer CPU world truth to pass.

Baseline request `fd092c40e8d19ddc5f67d7a1f2165ae74f865864` / run `34155859120` completed the cold build and 180-second VoxelShowcase replay on Apple M4 Max/Metal. It ended `missingVisible=63`, mixed mirror `40270/40270`, `NoSlot=130550`, oldest fine request ~151.7 seconds, zero page-allocation failures/pressure evictions, and late ~150–220 FPS versus early ~500–600 FPS.

## Proven repairs and module-gate diagnosis

H1 source/admission starvation is supported. Fine requests now capacity-bound immutable ownership against the 40,270-slot mirror: step 1 `1000`, step 2 `5832`, step 4 `39304`; step 8 is exclusive HLOD with copied bounded summaries. Different LOD modes drain before switching. No content/distance/quality/budget changed.

Run `34173008582` exposed a coordinator-demand leak. `9bbf41d00d7b3976ec378832ec19abf4ec7b5f4c` repaired coverage lifetime and `676709da2c66225d10dd9b5c31b08b712cf0b85f` added regression coverage. Later exact runs reduced Rendering validation to the legacy step-4 publication timeout.

Experiments 008/009 stopped speculative product changes and reduced the symptom. Run `34194247464` proves shared-context lifetime is not causal: the matched two-context diagnostic passes in 0.034 seconds (`maxPrepareMs=4.446`) while the legacy first full batch takes 14.448 seconds and emits 640 `VoxelBrickMesher` Editor/Metal shader warnings; following real step-1/2 publications take 0.029/0.026 seconds with no mesher warnings.

The attempted module-global warm-up on source `f51184569f55ddc954e63e5915e816fecb6a9b2b`, request `e296a25c4cf526f52e9d35bb1ce356aa79b1dd7d`, run `34197667062` is rejected: its one-time setup never observed summary submission and caused 229 setup-derived failures, while standalone VoxelShowcase still passed.

Current master `b56436f198702fa91bfaccec69a30df25a52a2cd` already contains the correct narrow test boundary: after the real lane first reaches `Submitted`, the unchanged five-second asynchronous publication window begins, excluding only synchronous cold Editor/Metal submission compilation. Merge `292c2768258e89b19f006bc6b737e278ff5c6d5a` integrates that master and removes the invalid warm-up files while preserving agent-3's production repairs and diagnostics.

## Remaining gates

Exact-validate the merged step-4 publication path, then return to annotated castle/traversal cell-to-draw correctness, owning Rendering players plus full VoxelShowcase, matched stationary/traversal benchmarks and Metal trace, Water presentation migration, device budgets/lifetime cycles, canonical Kentridge integration, exact-SHA closure bookkeeping, final current-master reconciliation, PR and auto-merge. Numeric task target remains 400 FPS at 1920x1080 scale 1 on M4 Max/Metal; historical 1,000 FPS remains context until coordinator reconciliation.
