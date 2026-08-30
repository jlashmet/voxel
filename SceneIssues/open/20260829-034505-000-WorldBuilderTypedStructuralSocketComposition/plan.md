# Plan

## Observed behavior / acceptance
- Canonical production remains `FeatureDefinition` + typed `SlotSpec` + `ShapeOp.CallSlot` + `FeatureCatalogue` + descendant-aware `FeatureRegionBuild`; no parallel structural solver.
- Core deterministic composition, validation, authoritative child rasterization, support metadata, decoration handoff, four proving catalogues, negative contracts, and production `CharacterMotor` traversal are implemented.
- Mechanical checkpoint `33314706183` passed focused PlayMode, all three real-player traversals, and emitted eight frames; manual review rejected that older presentation as below the current production-quality art bar.
- Visual rework adds authoritative-voxel bridge hierarchy/gorge context, castle crown/gate/tower grounding, cliff terraces/supports, and distinct facade/roof variants without changing global solver or motor budgets.

## Hypotheses / discriminator
1. The remaining blocker is presentation architecture/cost: independently bounded presentation pieces will preserve the stronger visuals while satisfying the existing conservative composition budget.
2. Even after budget-safe partitioning, the rendered compositions/cameras may remain below production quality.

Run `33323976945` discriminated the first failure: focused PlayMode passed, but built-player presentation rejected the combined bridge context with `VoxelBudgetExceeded` because its full 1220×height×720 bounds were charged. The selected fix partitions river, terrain shoulders, piers, castle sections, cliff sections, and facade variants into independent bounded authoritative catalogues. Static audit also corrected castle buttresses outside local bounds and makes each pier footprint cover its actual terrain-derived cap.

Next discriminator: one exact-SHA PlayMode request through existing `ci-test/fixes/agent-5`, including the SceneIssue built-player harness. Any runtime/budget failure or any frame below `production-quality` keeps the issue open.

## Reusability / blast radius
- Generic compatibility, facing, capacity, clearance, support, recursion, deterministic selection, and budgets remain in `VoxelEngine.Structures`; gallery code only authors proof content.
- Decoration handoff remains an adapter into the existing decoration system; structural sockets are not used for micro-detail placement.
- Feature-only diff is limited to structural composition contracts/runtime, focused regressions, gallery proof/audit content, and this SceneIssue bookkeeping. No unrelated capture, workflow, global budget, `CharacterMotor`, or CI request file is changed on the feature branch.
- Largest fixed presentation footprint remains 1220 voxels (<1280). Split-piece conservative costs are individually below the existing 16,777,216-voxel ceiling by construction; final built-player logs must confirm actual costs/writes.

## Remaining gates
- Green exact-SHA focused PlayMode + exact-scene built-player audit; all three traversals and negative contracts pass.
- Inspect every durable full-resolution structural frame; only `production-quality` passes visual acceptance.
- Record final planning/voxel/region/memory/render cost and check every task/acceptance item.
- Complete pending metadata and open -> pending bookkeeping, then pending -> closed with `status=fixed`/`resolvedUtc` only after exact-SHA gates pass.
- Refresh current `origin/master`, merge if needed, push feature head, then non-force push that exact head to `origin/master`; retry if master advances.
