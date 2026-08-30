# Plan

## Acceptance
Built `VoxelShowcase` must show a substantial grounded mountain with a readable winding ascent, normal grounded traversal to a usable summit, the explicitly allowed cube dragon visibly supported, and proximity dialogue `Hello, I'm Mr. Dragon.` WorldBuilder/shared modules own geometry and interaction. `AGENTS.md` requires player-visible work to be `production-quality`; only dragon art has the issue-specific placeholder allowance. Closure also requires a source-matched checked-in startup bake and exact built-player evidence.

## Proven state
Architecture, grounded traversal/headroom/support, castle-suppression avoidance, sparse upper-dragon bake coverage, raster fast-path equivalence, and exact-CI native bake exit are proven. Run `33310677691` passed structural acceptance and 17/17 normal-movement replay under unchanged 240 s / 14 GiB guards.

Run `33314740587` falsified “material separation is sufficient.” Revision-4 dark-rock/moss/dirt improved readability and baked within budget, but exact frames remained `prototype/blockout quality`: repeated rounded support banks, pile-of-domes silhouette, hard road edges, engineered summit. Its requested test also exposed one stale legacy-material assertion; replay reached grounded waypoint 16/17 before the 58 s harness timeout with dialogue already triggered.

## Selected fix
Revision 5 keeps the generic physical catalogue and route truth, but the reusable presentation layer now keeps moss only on three broad asymmetric foothills, consolidates consecutive same-elevation support frustums into overlapping rock ridges, and narrows the summit crest while retaining cube support. It adds no primitives. The visual regression now freezes primitive/budget count plus every carve/ramp/path instruction and permits only the intended support-ridge/core presentation fields. Prepared-bake acceptance expects production rock role `6`; evidence timeout is 59 s with movement/gameplay unchanged.

Current master `65e33762a0d0...` is merged at `edddd087f1e6...`; its delta was an unrelated GPU SceneIssue file.

## Next discriminator
Immediately before request, merge then-current master. Use only `ci-test/fixes/agent-4` for `MountainDragonVisualFinalAcceptanceTests.ProductionQualityMountainMaterialsAndEncounterAreReadyForBuiltPlayerReplay` plus the built-player evidence route. Inspect exact approach/base/middle/upper/summit/dialogue frames. Any below-`production-quality` result adds concrete defects to `tasks.md` and remains open.

## Remaining gates
1. Green exact PlayMode + built-player run and unchanged bake/runtime budget.
2. Commit that accepted run's exact generated payload + manifest and validate clean-checkout consumption/provenance.
3. Complete `open -> pending -> closed` metadata/lifecycle only after every acceptance/checklist item is green; merge then-current master and non-force push exact feature head to master.
