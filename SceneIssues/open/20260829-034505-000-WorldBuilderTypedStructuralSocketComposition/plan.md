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
7. **Cliff ramp continuity was the only route-2 defect** — falsified by exact run `33311927310`. The fixed-string-safe name and one-voxel ramp base slab cleared the focused PlayMode regression and improved route 2 from 30.919 m remaining to 6.435 m, but the motor now stops at the ramp/upper-platform seam. The authored upper platform walking surface is 8 voxels (0.8 m) above the ramp top, exceeding the production motor's 0.35 m step height.

## Selected fix / current state
- Four production cases remain: monumental multi-region bridge, wall/tower/gatehouse castle, supported multi-level cliff chain, and two facade/roof styles.
- Existing gallery audit is scoped to this SceneIssue id and records structural metrics, negative cases, three production-`CharacterMotor` traversals, eight structural frames, and cost proxies.
- Focused PlayMode regression plans all four production catalogues twice, compares deterministic graph/cost/bounds, and verifies invalid attachment reasons; it passed on exact feature source `250cc16515451e57a5543d5ae1104e3c5f79b2eb` in run `33311927310`.
- `fixes/agent-5` was refreshed with `origin/master` at `e17e858bfe0497c90b87db70fcfef80a142917a4` through merge commit `3cd4307d54bd7dba7578866c6c0717141f32cd24`; the upstream change is an unrelated new SceneIssue file and does not overlap this feature.
- The cliff proof now uses fixed-string-safe socket name `upper-terrain-platform` and a one-voxel authoritative base slab under the shallow ramp. The remaining production correction is gallery-only: lower the upper-platform attachment by 8 voxels so its 12-voxel platform thickness ends exactly at the ramp's top walking surface. Keep terrain-support probes, stable socket identity, global ramp semantics, and production `CharacterMotor` unchanged.

## Remaining gates / blast radius
- Revalidate the corrected feature through the existing `ci-test/fixes/agent-5` PlayMode + scene replay transport; do not replace active queued/running work.
- Run `33311927310` produced no `StructuralCompositionAudit` frames because the structural audit exits on route-2 failure before capture, so unrelated town screenshots from that artifact are not acceptance evidence.
- Inspect all durable full-resolution structural frames and exact logs after traversal clears; record bridge/castle planning, child/primitives/voxel/region/memory/render proxies, and review the final assignment-only diff.
- Ramp continuity cost already added is one `260 x 1 x 80` proof-only box (20,800 conservative voxels and one primitive before overlap). The seam-height correction only changes the upper platform's local Y attachment by -8 voxels; it adds no primitive, allocation, global composition ceiling, streaming budget, solver behavior, or unrelated ramp/CharacterMotor cost.
- Only after green exact-SHA regression + built-player validation: complete pending metadata, move open -> pending, then pending -> closed with `status=fixed` / `resolvedUtc`, merge current master, and non-force push the exact feature head to master.
