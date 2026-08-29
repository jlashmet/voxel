# Plan

## Observed behavior and acceptance
`VoxelShowcase` must deliver a natural walkable mouth, prolonged organic descent, huge irregular cavern with varied geology, reachable aged ruin, exactly two grounded readable statues, sparse supported torch/lantern lighting, and deep darkness. Closure requires focused regression, exact built-player traversal through normal movement/collision/streaming, direct AAA visual review, and bounded cost.

`SceneIssues/feature-readme.md` is absent; `AGENTS.md`, `SceneIssues/README.md`, the assignment contract, and `quality-review.md` are authoritative.

## Latest evidence and hypotheses
Final request `77001787` / run `33272245282` is functionally green: focused PlayMode passed and the built player completed waypoint 38/38 with zero harness assertions. Metrics were 30,351,634 writes, 3,589,665 visual-finish writes, 20 preloaded regions, 8 lights, and 2 statues.

Direct review of all eight 1600x900 frames still fails the visual gate. Most of the descent reads as a long rectangular, beam-lined masonry corridor; the cavern scale/geology never reads clearly; the final frame is dominated by a flat tan wall/opening and does not clearly present the aged ruin with both statues.

The camera/cadence-only hypothesis is rejected: the same rectangular envelope persists across ~50 seconds of moving-player evidence, and the production cave core explicitly carves rectangular cross-sections. Sparse mouth/dogleg cylinders cannot naturalize the majority of the route. A second source defect compounds the destination evidence: the semantic final waypoint is the ruin centre, so the player finishes looking through/into the structure instead of approaching its facade.

## Selected fix
Keep the generic cave core unchanged for blast-radius safety. Extend the reusable `UndergroundCavernTraversalEnhancement` profile with bounded full-route naturalization: deterministic overlapping irregular void nodes along the primary descent, with configurable spacing, lateral jitter, radius, and ceiling variation while preserving the existing walkable core and grade. Expose/verify a production naturalization-node metric and keep the total feature under the existing 55M-write budget.

Change the reusable production route output to stop at a derived ruin-front approach point rather than the ruin centre, preserving normal gameplay traversal while making the destination composition readable. Do not change motor limits, light cap, renderer semantics, or existing device budgets.

## Remaining gates
Implement/regress both fixes, request one fresh exact-SHA CI on `ci-test/fixes/agent-3`, require focused PlayMode plus built-player traversal/capture green, inspect every useful frame for the full reveal/AAA bar, record final cost evidence, then move only this assignment through pending/closed and non-force promote the exact validated head.