# Plan

## Acceptance
Built `VoxelShowcase` must show a substantial grounded mountain with a readable winding ascent, normal grounded traversal to a usable summit, the explicitly allowed cube dragon visibly supported, and proximity dialogue `Hello, I'm Mr. Dragon.` WorldBuilder/shared modules own geometry and interaction. `AGENTS.md` requires player-visible work to be `production-quality`; only dragon art has the issue-specific placeholder allowance. Closure also requires a source-matched checked-in startup bake and exact built-player evidence.

## Proven state
Architecture, grounded traversal/headroom/support, castle-suppression avoidance, sparse upper-dragon bake coverage, raster fast-path equivalence, and exact-CI native bake exit are proven. Run `33310677691` passed structural acceptance and 17/17 normal-movement replay under unchanged 240 s / 14 GiB guards.

Run `33314740587` falsified “material separation is sufficient.” Revision-4 dark-rock/moss/dirt improved readability and baked in about 206 s, but exact frames remained `prototype/blockout quality`: repeated rounded support banks, pile-of-domes silhouette, hard road edges, engineered summit. Its requested test also exposed one stale legacy-material assertion; replay reached grounded waypoint 16/17 before the 58 s harness timeout with dialogue already triggered.

Revision-5 then tested duplicate broad full-height ridges while retaining primitive count. Exact run `33316622225` falsified that cost hypothesis: the fresh bake timed out at 241 s with 11,459 MiB peak RSS / zero swap under the unchanged 240 s / 14 GiB guard. No manifest was produced, so the requested test was skipped and downstream player captures are invalid for visual review.

## Selected fix
Revision 6 is implemented. The generic physical catalogue, route truth and primitive count stay fixed. Each consecutive same-elevation support pair becomes one full-height rock ridge whose top covers both authored support centers with path-width margin plus one lower/narrow rock buttress anchored alternately at an original center. The three broad foothills remain moss and the narrowed summit crest remains supported. Every carve/ramp/path instruction stays byte-identical. A conservative full-raster support proxy falls from ~451.4M generic units to ~258.6M (~57.3%); regression requires <75%. Startup realization provenance is bumped to revision 6 so rejected revision-5 bytes are stale.

Current master `65e33762a0d0...` is already merged; immediately before the request the feature will refresh from then-current master again.

## Next discriminator
Review the final source diff, merge then-current master, then use only `ci-test/fixes/agent-4` for `MountainDragonVisualFinalAcceptanceTests.ProductionQualityMountainMaterialsAndEncounterAreReadyForBuiltPlayerReplay` plus the built-player evidence route. The unchanged 240 s / 14 GiB bake guard must pass before the exact approach/base/middle/upper/summit/dialogue frames can be accepted. Any below-`production-quality` result adds concrete defects to `tasks.md` and remains open.

## Remaining gates
1. Green exact PlayMode + built-player run and unchanged bake/runtime budget.
2. Commit that accepted run's exact generated payload + manifest and validate clean-checkout consumption/provenance.
3. Complete `open -> pending -> closed` metadata/lifecycle only after every acceptance/checklist item is green; merge then-current master and non-force push exact feature head to master.
