# Tasks

- [ ] Inspect the reopened issue, prior implementation, and every available visual/runtime evidence artifact for the mountain/dragon flow, including prior mountain-target CI artifacts.
- [x] Reproduce/discriminate stale startup bake versus runtime realization/render failure using production paths: checked-in `ShowcaseWorld.bytes` predates the mountain landing (Aug 25 vs Aug 28).
- [x] Add discovered required work here before implementation.
- [ ] Fix `ShowcaseWorldBaker.BakeShowcaseWorld` so offline showcase baking always uses `ShowcaseStartupSource.Generate`, matching the gallery baker instead of defaulting to `Bake` and risking stale-image reuse.
- [ ] Add a regression that fails if the showcase baker can select the baked startup source and proves the generated/captured startup image contains mountain/path/dragon occupancy through production WorldBuilder realization.
- [ ] Recover/regenerate or safely refresh the checked-in VoxelShowcase startup bake from current authored WorldBuilder content without creating another CI transport.
- [ ] Prove startup-bake provenance/content with a focused regression so a stale shipped bake cannot silently omit newly authored landmarks again.
- [ ] Implement the smallest reusable WorldBuilder/shared-layer or evidence-navigation fix needed; keep VoxelShowcase composition-only.
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
