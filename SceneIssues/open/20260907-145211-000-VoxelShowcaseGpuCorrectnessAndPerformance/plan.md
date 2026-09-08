# VoxelShowcase GPU correctness and performance handoff

## Observed state and acceptance

This continuation owns remaining GPU renderer correctness, full-scene quality, performance, presentation migration and lifetime/budget work after PR #316. Correctness gates optimization. Never reduce content, distance, quality, device budgets or integer CPU world truth to pass.

Exact-SHA request `fd092c40e8d19ddc5f67d7a1f2165ae74f865864` / run `34155859120` completed the real cold build and 180-second production VoxelShowcase replay on Apple M4 Max/Metal. The 14,336 MB cold-build guard is validated. The player still ended with `missingVisible=63`; the mixed mirror saturated at `40270/40270`, `NoSlot=130550`, and one fine request waited about 151.7 seconds. Page allocation failures and pressure evictions were zero. Late whole-frame windows degraded from early ~500–600 FPS to roughly 150–220 FPS as backlog accumulated.

## Proven first invariant and corrected repair

H1 source-admission starvation is supported. Pre-fix steps 1/2/4 retained unrestricted whole-source `RequestCoverage` leases. Exact red request `41cd5e3b07c7d5a9d8c4af87dbd1c360a7191fe4` / run `34163251595` compiled and ran four cases: steps 1/2/4 failed `Expected: 0, But was: 1`; step 8 passed.

The first edit-watch-only repair (`0c65f67b4e1ef1518923fb24a19eb4c8170eb707`) is rejected before green CI because fine prepared-cache entries retain packed live mixed-slot references; evicting those sources before count/write risks stale-slot reuse.

The corrected repair preserves immutable exact leases but gates admission by the actual shared mirror slot count. With the repository's 96 MB minimum layout (`40270` mixed slots), worst-case reservations are step 1 `10^3=1000`, step 2 `18^3=5832`, step 4 `34^3=39304`, yielding up to 8/6/1 owners respectively under the global eight-chain cap. Step 8 is one exclusive HLOD turn with edit watch plus bounded copied-summary slices and no whole mixed-slot reservation. Different LOD modes do not overlap; once another LOD waits, the active mode drains rather than refills. Cancelled waiters are pruned. No capacity, quality, distance or content budget changes. See experiments 003–004.

Exact request `90b77cf525b74b4ba5dd0e8a2e6c38ac4312c4c2` / run `34173008582` proved the focused ownership path and standalone replay could run, but automatic Rendering validation failed 5 tests. Three failures directly exposed coordinator demand left at 1 after context release; the leak then contaminated bounded step-4 and water publication. Root cause: `GpuSurfaceSourceAdmission.Release` returned before releasing explicit coordinator coverage when a production-faithful queue path already held coverage without admission ownership. Commit `9bbf41d00d7b3976ec378832ec19abf4ec7b5f4c` separates coverage lifetime from owner accounting; `676709da2c66225d10dd9b5c31b08b712cf0b85f` adds the focused regression. See experiment 006.

## Remaining gates

Run the repaired focused regression on exact SHA, then Rendering/SolidGpu and full VoxelShowcase. Require bounded service and no persistent occupied-visible holes before optimization. Then matched stationary/traversal benchmarks and Metal trace; the current task target is 400 FPS at 1920x1080 scale 1 on M4 Max/Metal, with historical 1,000 FPS retained only as context until final coordinator reconciliation. Finish Water presentation migration, device budgets/lifetime cycles, canonical Kentridge integration, exact-SHA validation, closure bookkeeping, current-master merge, PR and auto-merge per `tasks.md`.
