# Plan

## Observed gap / acceptance
`captures` is empty, so the feature note is the repro contract. Closure requires the authoritative Kentridge macro graph to become a deterministic physical world: grounded settlements, continuous terrain-aware hard routes, reusable geographic constraints, substantial lake/ridge geography, CharacterMotor traversal, built-player evidence, and measured cost.

## Hypotheses / results
1. **Scale up legacy markers/roads.** Rejected: it cannot express reusable water/barrier relationships, crossings/passes, generic settlement realization, or blocked-route rejection.
2. **Keep `TopDownWorldLayout` authoritative and add a reusable physical-plan layer.** Selected and implemented. Regions, route semantics, settlement envelopes, and deterministic terrain-aware solving resolve before voxel emission.
3. **Rossdam/Bandit failure was an oversized lake.** Rejected. The substantial modern lake genuinely grazes the verified Bandit corridor; an explicit dry-shore `GoAround` is the correct semantic contract and now has a corridor regression.
4. **Orc/ridge failure needed another pass or smaller ridge.** Rejected. The verified Orc branch only grazes the Logan ridge shoulder. Preserve the substantial ridge and use the existing `GoAround` semantic; implemented with a focused travel-margin regression.

## Implemented direction / blast radius
- Required region vocabulary, deterministic extents/elevation, relationships, exclusions, crossing/pass/around semantics.
- Terrain-aware hard-route solver rejects unsolved blockers; production road rise acceptance is <=6 voxels per 30 dm step.
- Existing richer Kentridge/Hightown output remains; Moordell, Rossdam, Fairy Village, and Orc Village receive bounded streamed >=4-building blockouts and road arrivals.
- Rossdam is a carved streamed water basin; Logan uses the authored ridge pass; Bandit and Orc use explicit route-arounds.
- Production evidence mode uses the real motor/streamer and surveys settlements/geography. No second graph, eager destination hierarchy, planner weakening, or extra scene objects were introduced by the Orc repair.

## CI state / blocker
Final target: `VoxelEngine.Tests.PlayMode.KentridgeMacroWorldPhysicalProductionAcceptanceTests.PhysicalMacroWorldHasWalkableRoutesAndADeepStreamedWaterBody`.

Runs `33232755172` and `33255557296` exposed the Bandit/lake and Orc/ridge semantic defects; both are repaired. Current master `ff781ed26b1d9182fa8cd76e2d2da08abfa3765c` was merged at `379439a571b3e941ee9fc818c402fc49331ebf28`. Final request `33258816868` for source `afc684712447cf000996c310aa41e8e967fb5dc0` failed before the Kentridge test: current-master opening story/campaign code references missing story condition/effect API types/factories. The failing `StoryRuleEngine.cs` blob is identical on master and agent-6, so this is an external baseline product blocker, not an agent-6 defect or infrastructure retry.

## Remaining gate
Keep the feature open. Re-fetch master; only after the owning story/campaign compile fix lands, merge that current master state, issue a new exact-SHA final request, inspect focused + built-player evidence and budgets, complete all `tasks.md` checks, then perform pending/closed bookkeeping and non-force master promotion.
