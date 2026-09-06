# GPU renderer production restoration — implementation plan

**Acceptance scene:** `Assets/Scenes/VoxelShowcase.unity`. Production-quality CPU presentation precedes restoring GPU cutover. CPU captures never count as GPU acceptance.

## Current evidence

Feature `fc767620a0fe5d0dfee204947d13e7eefaa2a3fa`, request `8e6aac9fe8845a04a0bdfca2640fc11988e50506`, run `33996360570` passed the derived module tests/players and 45-second VoxelShowcase replay. The actual SceneIssue player log reports `gpu[req=0 ... pub=0]`. `SceneIssue/verification-final.png` and 15/25/35-second stationary captures remain **prototype/blockout quality**, not visual acceptance: castle visible, giant left slab and right-hand blockout masses unresolved. The installed far-material handoff is repaired; current master owns the proven Input System prerequisite and Composition tests have their own EditMode assembly.

## First geometry discriminator

1. **Lost taper:** the production mountain catalogue emits a `Frustum` followed by switchback fills/ramps. Canonical frusta carry a centre, signed axis and two radii. The old adapter discards those values, and the old renderer substitutes an AABB. This is a demonstrated lossy boundary and the leading slab explanation.
2. **Another owner/source:** the captured slab could additionally belong to a canonical box or another presentation path. Do not claim its removal without the next built-player capture.

Regression source `da3f5be338c57f5fe99ad4324405422e78c3918e` adds eight cases through the real canonical emitter, adapter and mesh resolver. Transverse intersections are checked against integer `PrimitiveRasteriser.Contains`, including all axes, both directions, negative coordinates, unequal normalization dimensions, voxel scales and a zero-radius endpoint. Closed/outward/nondegenerate topology and cache reuse are checked as well.

## Candidate repair and exact CI ownership

Candidate `a164456a9eac5091ec3e5d6c2e03a9de7b675199` resolves signed cap centres/radii in Composition into renderer-neutral normalized values. The existing renderer now tessellates frusta with 50 vertices / 96 triangles and retains revision-based caching. No scene-specific geometry recipe, voxel-authority change, alternate renderer or whole-volume sampling. The existing Rendering-owned player scene now exercises normal/reversed taper and requires its `frusta=2` log.

The **fail-before request** `6ddc72724c6653538be5c5a9818ebee059726264`, run `33999899224`, job `101396766672`, is **queued** at the latest observation. Do not replace it while queued/running. The candidate is committed but has no Unity compile/test or new visual proof yet. After the request terminates, classify the actual result; then submit one exact current-feature-SHA pass-after request, including the VoxelShowcase replay. Inspect its PNGs and actual GPU request/publication counters before reporting progress. No new render exists yet.

## Remaining gates

TGPU-019CPU4F tracks this bounded correction; all other primitive/material/handoff defects remain open. Obtain production-quality CPU stationary/traversal evidence, then reconcile the retained GPU implementation and complete parity, paging/lifetime, streaming/edit, no-fallback, performance and independent-consumer proof. Final exact-SHA gates precede closure directly to `closed`, current-master integration and PR plus auto-merge. Historical GPU fixture success is not full-scene acceptance.
