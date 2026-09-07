# VoxelShowcase GPU correctness and performance handoff

## Observed state and acceptance

This continuation owns remaining GPU renderer correctness, full-scene quality, performance, presentation migration and lifetime/budget work after PR #316. Correctness gates optimization. Never reduce content, distance, quality, device budgets or integer CPU world truth to pass.

Exact-SHA request `fd092c40e8d19ddc5f67d7a1f2165ae74f865864` / run `34155859120` completed the real cold build and 180-second production VoxelShowcase replay on Apple M4 Max/Metal. The 14,336 MB cold-build guard is validated. The player still ended with `missingVisible=63`; the shared mixed mirror saturated at `40270/40270`, `NoSlot` reached `130550`, and one fine request waited about 151.7 seconds. Page allocation failures and pressure evictions were zero. Late whole-frame windows degraded from early ~500–600 FPS to roughly 150–220 FPS as backlog accumulated.

## Proven first invariant and corrected repair

H1 source-admission starvation is supported. Pre-fix steps 1/2/4 retained unrestricted whole-source `RequestCoverage` leases, while reclamation refuses every demanded/active block. Exact red request `41cd5e3b07c7d5a9d8c4af87dbd1c360a7191fe4` / run `34163251595` compiled and executed four requested cases: steps 1/2/4 failed `Expected: 0, But was: 1`; step 8 passed.

The first edit-watch-only repair (`0c65f67b4e1ef1518923fb24a19eb4c8170eb707`) is rejected before green CI. Fine prepared-cache entries retain packed live mixed-slot references, so making earlier prepared portions evictable before count/write could cause stale-slot reuse.

Corrected repair bounds immutable source admission instead: waiting requests own no source/extraction lease; steps 1/2 admit at most two same-step exact owners; step 4 admits one exact owner; step 8 admits one HLOD owner with edit watch plus bounded copied-summary slices; different steps do not overlap. Newly acquired leases are released on downstream admission failure and otherwise live through normal publication/cancellation cleanup. No capacity, quality, distance or content budget changes. See experiments 003–004.

## Remaining gates

Run the corrected focused regression on exact SHA, then Rendering/SolidGpu and full VoxelShowcase. Require bounded service and no persistent occupied-visible holes before optimization. Then matched stationary/traversal benchmarks and Metal trace; the current task target is 400 FPS at 1920x1080 scale 1 on M4 Max/Metal, with historical 1,000 FPS retained only as context until final coordinator reconciliation. Finish Water presentation migration, device budgets/lifetime cycles, canonical Kentridge integration, exact-SHA validation, closure bookkeeping, current-master merge, PR and auto-merge per `tasks.md`.
