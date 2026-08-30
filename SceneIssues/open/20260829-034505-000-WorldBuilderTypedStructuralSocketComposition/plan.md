# Plan

## Observed behavior / acceptance
- The canonical production path is `FeatureDefinition` + typed `SlotSpec` + `ShapeOp.CallSlot` + `FeatureCatalogue` + descendant-aware `FeatureRegionBuild`; a parallel structural solver would duplicate authoritative world identity.
- Core composition, validation, deterministic graph/hash identity, bounded recursion/cost, authoritative descendant rasterization, support metadata, decoration handoff, and four gallery proving catalogues are implemented with focused regressions.
- Remaining acceptance is exact-scene proof in built `WorldbuildingGalleryShowcase`: bridge, castle, cliff settlement, and facade/roof variants must render cleanly, production `CharacterMotor` must traverse bridge/gate/vertical routes, and cost/region/memory evidence must remain bounded.

## Hypotheses / material results
1. **Complete existing `CallSlot` path** — confirmed by catalogue ownership, bytecode execution, planner output, and descendant region rasterization.
2. **Add a parallel solver** — rejected because it would create competing generation/save/network identity.
3. **Scene proof is missing from startup** — resolved with four bounded structural catalogues plus a bounded presence/repair pass for live-generate and stale-bake startup.
4. **Traversal belongs in lower `ShowcaseWorld`** — falsified by assembly compilation; route/preload data stays in `Game.Composition.Showcase`, traversal execution stays in SceneRuntime beside `CharacterMotor`.
5. **SceneRuntime can avoid `VoxelEngine.Structures.Runtime`** — falsified by exact CI `33307182322`: audit assertions consume `StructuralCompositionResult` / `StructuralAttachmentRejectReason`, which are runtime planner contracts. The runtime asmdef has no dependency back to Showcase, so adding that reference is acyclic and minimal.

## Selected fix / current state
- Four production cases: monumental multi-region bridge, wall/tower/gatehouse castle, supported multi-level cliff chain, and two facade/roof styles.
- Existing gallery audit is scoped to this SceneIssue id and records structural metrics, negative cases, three production-`CharacterMotor` traversals, eight structural frames, and cost proxies.
- Focused PlayMode regression plans all four production catalogues twice, compares deterministic graph/cost/bounds, and verifies invalid attachment reasons.
- SceneRuntime assembly now explicitly references `VoxelEngine.Structures.Runtime` for the planner result/rejection types used by its audit harness.

## Remaining gates / blast radius
- Revalidate the exact feature SHA through the existing `ci-test/fixes/agent-5` PlayMode + scene replay transport; do not replace active queued/running work.
- Inspect all full-resolution structural frames and exact logs, record bridge/castle planning, child/primitives/voxel/region/memory/render proxies, and review the final assignment-only diff.
- Only after green exact-SHA regression + built-player validation: complete pending metadata, move open -> pending, then pending -> closed with `status=fixed` / `resolvedUtc`, merge current master, and non-force push the exact feature head to master.
