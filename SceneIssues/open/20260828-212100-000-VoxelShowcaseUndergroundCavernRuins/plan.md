# Plan

## Acceptance / current evidence
`VoxelShowcase` must progress from a natural daylight mouth through a long organically shaped walkable descent into a huge dark irregular cavern, then to an aged reachable ruin framed by exactly two grounded humanoid statues. Closure requires focused exact-SHA regression, real built-player traversal/capture, direct rendered review, and bounded generation/render/light cost.

Exact run `33284693031` on source `492ea820...` is functionally green: focused PlayMode passed, the standalone player completed waypoint 38/38 with zero harness assertions, and generation stayed inside budgets (`34,798,060` total writes, `4,416,056` naturalization writes across 215 nodes, `3,579,396` finish writes, 20 preloaded regions, 6 route / 8 total lights). Direct review of all seven captured frames still failed: the descent remains dominated by tall repeated ribs and planar ceiling bands, and the destination reads as a rectangular masonry throat rather than a huge irregular cavern with the ruin and both statues clearly composed.

## Competing hypotheses and discriminator
1. **Cadence-only naturalizer defect**: fixed 13-voxel sampling caused the ribs. This is falsified: the live branch already uses deterministic variable spacing plus side/upper lobes, its structural regression is green, and the exact built-player frames retain the same dominant shape.
2. **Primitive-topology defect**: the generic cave core carves a full-height rectangular cross-section at every route step; the opt-in naturalizer, doglegs, and destination circulation then union vertical cylinders whose walls remain vertical and whose tops remain flat. Source matches the rendered ribs/bands. This is now the leading cause.

Next discriminator/fix: keep the generic cave engine, renderer, materials, camera, capture helper, and public authoring API unchanged. In the reusable opt-in underground-cavern layer, replace visible vertical-cylinder passage finish with a deterministic stacked-disc rounded-vault profile. Its minimum radius/height must mathematically contain the existing rectangular gameplay core between adjacent nodes; radius must vary through the wall height and taper above clearance into a crown, so the core cannot remain the visible boundary. Reuse the same vault brush for full-route naturalization, dogleg carving, and destination circulation. Keep floor discs and CharacterMotor semantics unchanged.

Add a production-computation regression for the vault profile: deterministic radii, multiple wall-radius variants, guaranteed core cover across maximum node spacing, tapered non-planar crown, bounded slice/lobe count, plus the existing world generation, CharacterMotor, determinism, 15M naturalization-write, 55M total-write, and eight-light checks.

## Blast radius / remaining gates
Expected code blast radius: only `Game.Structures.Runtime` underground-cavern helpers and focused cavern tests; no generic cave/core, renderer, material, lighting, camera, workflow, or public-interface changes. Cost must be measured from the final exact run against the current green baseline above and prior render baselines (~256k-291k endpoint vertices, ~526k-591k indices, ~287-298 draws; transient ~1.1M vertices / 2.28M indices / 582 draws).

After code/tests are final, merge current master if advanced, reconcile only this assignment's premature concurrent close, issue one canonical request on `ci-test/fixes/agent-3`, inspect every built-player frame, and close only if every acceptance criterion is visually and functionally green.
