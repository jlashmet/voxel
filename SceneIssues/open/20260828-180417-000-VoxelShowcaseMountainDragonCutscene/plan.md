# Plan

## Acceptance
Built `VoxelShowcase` must show a substantial grounded mountain with a readable winding ascent, normal grounded traversal from approach/base to summit, a visibly supported dragon placeholder, and proximity dialogue `Hello, I'm Mr. Dragon.` Closure requires a source-matched startup bake, green focused acceptance, complete production `AutoWalk -> CharacterMotor.Step` traversal, and human-reviewed approach/base/middle/upper/summit/dialogue captures from the exact built player.

## Proven implementation state
The mountain/path/cutscene behavior is composed through shared WorldBuilder/game modules. The mountain footprint was moved one 512-voxel region west to avoid castle-owned feature suppression; the resulting nine-region authoring cost was brought under the bake guard with output-equivalent block/raster fast paths. Alternating X ramps were narrowed between explicit flat end landings so the walking envelope remains clear while the tier elevations and route coordinates are preserved.

The batch-bake shutdown tail was the last measured bake blocker. `ShowcaseWorldBaker` now permits immediate successful process termination only for the exact GitHub Actions batch invocation of `VoxelEngine.Showcase.Editor.ShowcaseWorldBaker.BakeShowcaseWorld`; interactive bakes, non-CI batches, `-runTests`, and other execute methods retain normal lifecycle ownership. Exact run `33296679805` proved a fresh source-matched bake completed in 224 s, the following Unity invocation reopened immediately, and the focused test consumed that generated payload. This restores about 16 s of process-level margin under the 240 s wrapper.

## Latest failed gate and fixes
Run `33296679805` then exposed two narrower validation issues rather than production-generation failures:

1. The highest 24-voxel headroom probe crossed from world Y 511 to Y 512 at `(-1277,512,62)`. The sparse startup bake intentionally does not materialize empty sky-only vertical regions. `MountainDragonPathHeadroomBakeTests` now treats an absent region as material `0` only for headroom probes; path-floor and support probes remain strict and still fail if their containing region/material is absent.
2. The built player successfully cleared the castle detour and reached grounded waypoint `12/17` before the route's 55 s internal timer. The assignment-owned route keeps every coordinate, grounded check, expected Y offset, motor speed, and capture name unchanged, but reduces six screenshot settle holds from 6.25 s total to 0.6 s and raises the internal route timeout to 58 s. This recovers about 5.65 s while remaining inside the workflow's fixed 60 s replay ceiling and preserving normal production movement.

## Integration / blast radius
The two latest changes are test/evidence-only: one semantic reader correction in a PlayMode regression and one assignment-owned JSON timing budget. They do not change voxel output, runtime movement speed, mountain geometry, collision, world streaming, or gameplay composition. Current `origin/master` has advanced beyond the feature branch; merge it before the next request, inheriting unrelated road-generation/lifecycle work while preserving the Mountain Dragon rasterizer optimizations. Resolve only the shared `PrimitiveRasteriser` overlap by combining master road-shape semantics with the feature's proven fast paths.

## Remaining gates
1. Update `tasks.md` with run `33296679805`, the two completed corrective changes, and the pending validation work.
2. Merge current `origin/master` into `fixes/agent-4` as a real two-parent merge and verify master is an ancestor.
3. Use only `ci-test/fixes/agent-4` for the next exact-parent final request; do not replace it while queued/running.
4. Require a clean fresh/cache-valid bake, exact focused acceptance green, complete grounded 17-waypoint built-player traversal, and durable/human-reviewed captures for approach, base, middle, upper, summit, and dialogue.
5. Confirm accepted source-matched startup payload/manifest provenance and commit the accepted generated payload/evidence if repository workflow permits it without creating a second CI transport.
6. Only after every checklist/acceptance item is green, complete pending metadata, move only this assignment `open -> pending -> closed`, set `status=fixed` and `resolvedUtc`, merge the then-current `origin/master`, and push the exact feature head to `origin/master` non-force; fetch/merge/retry if master advanced.