# Plan

## Acceptance
Built `VoxelShowcase` must show a substantial grounded mountain, readable winding ascent, normal grounded traversal to the summit, visibly supported dragon placeholder, and proximity dialogue `Hello, I'm Mr. Dragon.` Closure requires a source-matched startup bake, green exact focused acceptance, complete production `AutoWalk -> CharacterMotor.Step` traversal, and human-reviewed approach/base/middle/upper/summit/dialogue captures.

## Proven state
WorldBuilder/shared modules own the mountain, path, placeholder, and encounter. The footprint is west of castle-owned feature suppression; alternating ramps have explicit clear landings; the 24-voxel headroom contract is covered. Output-equivalent Box/Frustum raster fast paths and CI-only successful-bake shutdown keep the nine-region source bake near the 240 s guard without changing runtime semantics. Current master `e95324aeaef6...` is already an ancestor of the feature branch.

Run `33298125653` on source `2106820d31cb...` proved a fresh current-master bake completed in 225 s at ~5.37 GB RSS with no swap growth and Unity reopened. The real player then completed all 17 grounded/Y-checked waypoints in 57.4 s; named captures visibly show the mountain/path, supported red summit placeholder, and `Hello, I'm Mr. Dragon.`

## Latest discriminator / selected fix
The only failed assertion in `33298125653` was startup-bake dragon material at `(-1112,530,200)`. The placeholder's fixed-altitude 60-voxel footprint crosses world Y 512, but offline baking materialised only terrain surface layers, so its Y=1 region was absent from `CaptureBake`; runtime streaming later generated that same layer, explaining why the built-player capture was correct.

Selected fix: `ShowcaseWorld.BakeCoverage` generically plans only explicit `Structure + FixedAltitude` footprint regions inside the startup disc, then `GenerateForBakeBlocking` materialises those regions after the terrain disc. This includes the dragon's lower/upper layers without making mountain/headroom sky resident. A focused regression requires exactly those two layers for the mountain-only catalogue. Blast radius is bake-only; gallery baking, runtime streaming, movement, geometry, and rasterisation are unchanged.

## Remaining gates
1. Issue one exact-parent PlayMode request through `ci-test/fixes/agent-4`; leave it untouched while queued/running.
2. Require fresh/cache-valid bake <240 s and <14 GB, Unity reopen, green `MountainDragonFinalAcceptanceTests.NaturalizedMountainBakeAndEncounterAreReadyForBuiltPlayerReplay`, and complete built-player replay/captures.
3. Record accepted bake/source provenance and payload/manifest evidence if retrievable through the existing transport.
4. Only after every checklist/acceptance item is green, complete metadata, move only this assignment `open -> pending -> closed`, set `status=fixed`/`resolvedUtc`, merge then-current master, and push the exact head to `origin/master` non-force.