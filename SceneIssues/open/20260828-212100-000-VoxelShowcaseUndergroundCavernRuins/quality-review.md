# Quality review — 2026-08-29

The current `fixes/agent-3` bookkeeping must **not** be promoted or merged as a completed SceneIssue yet.

- The branch moved this assignment to `pending` while its own `resolutionSummary` says it is still **awaiting exact-SHA CI and rendered acceptance evidence**. That contradicts the SceneIssues workflow: `pending` is only appropriate after the required exact-SHA targeted CI and exact-SHA built-application acceptance gate are green.
- Acceptance criterion (12) requires the exact built application to **traverse from the surface into the cavern and to the ruin** and capture the entrance, descent, cavern reveal, formations, ruin/statues, and lighting. A staged camera sequence or camera retargeting is not a substitute for production-gameplay traversal of the full route.
- Do not narrow or retarget the evidence harness merely to make the gate pass. The harness must prove the authored experience using normal production movement/traversal and the original acceptance criteria.

Required before promotion: keep/revert the assignment to `open`, obtain green exact-SHA targeted CI, obtain green exact-SHA built-player traversal/rendered evidence for the complete route and required visual stages, inspect that evidence against the AAA world-authoring bar, and only then set pending/closure metadata.

## Exact-run review — run 33274618946

The exact targeted CI is functionally green: focused PlayMode passes and the standalone `VoxelShowcase` reaches cavern route waypoint 38/38 through normal production movement/collision. It is **not** visually acceptable and remains non-promotable.

- Presented frames through `t=94.5s` include the late destination approach and post-completion view, so the earlier timing-only hypothesis is rejected as the sole cause.
- The late frames are dominated by block/masonry-textured dark walls and a narrow architectural corridor silhouette. They do not establish a huge natural irregular cavern or readable mineral/geological formations.
- The final post-completion frame shows only a cropped entrance/arch composition; two large grounded humanoid statues do not read as the required flanking focal pair.
- Source inspection explains the mismatch: the production cave palette currently assigns `GameMaterialIds.DarkStone` to `CaveMaterialPalette.Rock`, so every deep host shell, cave wall, cavern envelope, geological shoulder, and naturalization carve presents with the same masonry-like dark-stone material used architecturally. Geometry counters therefore cannot substitute for rendered geological readability.

Required repair: keep production traversal, budgets, and acceptance thresholds unchanged; use an actually geological cave-host material/presentation for the natural cavern/route while retaining dark/masonry materials for the aged ruin and exactly two statues, then obtain fresh exact-SHA built-player evidence and directly inspect the complete sequence.
