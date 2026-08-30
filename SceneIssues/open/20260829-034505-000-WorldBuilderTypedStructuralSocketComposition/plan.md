# Plan

## Observed behavior / acceptance
- The canonical production path is `FeatureDefinition` + typed `SlotSpec` + `ShapeOp.CallSlot` + `FeatureCatalogue` + descendant-aware `FeatureRegionBuild`; adding a second WorldBuilder structural solver would duplicate authoritative generation state.
- Core composition, validation, deterministic graph identity, bounded recursion/cost, authoritative descendant rasterization, support metadata, and decoration handoff are implemented with focused regressions.
- The remaining acceptance is exact-scene proof: bridge, castle, cliff settlement, and facade/roof variants must exist in `WorldbuildingGalleryShowcase`, survive both Generate and stale Bake startup, traverse through production `CharacterMotor`, produce inspectable frames, and report bounded planning/raster/render proxies.

## Hypotheses / results
1. **Complete the existing `CallSlot` path** — confirmed by catalogue ownership, definition-id rebasing, bytecode execution, region generation, and structural regressions.
2. **Add a parallel solver** — rejected because it would create competing save/network/world-generation identity.
3. **Scene proof is missing from production startup** — resolved: the gallery authors four bounded structural catalogues and runs a bounded post-startup presence/repair pass after either Bake or Generate.
4. **A separate validation harness is needed** — rejected: the existing gallery audit is extended only for this SceneIssue id and now checks structural metrics, negative cases, three production-`CharacterMotor` traversals, eight frames, and cost proxies.

## Selected fix / current state
- Four production proving cases are authored through the same typed catalogue/planner/raster path: monumental multi-region bridge, wall/tower/gatehouse castle, terrain-supported multi-level cliff chain, and two facade/roof styles.
- `WorldbuildingGalleryShowcase` calls the bounded structural ensure step after normal startup so stale bake content cannot omit the proof district.
- `WorldbuildingGalleryAuditHarness` scopes structural validation to this exact assignment and records traversal, visual, planning, region, voxel, memory, and render-proxy evidence.
- Focused PlayMode regression added at `7a38304e1cfac21ebb60d614aa0f502b170ac5af`; it plans all four production catalogues twice, compares deterministic graph/cost/bounds, and verifies required negative attachment reasons.

## Remaining gates / blast radius
- Refresh against `origin/master`, review assignment-only diff, then submit exactly one final PlayMode request via `ci-test/fixes/agent-5` with this SceneIssue and built-player replay.
- Inspect exact-SHA test/log/artifact evidence, including all eight full-resolution structural frames and bridge/castle cost data. Do not weaken global budgets.
- Only after green exact-SHA regression + built-app validation: complete pending metadata, move open -> pending, then pending -> closed with `status=fixed` and `resolvedUtc`, merge current master, and non-force push the exact feature head to master.
