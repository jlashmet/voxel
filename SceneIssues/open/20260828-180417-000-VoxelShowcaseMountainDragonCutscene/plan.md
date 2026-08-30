# Plan

## Acceptance
Built `VoxelShowcase` must show a substantial grounded mountain with a readable winding ascent, normal grounded traversal from approach/base to summit, a visibly supported dragon placeholder, and proximity dialogue `Hello, I'm Mr. Dragon.` Require source-matched startup bake, green focused acceptance, and human-reviewed approach/base/middle/upper/summit/dialogue captures from the real player.

## Proven causes / fixes
The original mountain footprint overlapped castle-owned feature-suppression regions. Moving `ShowcaseMountainDragonLayout.OriginX` one 512-voxel region west removed the overlap but increased effective mountain authoring from six active regions to all nine (+50%). Output-equivalent rasterizer fast paths now skip/provide whole-block operations only for proven canonical-empty/Uniform `Carve + Box` and fully interior `Fill`/`FillIfEmpty + Frustum` cases; Mixed, clipped, boundary, and other modes retain prior semantics and logical write accounting.

Run `33290377597` proved the nine-region bake could complete once at 236 s / 11,199 MB, then exposed an authored landing obstruction. `MountainDragonRampLandingProgramTests` and the geometry fix now keep 30-voxel end landings flat/clear, narrow alternating X ramps to the interior continuity span, and add the level-0 base landing.

## Current failed gate / discriminators
Exact run `33291011394` for the ramp-fixed source is diagnostic only. Cache miss bake was killed at 241 s (240 s limit), peak 11,482 MB. It wrote source-matched payload/provenance before forced shutdown, but the focused test was skipped. The built player still launched, ran 60 s, exited 23 at waypoint `0/15`, and emitted four stationary captures. Therefore both bake safety margin and route traversal remain open.

Two hypotheses drive the next work:
1. **Evidence-route approach:** after the westward mountain shift, the issue-owned initial straight leg now crosses legacy castle geometry before waypoint 0. Inspect the actual route/start, castle bounds, and replay movement/arrival rules; if confirmed, add only evidence-route approach waypoints around the obstruction and quantify added travel.
2. **Bake margin:** the 236 s prior success was too close to the 240 s contract. Inspect the baker end-of-run path and remaining dominant mountain raster work. Only make another production optimization if a focused semantic/cost discriminator proves an output-equivalent hot path; do not treat a manifest written before timeout as success.

## Integration / blast radius
Feature head before this plan update was `0c5fb83bdd8914af3be14f35559986e11e886598`; `origin/master` has since advanced to `61d03336390ed9079498b183217cbf0ecf0abcd2` and must be merged before the next substantial attempt. Route repair must remain assignment-owned data only. Any bake optimization must document affected shared consumers and preserve voxel truth, primitive order, surface/boundary semantics, and budgets.

## Remaining gates
Merge current master; resolve the two discriminators above; keep `tasks.md` current. Then use only `ci-test/fixes/agent-4` for one exact-parent final request. Require bake to exit cleanly under 240 s with meaningful margin, focused acceptance green, complete grounded `AutoWalk -> CharacterMotor.Step` traversal, and reviewed captures including `Hello, I'm Mr. Dragon.` Only then perform pending/closed metadata and non-force merge to current `origin/master`.