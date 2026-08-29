# Plan — Kentridge vegetation meadow density

## Scope
Implement only `20260829-015100-000-KentridgeVegetationMeadowDensity` on `fixes/agent-5`. Do not edit scene serialization or `.github/test-request.json` on the feature branch.

## Confirmed current architecture / evidence
- `KentridgeDefinition` already exposes an additive `RegionEcologyPolicy` with region identity, grass/undergrowth multipliers, palette metadata, meadow radius, route clearance, and maximum slope.
- `KentridgeRegionLife` realizes that policy through the existing point-cloud / surface-sampling vegetation path and keeps the work in Kentridge composition rather than scene-local grass GameObjects.
- The current procedural renderer is a packed blade-mesh renderer: one semantic `VegetationInstance` expands deterministically to 5–15 visible grass blades and blade meshes are split at `MaxBladesPerChunk = 36000`. The older 1,023-matrix batching notes are stale.
- The current Kentridge diagnostic reports semantic grass-instance count, not rendered blade count, and hard-codes `excludedPlacements=0`; neither is sufficient durable proof for the assignment.
- Current runtime surface rejection covers built content, route clearance, a river/water-side band, failed samples, and slope, but those exclusion classes are not all expressed by the reusable ecology policy.

## Competing hypotheses / discriminator
1. **Renderer capacity is the density bottleneck** — rejected: packed grass chunks already support tens of thousands of blades without a renderer rewrite.
2. **Regional placement/policy is the density bottleneck** — supported: density and area selection belong at the regional ecology seam and Kentridge already routes through it.
3. **The acceptance count is already proven** — rejected: 3,000 semantic instances are not the same metric as rendered blades; the renderer expands each seed to 5–15 blades.
4. **Invalid-surface proof is complete** — rejected: the diagnostic currently prints zero without measuring post-policy leakage, and the reusable policy does not encode every required exclusion class.
5. **Wind needs a Kentridge-specific shader change** — unproven. Preserve the shared renderer/material wind path and only modify production wind code if exact built-player evidence shows motion is absent.

## Implementation strategy
1. Keep `RegionEcologyPolicy` additive but give it an explicit meadow-surface exclusion contract covering building/structure, path/route, cultivated, water/wet, steep/cliff, and other-invalid classes.
2. Make Kentridge sampling classify each rejected candidate and ask the policy whether the surface is eligible; do not create a Kentridge-only shader, material, or scene scatter path.
3. Extract the renderer's deterministic seed-to-blade-count contract into reusable rendering API code and have `ProceduralGrassBatch`, Kentridge diagnostics, and tests share that exact calculation.
4. Report both semantic placement count and renderer-equivalent visible blade count. The acceptance threshold is `>= 3,000 rendered blades`; retain semantic count for cost visibility.
5. Replace hard-coded exclusion success with measured exclusion diagnostics and explicit per-class rejection/leakage evidence.
6. Keep the existing shared wind path. Add regression/diagnostic evidence that Kentridge grass carries nonzero bend and the shared render configuration supplies wind; built-player time-separated frames remain the authoritative visual gate.

## Regression strategy
- Prove Kentridge exposes a denser-than-baseline regional ecology profile and palette.
- Prove the policy rejects building, path, cultivated, water, steep/cliff, and other-invalid surfaces while allowing valid meadow ground.
- Run deterministic point-cloud placement through the production placement API and assert one connected Kentridge meadow reaches `>= 3,000` renderer-equivalent blades with zero placements on excluded samples.
- Prove density scaling and undergrowth synthesis remain deterministic.
- Prove the grass renderer's blade-count helper exactly drives the packed renderer path and remains bounded at 5–15 blades per semantic grass instance.
- Preserve existing packed-chunk/high-density renderer regressions; do not reintroduce assumptions about the removed 1,023-instance path.

## Blast radius / cost
- World-builder change remains additive; non-Kentridge regions retain existing behavior unless they opt into the ecology policy.
- Runtime realization remains scoped to the Kentridge playable slice plus one shared renderer counting helper used by the existing renderer itself.
- Current Kentridge density may render roughly 5–15× the semantic instance count. Record the exact deterministic blade count and resulting chunk count before closure; `36000` blades/chunk is the existing renderer ceiling per mesh chunk.
- No new per-frame allocations, material churn, grass GameObjects, scene serialization, or shader fork should be introduced.

## Validation / workflow
1. Keep `plan.md` and `tasks.md` current as required work is discovered.
2. Run focused regressions plus canonical pre-merge scene/module gates and generate required feature artifacts.
3. Validate the exact Kentridge scene in the built application: usable startup, dense meadow/player-height view, diagnostic `>= 3,000` rendered blades, zero excluded-surface leakage, and time-separated stationary frames showing wind motion.
4. Move only this assignment `open -> pending` with implementation/test/evidence metadata after implementation validation.
5. Request exactly one final targeted CI run on `ci-test/fixes/agent-5`; never place `.github/test-request.json` on the feature branch.
6. Require green CI for the exact feature SHA. If the feature SHA changes, re-run gates and use a fresh final request only as allowed by the repository workflow.
7. Complete pending metadata/evidence, move `pending -> closed`, set `status=fixed` and `resolvedUtc` only after every checkbox/acceptance criterion is satisfied.
8. Merge current `origin/master` into `fixes/agent-5`; if master advances, fetch/merge/retry; then non-force publish that exact feature head to `origin/master` and verify equality.
