# Plan — Kentridge vegetation meadow density

## Scope and acceptance
Work only `20260829-015100-000-KentridgeVegetationMeadowDensity` on `fixes/agent-5`. Kentridge must use reusable WorldBuilder ecology policy, render one connected grass meadow with at least 3,000 blades, respect exclusions, and show plainly visible wind in a stationary built-player replay. Do not edit scene serialization or `.github/test-request.json` on the feature branch.

## Material evidence
- Reusable regional ecology policy is implemented through production terrain sampling; Kentridge allows only semantic Grass and no trees/ambient animals.
- Run `33246401704` is green for its focused clock regression and real-player harness. It reports 11,478 grass instances / 114,580 blades, 57,589 blades in the primary meadow, 8 chunks, and zero excluded-surface grass, but the grass/ground raster is byte-identical at 39.3s, 49.3s, and 59.3s while the sky changes.
- Two custom `_GrassTime` delivery fixes therefore failed visually. Engine-managed `_Time.y` is already proven by moving sky pixels and is now used by the grass shader, eliminating redundant CPU clock state.
- New geometry evidence identifies the visibility defect: `ShowcaseWorld.MaterialAt(y, surface)` defines `y == surface` as the topmost occupied voxel and `y > surface` as empty; the same class places above-ground structures at `ground + 1`. `KentridgeRegionLife.TryGround()` currently anchors semantic vegetation at `surface * VoxelSize`, one voxel below the exposed face. Grass ribbons are only about 0.26–0.58 m tall, so much/all of their readable silhouette is buried despite topology counts.

## Selected correction
Keep the engine-managed shader clock cleanup. Correct reusable Kentridge ecology grounding to the exposed face: `(SurfaceHeight + 1) * VoxelSize`, preserving the sampled normal, X/Z placement, density, exclusions, deterministic seed, packed geometry, and renderer. Add a production-scene regression that inspects generated semantic grass roots against the same world `SurfaceHeight + 1` contract.

## Blast radius / cost
Grounding change is limited to vegetation/ambient-life samples emitted by `KentridgeRegionLife.TryGround`; it adds one integer increment per sampled root and no allocations/draws/topology. The shader-clock cleanup removes CPU time publication. Final player evidence must confirm no floating grass, unchanged density/leakage, visible blades and wind, and acceptable runtime; CPU-ms/GPU-ms/memory/build-time dimensions not emitted by the harness remain explicitly unavailable.

## Remaining gates
Implement/root-test the exposed-face correction, refresh current master, review feature-only diff, then submit one exact-SHA request on the assigned `ci-test/fixes/agent-5` mailbox only while idle. Require green focused production-scene regression + exact built-player replay and direct late-frame proof that blade silhouettes change. Then record durable verification/cost, complete all checkboxes and pending metadata, move open→pending→closed, merge current master, and publish the exact feature head to `origin/master` non-force.
