# Tasks

- [ ] Trace current `VoxelShowcase` composition through WorldBuilder/shared authoring and identify the production owners for terrain/carving, subterranean traversal, formations, ruin/statue assembly, and local/underground lighting.
- [ ] Map all 14 acceptance criteria to existing capabilities or explicit reusable gaps; add any discovered required work below.
- [ ] Implement a natural, normally walkable terrain cave mouth through WorldBuilder/shared systems.
- [ ] Implement a substantial, gently descending, continuously traversable subterranean route with adequate clearance and organic variation.
- [ ] Implement a much larger connected cavern with high irregular ceiling, broad navigable floor, recess/rock variation, and unblocked route.
- [ ] Populate reusable geological formations including stalactites and multiple additional categories without blocking circulation.
- [ ] Implement/place a reachable aged/damaged stone ruin with believable structural construction and readable entrance.
- [ ] Place exactly two large aged stone statues, grounded on credible bases and framing the ruin entrance.
- [ ] Implement localized, physically supported torch navigation lighting while preserving deep underground darkness and excluding outdoor-strength/direct sunlight behavior.
- [ ] Preserve the reveal sequence: daylight mouth -> prolonged descent -> huge dark cavern -> ruin/statues destination.
- [ ] Add focused behavioral regressions through the production WorldBuilder path for connectivity/traversability, formations, ruin/statues, and underground/local-light semantics.
- [ ] Review blast radius for every shared-system change and test likely negative regressions.
- [ ] Quantify world-build/runtime cost: voxel/chunk work, triangle impact, draw/light/shadow cost, memory, and supported-device budget impact without weakening budgets.
- [ ] Run exact-SHA focused targeted CI on `ci-test/fixes/agent-3` only after production/test work is final.
- [ ] Run exact-SHA built-application `VoxelShowcase` harness and verify usable rendered scene with no startup/runtime exceptions.
- [ ] Validate every acceptance criterion from built-app evidence; record durable verification evidence beside the assignment.
- [ ] Complete `issue.json` pending metadata and move only this assignment `open` -> `pending` in a bookkeeping commit.
- [ ] After all required green exact-SHA gates, set `status=fixed`, `resolvedUtc`, move only this assignment `pending` -> `closed`, merge current master, and push exact feature head to master non-force.
