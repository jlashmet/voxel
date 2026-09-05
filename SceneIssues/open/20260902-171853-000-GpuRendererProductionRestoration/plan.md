# GPU renderer production restoration — implementation plan

**Acceptance scene:** `Assets/Scenes/VoxelShowcase.unity`. Establish production-quality CPU presentation before restoring GPU cutover. CPU-only captures never count as GPU acceptance.

## Current evidence

Feature `fc767620a0fe5d0dfee204947d13e7eefaa2a3fa`, request `8e6aac9fe8845a04a0bdfca2640fc11988e50506`, run `33996360570` passed the derived module tests, module players and 45-second VoxelShowcase replay. The actual SceneIssue player log reports `gpu[req=0 ... pub=0]`. `SceneIssue/verification-final.png` and the 15/25/35-second stationary captures remain **prototype/blockout quality**, not visual acceptance: the castle is visible, but the giant left slab and right-hand blockout masses remain.

The shared far-material repair resolves installed material/coating values in Composition and preserves them into the production renderer. Current master owns the Input System prerequisite. The Composition NUnit regression now has a module-local EditMode assembly. Neither prerequisite work nor green automation closes the visual task.

## First geometry discriminator

1. **Lost taper:** the production mountain catalogue emits a `Frustum` followed by supported switchback fills/ramps. Canonical frusta carry a base centre, signed axis direction, and two radii. The presentation adapter currently retains only shape/AABB/axis, and `ProceduralFarFeatureRenderer` renders frusta as boxes. This is a demonstrated lossy boundary and a strong explanation for the slab.
2. **Another owner/source:** the captured slab could additionally belong to an authored box or another presentation path. A frustum correction alone must not be reported as eliminating the slab until the new built-player capture confirms it.

Add a focused behavioral regression through the real canonical emitter, presentation adapter, and production mesh resolver. Compare transverse ray intersections with the authoritative integer `PrimitiveRasteriser.Contains` oracle, across all three axes, both directions, negative coordinates, unequal bake dimensions and voxel scales. Require nondegenerate, outward, closed triangles. The existing AABB output must fail before the repair.

## Selected bounded repair

After the failing discriminator, transport resolved cap centres and radial extents as renderer-neutral normalized geometry values. Tessellate the frustum in the existing far renderer, caching under the existing bake revision. No scene names, coordinates, material IDs, extra renderer, voxel-authority changes, or whole-volume sampling. Keep other primitive mismatches explicit rather than claiming universal geometry parity.

Extend Rendering-owned built-player coverage through the same production frustum path. Replay VoxelShowcase immediately after the correctness fix and inspect the exact artifact. Preserve separate tasks.md; TGPU-019CPU3/4 own this necessary geometry repair.

## Remaining gates

Finish every CPU-visible material/geometry/handoff defect and obtain production-quality stationary/traversal captures. Then reconcile the retained GPU implementation, restore normal cutover, and complete deterministic parity, paging/lifetime, streaming/edit, no-fallback, performance and independent-consumer evidence. Run final exact-SHA validation; only then close directly to `closed`, merge current master, and promote by PR plus auto-merge. GPU historical fixture success remains scoped evidence, not full-scene success.
