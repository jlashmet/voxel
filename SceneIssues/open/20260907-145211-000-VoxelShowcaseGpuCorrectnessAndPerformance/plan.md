# VoxelShowcase GPU correctness and performance handoff

## Observed state and acceptance

This continuation owns remaining GPU renderer correctness, full-scene quality, performance, presentation migration and lifetime/budget work after PR #316. Correctness gates optimization. Never reduce content, distance, quality, device budgets or integer CPU world truth to pass.

Exact-SHA request `fd092c40e8d19ddc5f67d7a1f2165ae74f865864` / run `34155859120` completed the real cold build and 180-second production VoxelShowcase replay on Apple M4 Max/Metal. The narrow 14,336 MB cold-build guard is therefore validated. The player still ended with `missingVisible=63`; the shared source mirror saturated at `40270/40270`, `NoSlot` reached `130550`, and one fine request waited about 151.7 seconds. Page allocation failures and pressure evictions were zero. Late whole-frame windows also degraded from early ~500–600 FPS to roughly 150–220 FPS while this backlog accumulated. See experiment 003.

## Proven first invariant and repair

H1 is supported: fine requests retained whole source neighbourhoods through `RequestCoverage` while the newer count-batch path already owns source residency in bounded `RequestSourceRange` preparation slices. Mirror reclamation protects all demanded blocks, so overlapping requests can pin essentially the entire shared mirror and prevent the recovery slices they need from making progress. Missing persistent-directory source can resolve as air, explaining occupied-visible holes.

H2 premature pressure retirement was falsified: victim selection excludes protected/current replacement state and host acknowledgment validates live handle/generation. Clipmap rediscovery and GPU cutover for steps 1/2/4/8 were also traced and do not explain the baseline.

Selected repair: every admitted step keeps only a whole-request edit/version watch; source residency belongs solely to bounded preparation slices and submitted GPU readers. Product commit `0c65f67b4e1ef1518923fb24a19eb4c8170eb707`. A real-context red regression is queued on exact pre-fix source and must remain untouched until complete.

## Remaining gates

After red→green evidence, replay Rendering/SolidGpu and full VoxelShowcase. Require bounded source service and no persistent occupied-visible holes before performance work. Then run matched stationary/traversal benchmarks and Metal trace; numeric signoff remains blocked until the task's 400-versus-historical-1,000-FPS reconciliation is explicit. Finish Water presentation migration, device budgets/lifetime cycles, canonical Kentridge integration, exact-SHA validation, closure bookkeeping, current-master merge, PR and auto-merge per `tasks.md`.
