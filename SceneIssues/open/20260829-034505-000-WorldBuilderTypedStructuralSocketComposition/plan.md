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
6. **Bridge support placement was only a spacing/range issue** — falsified by exact CI `33308789329`. Correcting the inclusive support lattice advanced composition from `ClearanceBlocked` to `MissingSupport`, proving the spacing correction but exposing a second catalogue contract defect: `FindBridgeSite` maximizes gorge relief without requiring all three mandatory piers to reach terrain, while the authored support probe can extend 60 voxels below the physical pier.

## Selected fix / current state
- Four production cases: monumental multi-region bridge, wall/tower/gatehouse castle, supported multi-level cliff chain, and two facade/roof styles.
- Existing gallery audit is scoped to this SceneIssue id and records structural metrics, negative cases, three production-`CharacterMotor` traversals, eight structural frames, and cost proxies.
- Focused PlayMode regression plans all four production catalogues twice, compares deterministic graph/cost/bounds, and verifies invalid attachment reasons.
- SceneRuntime assembly explicitly references `VoxelEngine.Structures.Runtime` for the planner result/rejection types used by its audit harness.
- Bridge repeated-support range is corrected to the full inclusive three-bin lattice (`80..319`, spacing `80`) without increasing global placement attempts.
- Next production change is gallery-only: make bridge-site selection accept only candidates where each of the three deterministic pier loci has at least one terrain contact within the actual pier reach, and align `SupportProbeMin.y` with that physical pier reach. Do not widen global support/placement budgets and do not change the runtime planner unless new evidence requires it.

## Remaining gates / blast radius
- Revalidate the corrected bridge catalogue through the existing `ci-test/fixes/agent-5` PlayMode + scene replay transport; do not replace active queued/running work.
- Treat the missing standalone screenshot from run `33308789329` as downstream until structural composition reaches the scene audit; only change the capture harness if a green structural build still demonstrates an independent capture defect.
- Inspect all full-resolution structural frames and exact logs, record bridge/castle planning, child/primitives/voxel/region/memory/render proxies, and review the final assignment-only diff.
- Bridge support correction is confined to the deterministic proof catalogue/site scan. Expected incremental cost is bounded terrain-height sampling during the existing small bridge-site search; no per-region runtime budget or global composition ceiling changes.
- Only after green exact-SHA regression + built-player validation: complete pending metadata, move open -> pending, then pending -> closed with `status=fixed` / `resolvedUtc`, merge current master, and non-force push the exact feature head to master.
