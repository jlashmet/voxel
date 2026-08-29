# Plan — Kentridge vegetation meadow density

## Scope and acceptance
Work only `20260829-015100-000-KentridgeVegetationMeadowDensity` on `fixes/agent-5`. Kentridge must use reusable WorldBuilder ecology policy, render one connected grass meadow with at least 3,000 blades, respect exclusions, and show plainly visible wind in a stationary built-player replay. Do not edit scene serialization or `.github/test-request.json` on the feature branch.

## Material evidence
- Reusable regional ecology policy is implemented through production terrain sampling; Kentridge allows only semantic Grass and no trees/ambient animals.
- Corrected run `33246401704` is green for the focused CPU clock regression and real-player harness. It reports 11,478 grass instances / 114,580 blades, 57,589 blades in the primary connected meadow, 8 chunks, and zero excluded-surface grass.
- Visual acceptance still fails: the complete grass/ground raster is byte-identical at 39.3s, 49.3s, and 59.3s while the sky visibly changes. This independently falsifies both custom `_GrassTime` publication attempts (shared material and per-draw MPB).
- The same built frames are a discriminator: `AuthoredSky.shader` animates visible clouds from Unity's engine-managed `_Time.y`, proving the player/render pipeline has a working GPU time source. The Kentridge scene does not pause Unity `timeScale`; its cutscene runtime simply remains on dialogue, so scaled-vs-unscaled CPU time was not causal.

## Selected correction
Remove custom grass clock plumbing and use the already-proven engine-managed shader clock `_Time.y` directly in `ProceduralVegetationGrass.shader`. Remove `_GrassTime` from the material CBUFFER/properties and remove the per-frame CPU material/MPB clock writes. Preserve the same spatial phases, amplitudes, packed meshes, camera-facing reconstruction, and character push. Keep a focused regression for packed GPU-only topology plus rely on the mandatory exact built-player frame comparison as the behavioral wind oracle.

## Blast radius / cost
Only shared packed Grass/Nettle presentation changes. Density/topology and draw count remain unchanged. This correction removes one per-frame time-source read, one material float write, one property-block clear/write, and the persistent grass time property block; no new allocation, GameObject, material, draw, CPU blade update, or mesh rebuild is introduced. Final player evidence must compare runtime with the prior capture; CPU-ms/GPU-ms/memory/build-time dimensions not emitted by the harness remain explicitly unavailable.

## Remaining gates
Commit the engine-clock correction and focused regression cleanup, refresh current master, then run the assigned exact-SHA targeted CI mailbox only when no request is queued/running. Require green focused CI + exact built-player Kentridge replay and direct late-frame proof that blade silhouettes change. Then record durable verification/cost, complete all checkboxes and pending metadata, move open→pending→closed, merge current master, and publish the exact feature head to `origin/master` non-force.
