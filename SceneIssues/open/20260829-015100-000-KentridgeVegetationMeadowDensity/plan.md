# Plan — Kentridge vegetation meadow density

## Scope and acceptance
Work only `20260829-015100-000-KentridgeVegetationMeadowDensity` on `fixes/agent-5`. Kentridge must use reusable WorldBuilder ecology policy, render one connected grass meadow with at least 3,000 blades, place no meadow grass on excluded surfaces, and show plainly visible wind in a stationary built-player replay. Do not edit scene serialization or `.github/test-request.json` on the feature branch.

## Material evidence
- Additive regional ecology policy is already implemented and realized through production terrain sampling; Kentridge currently allows only grass and no trees/ambient animals.
- Prior exact-player replay `33242524673` reported 11,478 semantic grass instances / 114,580 rendered blades, with 57,589 blades in the largest connected meadow, 8 packed chunks, and zero excluded-surface leakage.
- Human inspection falsified the earlier “source time plumbing is sufficient” hypothesis: late stationary meadow frames are pixel-identical while the sky changes.
- The grass shader is compiled/included and deforms vertices from `_GrassTime`. `ProceduralVegetationBatchRenderer.DrawNow()` resubmits packed grass every frame.
- Root cause is the queued `Graphics.DrawMesh` state boundary: wind time was mutated on a shared material before deferred rendering. Unity documents that queued draws should use `MaterialPropertyBlock` when per-draw properties must be snapshotted.

## Selected fix
`ProceduralGrassBatch.Draw()` now reuses one persistent property block, snapshots an unscaled presentation clock into `_GrassTime`, and passes that block directly to every packed-grass draw. Mesh construction, batching, shader deformation, and character interaction stay unchanged. A PlayMode regression injects two clock values across frames through the actual `Draw()` path and proves topology is unchanged without a rebuild.

## Blast radius / cost
Shared packed grass is affected wherever semantic Grass/Nettle uses this renderer. Added steady-state CPU work is one time-source read, one property-block clear, and one float write per grass batch draw; the property block is allocated once with the batch. There are no new GameObjects, materials, mesh rebuilds, per-blade CPU updates, or per-frame managed allocations. Final player evidence must confirm runtime remains acceptable relative to the previous ~110 FPS / 114,580-blade capture.

## Remaining gates
Refresh/merge current `origin/master`, then issue exactly one final targeted request through `ci-test/fixes/agent-5` for the wind regression plus 60-second Kentridge replay. Require green exact-SHA regression and built-player harness, manually inspect multiple stationary late frames for changed grass silhouettes, record durable final evidence/cost, complete metadata/checklists, move open→pending→closed in repository order, merge current master again, and publish the exact feature head to `origin/master` non-force.
