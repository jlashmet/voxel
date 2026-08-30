# Plan

## Acceptance
Built `VoxelShowcase` must show a substantial grounded mountain/readable winding ascent, normal grounded traversal from base to summit, a visibly supported dragon placeholder, and proximity dialogue `Hello, I'm Mr. Dragon.` Durable approach/base/switchback/summit/dialogue captures plus a source-matched startup bake are required.

## Proven causes
Fresh-bake run `33285254695` cleared the earlier bake-time blocker (~226 s) but exposed missing Gravel at first-turn voxel `(-435,264,-316)`. `ShowcaseWorld` suppresses generic catalogue features in castle-owned regions; castle ownership is `x=-1..1,z=-1..1`, while the old mountain footprint occupied `x=-3..-1,z=-1..1`, so its eastern column was intentionally discarded. The feature-local layout shift `OriginX -1200 -> -1712` removes that overlap, but raises effective mountain authoring from 6 active footprint regions to all 9 (+50%). Exact request `517acdd1e7498f703b1a8355f2dc026261f33e97`, run `33287514117`, then hit the 241 s bake watchdog twice (10.5 GB peak RSS on attempt 1).

## Hot-path discriminator / selected optimization
The realized mountain program contains 20+ convex frustum support fills. `PrimitiveRasteriser` already fast-paths canonical-empty box carve and fully covered Uniform box carve, but a fully interior frustum `Fill` into canonical Empty still performs 512 identical containment/read/write iterations per 8^3 storage block. Regression `FrustumFillAtomicallyAuthorsFullyInteriorEmptyBlockWithoutOverfillingBoundaryBlock` was added first and corrected for region-local reads at `0bbf30043ccc...`.

Production commit `090d0032456e...` adds only a conservative frustum-fill fast path: a complete canonical-empty block is atomically authored only when all eight extreme voxel centres are inside the convex frustum. The emitted `VoxelCell` exactly matches the existing Fill semantics and logical accounting remains 512 writes. Partial/non-empty blocks, all other modes/shapes, primitive ordering, and the boundary-halo pass are unchanged.

## Blast radius / remaining gates
Shared rasterizer behavior changes only for the proven interior-empty frustum case; nominal mountain footprint, primitive count, carve volume, region ownership, and serialized semantics are unchanged. Current `origin/master` (`d4b31a700bae...`) is already an ancestor of the candidate. Next: update task evidence, refresh/merge latest master immediately before the one final request, then reuse only `ci-test/fixes/agent-4`. Require a fresh source-matched bake under the 240 s subprocess contract, green exact focused acceptance, complete grounded built-player route, and human-reviewed approach/base/switchback/summit/dialogue captures. Only then complete pending metadata and close/merge.
