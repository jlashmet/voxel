# Tasks

- [ ] Inspect the reopened issue, prior implementation, and every available visual/runtime evidence artifact for the mountain/dragon flow.
- [ ] Reproduce/discriminate stale startup bake versus runtime realization/render failure using production paths.
- [ ] Add any newly discovered required work here before implementation.
- [ ] Implement the smallest reusable WorldBuilder/shared-layer fix; keep VoxelShowcase composition-only.
- [ ] Add/update focused behavioral regression(s) through the production WorldBuilder path for mountain/path, grounded summit dragon, and normal proximity-triggered dialogue.
- [ ] Check blast radius across WorldBuilder/terrain/story consumers and quantify runtime/world-build cost against existing budgets.
- [ ] Validate the checked-in startup bake contains the accepted mountain.
- [ ] Run exact-SHA built-application VoxelShowcase validation with no startup/runtime exceptions.
- [ ] Traverse the complete route through normal player movement without jumps/teleportation/impassable intersections.
- [ ] Save durable built-application visual captures for approach landmark, path base, representative switchbacks, supported summit dragon, and proximity-triggered `Hello, I'm Mr. Dragon.` dialogue.
- [ ] Human-review the rendered captures and confirm AAA visual/spatial acceptance before pending promotion.
- [ ] Run final focused targeted CI on `ci-test/fixes/agent-4` only, against the exact feature SHA, and verify green `ci/single-test`.
- [ ] Complete pending metadata (`status`, `resolutionSummary`, `regressionTest`, `fixCommit`) and move only this assignment from `open/` to `pending/`.
- [ ] After all exact-SHA gates are green, set `status=fixed` and `resolvedUtc`, move only this assignment from `pending/` to `closed/`, merge current `origin/master`, and non-force push the exact feature head to `origin/master`.
