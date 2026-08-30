# Plan

## Observed behavior / acceptance
- Canonical production remains `FeatureDefinition` + typed `SlotSpec` + `ShapeOp.CallSlot` + `FeatureCatalogue` + descendant-aware `FeatureRegionBuild`; no parallel structural solver.
- Core deterministic composition, validation, bounded recursion/cost, authoritative child rasterization, support metadata, decoration handoff, four proving catalogues, negative contracts, and production `CharacterMotor` traversal are implemented.
- Mechanical checkpoint `33314706183` on `945ec3fe05798096e6177b1eba1a213be4b47e7b` passed focused PlayMode + standalone-player traversal and emitted eight full-resolution frames, but manual art review rejected the old proof as below the required production-quality bar.
- Current visual rework is merged with master at `0cd53d0761698490ad29edd563167251030b1aa5`: the authoritative voxel presentation now adds stronger bridge span rhythm/abutments/piers/gorge-water context, castle crown/gate/tower grounding, cliff terraces/supports/vertical context, and materially distinct facade/roof assemblies. Global structural and motor budgets remain unchanged.

## Competing hypotheses / discriminator
1. The prior failure was mainly presentation hierarchy/context: stronger grounded construction, silhouette, material separation, and establishing context will make the same typed compositions read production-quality.
2. The underlying proof geometry/camera bounds are still too blockout-like or poorly framed, so extra detail will not be sufficient.

Discriminator: one final exact-SHA PlayMode request using the existing `ci-test/fixes/agent-5` transport, including the exact SceneIssue built-player harness. Inspect every durable 1600x900 structural frame and classify the result using current `AGENTS.md`; any frame that remains below `production-quality` keeps the issue open.

## Reusability review gates
- Keep all socket compatibility, facing, capacity, clearance, support, recursion, deterministic selection, and budget semantics in generic `VoxelEngine.Structures` contracts/runtime; the WorldbuildingGallery must only author and demonstrate those semantics, never define gallery-only attachment rules.
- Audit the large gallery structural composition/presentation files before closure and move any reusable structural decision or transformation logic down into the generic composition layer rather than duplicating it in showcase code.
- Prove the same typed socket contract can compose at least two materially different structure families without special-case bridge/castle/cliff identifiers in the generic solver.
- Keep decoration handoff as an adapter into the existing decoration system; do not let structural sockets become a parallel micro-detail/decorative placement system.

## Cost / blast-radius gates
- Baseline run `33314706183`: bridge planning `0.014 ms`, castle `0.005 ms`; 20 children, 51 structural primitives, 15 visited regions, 40 rasterized instances, 15,907,368 written voxels; traversal bridge `115 m -> 1.25 m`, gate `17.6 m -> 1.223 m`, cliff `42 m -> 1.317 m`.
- Presentation changes are confined to the existing gallery authoritative-voxel pass and audit framing. No solver semantics, composition ceilings, scan budgets, `CharacterMotor` tolerances, or unrelated generation/render behavior changed.
- Bridge presentation footprints are bounded at 1220 voxels, below `MaxFootprintVoxels=1280`; proof-local primitive ceilings remain below the existing per-instance ceiling. Final CI must confirm voxel-cost and runtime bounds.

## Remaining gates
- Freeze final source SHA after updating this plan/tasks; do not edit production/test content afterward.
- Green exact-SHA focused PlayMode + exact-scene built-player audit; all three traversals and negative contracts pass.
- Inspect all eight durable frames; only `production-quality` passes visual acceptance.
- Record final measured costs/blast radius, complete pending metadata, move open -> pending -> closed, refresh master again, and non-force fast-forward `origin/master` to the exact feature head.
