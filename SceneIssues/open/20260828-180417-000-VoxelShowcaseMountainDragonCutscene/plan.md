# Plan

## Acceptance
Built `VoxelShowcase` must show a substantial grounded mountain with a readable winding ascent, normal grounded traversal to a usable summit, the explicitly allowed cube dragon visibly supported, and proximity dialogue `Hello, I'm Mr. Dragon.` WorldBuilder/shared modules own geometry and interaction. `AGENTS.md` requires player-visible work to be `production-quality`; only dragon art has the issue-specific placeholder allowance. Closure also requires a source-matched checked-in startup bake and exact built-player evidence.

## Proven state
Architecture, grounded traversal/headroom/support, castle-suppression avoidance, sparse upper-dragon bake coverage, raster fast-path equivalence, and exact-CI native bake exit are proven. Run `33310677691` passed structural acceptance and 17/17 normal-movement replay under unchanged 240 s / 14 GiB guards.

Run `33314740587` falsified “material separation is sufficient.” Revision-4 dark-rock/moss/dirt improved readability and baked in about 206 s, but exact frames remained `prototype/blockout quality`: repeated rounded support banks, pile-of-domes silhouette, hard road edges, engineered summit. Its requested test also exposed one stale legacy-material assertion; replay reached grounded waypoint 16/17 before the 58 s harness timeout with dialogue already triggered.

Revision-5 then tested duplicate broad full-height ridges while retaining primitive count. Exact run `33316622225` falsified that cost hypothesis: the fresh bake timed out at 241 s with 11,459 MiB peak RSS / zero swap under the unchanged 240 s / 14 GiB guard. No manifest was produced, so the requested test was skipped and downstream player captures are invalid for visual review.

## Selected fix
Revision 6 keeps the generic physical catalogue, route truth and primitive count. For each same-elevation support pair, one full-height rock ridge will span both authored support centers while the second existing primitive becomes a lower/narrow rock buttress at one original support center. This removes the rejected duplicate full-height raster volume while retaining continuous support and asymmetry. The three broad foothills remain moss; the narrowed summit crest remains supported. The visual regression will freeze every carve/ramp/path instruction, validate deterministic ridge+buttress coverage, and require a support-frustum raster-volume proxy below the generic baseline. Prepared-bake acceptance remains production rock role `6`; evidence timeout remains 59 s with movement/gameplay unchanged.

Current master `65e33762a0d0...` is already merged; immediately before the next request the feature will refresh from then-current master again.

## Next discriminator
After implementing revision 6 and its regression/provenance bump, merge then-current master. Use only `ci-test/fixes/agent-4` for `MountainDragonVisualFinalAcceptanceTests.ProductionQualityMountainMaterialsAndEncounterAreReadyForBuiltPlayerReplay` plus the built-player evidence route. The unchanged 240 s bake guard must pass before the exact approach/base/middle/upper/summit/dialogue frames can be accepted. Any below-`production-quality` result adds concrete defects to `tasks.md` and remains open.

## Remaining gates
1. Green exact PlayMode + built-player run and unchanged bake/runtime budget.
2. Commit that accepted run's exact generated payload + manifest and validate clean-checkout consumption/provenance.
3. Complete `open -> pending -> closed` metadata/lifecycle only after every acceptance/checklist item is green; merge then-current master and non-force push exact feature head to master.
