# Plan

## Observed behavior and acceptance
`VoxelShowcase` must deliver a natural walkable mouth, prolonged organic descent, huge irregular cavern with varied geology, reachable aged ruin, exactly two grounded readable statues, sparse supported torch/lantern lighting, and deep darkness. Closure requires focused regression, exact built-player traversal through normal movement/collision/streaming, direct AAA visual review, and bounded cost.

`SceneIssues/feature-readme.md` is absent; `AGENTS.md`, `SceneIssues/README.md`, the assignment contract, and `quality-review.md` are authoritative.

## Latest evidence and discriminated hypotheses
Request `77001787` / run `33272245282` is functionally green: focused PlayMode passed and the built player completed waypoint 38/38 with zero harness assertions. Metrics were 30,351,634 writes, 3,589,665 visual-finish writes, 20 preloaded regions, 8 lights, and 2 statues. Direct review of all eight 1600x900 frames still fails the visual gate: most of the descent reads as a long rectangular, beam-lined corridor; cavern scale/geology never reads clearly; the final frame is dominated by a flat wall/opening instead of a readable ruin/statue composition.

The camera/cadence-only hypothesis is rejected because the same rectangular envelope persists across ~50 seconds of moving-player evidence and the generic cave core explicitly carves rectangular cross-sections. The route also ended at the ruin centre, which forced the final player view into/through the structure rather than presenting its facade.

The first visual-repair request `c0b16843` / run `33274260594` was queued on feature SHA `8be989de`. Subsequent source review found that SHA was not final: route naturalization was ordered after supported route lantern geometry and could erase fixture voxels, and a 132-voxel ruin offset separated the older semantic statue bases from the articulated finish. That request is diagnostic only and must not be used as a completion gate.

## Selected fix
Keep the generic cave core unchanged for blast-radius safety. Add reusable full-route naturalization that carves deterministic overlapping irregular void nodes along the primary descent while skipping dogleg host windows, preserving route grade and forced turns. Run naturalization before `UndergroundCavernTraversalEnhancement` so doglegs, mouth treatment, and supported route lantern fixtures are authored afterward and remain intact.

Use the existing reusable ruin config to move the production landmark near the far wall with `RuinForwardOffset=112`, which keeps the current semantic statue bases aligned with the articulated facade finish while satisfying the reusable far-end layout check. Use `UndergroundCavernDestinationLayout.ResolveRuinApproach` for the final gameplay waypoint so the moving player stops outside the facade with viewing setback instead of entering the ruin centre.

Keep motor limits, renderer/light semantics, the eight-light cap, preload envelope, and the 55,000,000-write device budget unchanged. Production regression requires >=150 naturalization nodes, bounded naturalization writes, far-end ruin placement, a facade-viewing endpoint, and normal CharacterMotor completion.

## Remaining gates
Wait for the stale queued request to finish without touching its CI ref. Then create one fresh exact-SHA request from the final feature head, require focused PlayMode plus built-player traversal/capture green, inspect every useful frame for daylight mouth -> varied descent -> huge irregular cavern/formations -> aged ruin/exactly two statues, record exact cost evidence, and only then move this assignment through pending/closed and promote the exact validated head non-force.