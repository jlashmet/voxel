# Plan

## Observed gap / acceptance
`captures` is empty, so the feature note is the repro contract. Closure requires the authoritative Kentridge macro graph to become a deterministic physical world: grounded settlements, continuous terrain-aware hard routes, reusable geographic constraints, substantial lake/ridge geography, CharacterMotor traversal, built-player evidence, and measured cost. `SceneIssues/feature-readme.md` is absent on the feature branch, so canonical `SceneIssues/README.md` governs the feature workflow.

## Hypotheses / results
1. **Scale up legacy markers/roads.** Rejected: it cannot express reusable water/barrier relationships, crossings/passes, generic settlement realization, or blocked-route rejection.
2. **Keep `TopDownWorldLayout` authoritative and add a reusable physical-plan layer.** Selected and implemented. Regions, route semantics, settlement envelopes, and deterministic terrain-aware solving resolve before voxel emission.
3. **Rossdam/Bandit failure was an oversized lake.** Rejected. The substantial modern lake genuinely grazes the verified Bandit corridor; an explicit dry-shore `GoAround` is the correct semantic contract and now has a corridor regression.
4. **Orc/ridge failure needed another pass or smaller ridge.** Rejected. The verified Orc branch only grazes the Logan ridge shoulder. Preserve the substantial ridge and use the existing `GoAround` semantic; implemented with a focused travel-margin regression.
5. **Run `33258816868` proved current master was independently broken.** Rejected after final diff audit. Agent-6 carried a stale pre-opening `StorySpecs.cs` overlay that removed exactly the condition/effect symbols reported missing by CI. Restoring current-master `StorySpecs.cs` (`3b34ab066a11c8af3f003a0559db3a4ea5357bba`) at `eb6b77cc13bf5b1850a81df9a73a6250f7d2ba5b` removes that file from the feature diff without altering opening-story code.

## Implemented direction / blast radius
- Required region vocabulary, deterministic extents/elevation, relationships, exclusions, crossing/pass/around semantics.
- Terrain-aware hard-route solver rejects unsolved blockers; production road rise acceptance is <=6 voxels per 30 dm step.
- Existing richer Kentridge/Hightown output remains; Moordell, Rossdam, Fairy Village, and Orc Village receive bounded streamed >=4-building blockouts and road arrivals.
- Rossdam is a carved streamed water basin; Logan uses the authored ridge pass; Bandit and Orc use explicit route-arounds.
- Production evidence mode uses the real motor/streamer and surveys settlements/geography. No second graph, eager destination hierarchy, planner weakening, or extra scene objects were introduced by the Orc repair.
- Final scope audit after the StorySpecs repair shows only agent-6 Kentridge/WorldBuilder/test/assignment files differ from current master; `.github/test-request.json` is not in the feature diff.

## CI state / remaining gate
Final target: `VoxelEngine.Tests.PlayMode.KentridgeMacroWorldPhysicalProductionAcceptanceTests.PhysicalMacroWorldHasWalkableRoutesAndADeepStreamedWaterBody`.

Runs `33232755172` and `33255557296` exposed the Bandit/lake and Orc/ridge semantic defects; both are repaired. Run `33258816868` exposed the stale `StorySpecs.cs` merge overlay before the Kentridge test executed; that product defect is repaired and documented in `experiment-004-stale-storyspecs-overlay.md`. Current master remains `ff781ed26b1d9182fa8cd76e2d2da08abfa3765c`, already integrated in the branch history.

Freeze the corrected feature head, issue the next exact-SHA request on the existing `ci-test/fixes/agent-6` transport, inspect focused + built-player evidence and budgets, complete every `tasks.md` check, then perform pending/closed bookkeeping, re-merge current master if it advances, and non-force promote the exact feature head to `master`.
