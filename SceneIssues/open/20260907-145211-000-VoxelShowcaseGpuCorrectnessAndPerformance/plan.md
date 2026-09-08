# VoxelShowcase GPU correctness and performance handoff

## Observed state and acceptance

This continuation owns remaining GPU correctness, full-scene quality, performance, presentation migration and lifetime/budget work after PR #316. Correctness gates optimization. Never reduce content, distance, quality, device budgets or deterministic integer CPU world truth to pass.

Baseline request `fd092c40e8d19ddc5f67d7a1f2165ae74f865864` / run `34155859120` completed the cold build and 180-second VoxelShowcase replay on Apple M4 Max/Metal. It ended `missingVisible=63`, mixed mirror `40270/40270`, `NoSlot=130550`, oldest fine request ~151.7 seconds, zero page-allocation failures/pressure evictions, and late ~150–220 FPS versus early ~500–600 FPS.

## Proven repairs and module-gate diagnosis

H1 source/admission starvation is supported. Fine requests now capacity-bound immutable ownership against the 40,270-slot mirror: step 1 `1000`, step 2 `5832`, step 4 `39304`; step 8 is exclusive HLOD with copied bounded summaries. Different LOD modes drain before switching. No content/distance/quality/budget changed.

Run `34173008582` exposed a separate coordinator-demand leak. `9bbf41d00d7b3976ec378832ec19abf4ec7b5f4c` repaired coverage lifetime and `676709da2c66225d10dd9b5c31b08b712cf0b85f` added regression coverage.

Runs `34183296361` and `34188737907` reduced Rendering validation to one legacy step-4 publication failure. Because it survived two materially different fixes, experiments 008/009 stopped speculative product changes and reduced to a production-path repro.

Exact source `e03d7b60ff14eaffe3940c23d7ce158a228e6ed1`, request `152b97d3365a05117b848f456fb6d4f46b4de009`, run `34194247464` proves shared-context lifetime is not the cause: the matched two-context diagnostic passes in 0.034 seconds (`maxPrepareMs=4.446`) while the legacy case alone takes 14.448 seconds. That first successful full batch emits 640 `VoxelBrickMesher` Editor/Metal shader warnings; following real step-1/2 publications take 0.029/0.026 seconds with zero mesher warnings. Experiment 010 therefore isolates the module red gate to one-time editor shader compilation being charged to a steady-state five-second liveness assertion.

Test-only setup through `893b08c8de345e1af22991bf7202fa215073f844` warms the exact mixed step-4 production batch once under a separate bounded cold-compiler setup ceiling. Existing five-second transaction deadlines and all runtime behavior remain unchanged.

## Remaining gates

Validate the warm-up on exact SHA, then return to annotated castle/traversal cell-to-draw correctness, owning Rendering players plus full VoxelShowcase, matched stationary/traversal benchmarks and Metal trace, Water presentation migration, device budgets/lifetime cycles, canonical Kentridge integration, exact-SHA closure bookkeeping, current-master merge, PR and auto-merge. Numeric task target remains 400 FPS at 1920x1080 scale 1 on M4 Max/Metal; historical 1,000 FPS remains context until coordinator reconciliation.
