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
5. **SceneRuntime can avoid `VoxelEngine.Structures.Runtime`** — falsified by exact CI `33307182322`: audit assertions consume runtime planner contracts, so the existing acyclic runtime assembly reference is required.
6. **Bridge still fails after the inclusive support-lattice correction** — falsified by durable artifacts from exact run `33308789329`: the built player reports `STRUCTURAL_GALLERY authored=True` and a valid bridge cost/hash. The earlier `MissingSupport` interpretation came from incomplete log evidence.
7. **Remaining final-run defects** — confirmed from run `33308789329` artifact: the focused PlayMode test throws because `upper-terrain-supported-platform` exceeds the existing `FixedString32Bytes` name contract; built-player route 2 walks off the shallow ramp because `BoxEmitter.RampContains` emits no occupied voxel at the first shallow columns, falls to terrain, then stops at the later wedge face.

## Selected fix / current state
- Four production cases remain: monumental multi-region bridge, wall/tower/gatehouse castle, supported multi-level cliff chain, and two facade/roof styles.
- Existing gallery audit is scoped to this SceneIssue id and records structural metrics, negative cases, three production-`CharacterMotor` traversals, eight structural frames, and cost proxies.
- Focused PlayMode regression plans all four production catalogues twice, compares deterministic graph/cost/bounds, and verifies invalid attachment reasons.
- `fixes/agent-5` has been refreshed with current `origin/master` at `e17e858bfe0497c90b87db70fcfef80a142917a4` through merge commit `3cd4307d54bd7dba7578866c6c0717141f32cd24`; the upstream change is an unrelated new SceneIssue file and does not overlap this feature.
- Next production correction is gallery-only: shorten the cliff socket display name to fit its declared fixed-string storage, and add a one-voxel authoritative base slab under the shallow proof ramp so the production motor has continuous voxel support from the lower platform. Do not alter global ramp raster semantics or raise any budget.

## Remaining gates / blast radius
- Revalidate the corrected feature through the existing `ci-test/fixes/agent-5` PlayMode + scene replay transport; do not replace active queued/running work.
- Inspect all durable full-resolution structural frames and exact logs, record bridge/castle planning, child/primitives/voxel/region/memory/render proxies, and review the final assignment-only diff.
- Expected incremental authoring cost from the ramp continuity fix is one `260 x 1 x 80` proof-only box (20,800 conservative voxels and one primitive before overlap), with no change to global composition ceilings, streaming budgets, solver behavior, or unrelated ramp consumers.
- Only after green exact-SHA regression + built-player validation: complete pending metadata, move open -> pending, then pending -> closed with `status=fixed` / `resolvedUtc`, merge current master, and non-force push the exact feature head to master.
