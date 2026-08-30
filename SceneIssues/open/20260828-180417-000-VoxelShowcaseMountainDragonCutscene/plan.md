# Plan

## Acceptance
Built `VoxelShowcase` must show a substantial grounded mountain/readable winding ascent, normal grounded traversal from base to summit, a visibly supported dragon placeholder, and proximity dialogue `Hello, I'm Mr. Dragon.` Durable approach/base/switchback/summit/dialogue captures plus a source-matched startup bake are required.

## Proven causes and completed discriminators
The original mountain footprint overlapped castle-owned feature-suppression regions. Moving `ShowcaseMountainDragonLayout.OriginX` one 512-voxel region west removed that overlap while preserving the 3x3 footprint and route geometry, but increased effective mountain authoring from six active regions to all nine (+50%). That exposed a 240 s bake-cost blocker.

The bake-cost investigation stayed output-equivalent and progressively narrowed shared rasterizer work: canonical-empty `Carve + Box` blocks are skipped, fully covered Uniform carve blocks use authoritative whole-cell clearing, fully interior canonical-empty Frustum Fill blocks use one whole-cell fill, deep-interior frustum halo blocks skip the 512-voxel distance scan, and the same proven cases were extended narrowly to the dominant `FillIfEmpty + Frustum` support mode. Mixed, clipped, boundary, non-frustum, and non-proven modes retain their prior per-cell semantics and exact logical write accounting.

Exact request `cea45ae169606f02a4e0d332cf92b87871ed4115` for source `e394149aaf0d1ee685399908685f4309142a7484`, run `33290377597`, finally proved the full nine-region source-matched startup bake fits the subprocess contract: 236 s at 11,199 MB peak RSS. That clears the performance blocker with roughly four seconds of margin.

## Fresh-bake semantic discriminator
The same run reached semantic acceptance and failed at `turn landing 1 column 2`: material `13` occupied `(-1277,312,-197)` inside the required 24-voxel clear envelope. This is authored path material, not terrain/support corruption or a rasterizer fast-path mismatch. The prior X ramps spanned the full 360-voxel run, so the next ramp's low wedge overlapped the 30-voxel flat turn landing and rose into its headroom. The built-player route then timed out before waypoint 1, so that run's generic fallback captures are explicitly non-acceptance evidence.

Before changing production geometry, `MountainDragonRampLandingProgramTests` was added to require every alternating X ramp to keep the low landing centre clear above its floor while retaining the first interior floor column and reaching the full +46-voxel tier height immediately before the high landing.

Production `EmitPathSurface` now leaves complete 30-voxel turn landings at both ends. For the current 360-run/30-width/46-rise layout, the interior ramp run is 300 voxels; integer-ramp continuity requires only a six-voxel low-end overlap, and those six columns remain floor-only. The ramp reaches full tier height at the last interior column before the opposite landing. An explicit base landing was added for level 0. This changes no terrain/support/rasterizer/storage behavior and adds only one path-surface primitive; the existing <=80 primitive acceptance remains the budget gate.

## Master integration and blast radius
Current `origin/master` `0901be5a0640e3eec103cdf3c97aa12b8cd42a9e` was integrated into `fixes/agent-4` as a two-parent merge `e87e3b67161155faa5b275a0840d1f8f61ff8d42`. Master-only changes were the separate town-architecture feature and its lifecycle files; the merge used those exact master blobs/deletions and had no overlap with the mountain catalogue, mountain tests, or this assignment. `master` is now an ancestor (`behind_by=0`).

The post-failure candidate blast radius is intentionally narrow: one new focused PlayMode source test + metadata and one geometry-only change in `WorldBuilderMountainLandmarkCatalogue.EmitPathSurface`. It does not increase mountain footprint/regions, headroom-carve volume, support mass, runtime systems, shared rasterizer fast paths, or route coordinates. The only realized-program cost increase is one 30x1x30 base-floor box primitive; X-ramp voxel volume decreases because ramps no longer cover both full end landings.

## Remaining gates
Update `tasks.md` with this completed discriminator/fix, then re-check current master immediately before the request. Use only `ci-test/fixes/agent-4` to create one exact-parent request for `VoxelEngine.Tests.PlayMode.MountainDragonFinalAcceptanceTests.NaturalizedMountainBakeAndEncounterAreReadyForBuiltPlayerReplay` and the assigned SceneIssue. Require: source-matched bake under the 240 s subprocess contract; green focused acceptance including primitive/cost and realized headroom contracts; complete grounded production `AutoWalk -> CharacterMotor.Step` route; human-reviewed approach/base/middle/upper/summit/dialogue captures showing the readable mountain/path, supported dragon, and `Hello, I'm Mr. Dragon.` Only after all gates are green should pending/closed metadata be completed and the assignment merged to current `origin/master` non-force.
