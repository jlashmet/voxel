# Plan — Kentridge vegetation meadow density

## Scope and acceptance
Work only `20260829-015100-000-KentridgeVegetationMeadowDensity` on `fixes/agent-5`. Kentridge must use reusable WorldBuilder ecology policy, render one connected grass meadow with at least 3,000 blades, place no meadow grass on excluded surfaces, and show plainly visible wind in a stationary built-player replay. Do not edit scene serialization or `.github/test-request.json` on the feature branch.

## Material evidence
- Reusable regional ecology policy is implemented through production terrain sampling; Kentridge allows only semantic Grass and no trees/ambient animals.
- Exact-player run `33244533044` reports 11,478 semantic grass instances / 114,580 rendered blades, 57,589 blades in the largest connected meadow, 8 packed chunks, and zero excluded-surface leakage. The focused MPB-clock test and built-player harness are green.
- Human inspection still fails animation: grass/ground pixels at 39.9s, 49.9s, and 59.9s are exactly identical while sky pixels change. The shader's 0.82/0.46/1.06 rad/s wave terms cannot all alias at the 10-second capture cadence, so the MPB-only hypothesis is falsified.
- The remaining shared material path in `ProceduralVegetationMaterials.ApplyGrassState()` publishes `_GrassTime` from scaled `Time.time`. The stationary replay is in paused dialogue/cutscene gameplay, which explains the frozen visible material clock even though the application continues rendering.

## Selected correction
Keep the packed GPU deformation and existing MPB submission, but change the shared production material `_GrassTime` publication from `Time.time` to `Time.unscaledTime`. Add a PlayMode regression that sets `Time.timeScale = 0`, advances real frames, republishes production grass state, and proves the shared material clock advances while scaled gameplay time stays fixed. This is a one-line behavioral change plus test; no shader fork, mesh rebuild, or new animation system.

## Blast radius / cost
Shared packed Grass/Nettle presentation is affected. Density/topology are unchanged. The material already writes one float per frame; the correction only changes its clock source from scaled to unscaled time. The retained MPB remains one persistent block per batch with no per-frame allocation. No new GameObjects, materials, mesh rebuilds, per-blade CPU updates, or render draws are introduced. Exact CPU-ms/GPU-ms/memory/build-time counters are not emitted by the current harness and must not be invented.

## Remaining gates
The one allowed final CI transport has already been consumed by run `33244533044`; do not update `ci-test/fixes/agent-5` again under the current instruction. Keep the issue open until an authorized exact-SHA focused regression + built-player replay proves changed grass silhouettes at a stationary camera, then record passing verification/cost, complete metadata/checklists, move open→pending→closed, merge current master, and publish the exact feature head to `origin/master` non-force.
