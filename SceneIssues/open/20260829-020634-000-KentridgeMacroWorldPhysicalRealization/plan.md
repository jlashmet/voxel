# Plan

## Observed gap / acceptance
`captures` is empty, so the feature note is the repro contract. Closure requires the authoritative Kentridge macro graph to drive deterministic physical settlements, continuous terrain-aware hard routes, reusable geographic constraints, substantial lake/ridge geography, CharacterMotor traversal, closure-quality built-player evidence, and measured cost. `SceneIssues/feature-readme.md` is absent, so canonical `SceneIssues/README.md` governs.

## Hypotheses / results
1. **Scale up legacy markers/roads.** Rejected: no reusable geography constraints/crossings, generic settlement realization, or blocked-route rejection.
2. **Keep `TopDownWorldLayout` authoritative and add a reusable physical-plan layer.** Selected/implemented: deterministic regions, route semantics, settlement envelopes, and terrain-aware solving resolve before voxel emission.
3. **Rossdam/Bandit and Orc/ridge failures required weaker geography.** Rejected. Preserve substantial geography and author explicit semantic `GoAround` solutions; both have focused production regressions.
4. **Run `33258816868` proved master was broken.** Rejected. Agent-6 carried stale `StorySpecs.cs`; restoring current-master bytes removed the missing APIs and that file from the feature diff.
5. **Green workflow alone proves visual acceptance.** Rejected. `33259572439` lacked time; `33260139560` captured at `coverage=False`; `33260866388` was coverage-gated but still lost remote settlement feature meshes and timed out before lake/ridge/overview.
6. **Remote blocking prewarm helps later evidence.** Rejected by same-camera comparison: no-prewarm artifact `9716890862` visibly contains the Moordell building, while prewarmed `9717246958` does not. See `experiment-006-evidence-prewarm-lifecycle.md`.

## Implemented direction / blast radius
- Shared region vocabulary/constraints, terrain-aware solver, reusable settlement blockouts, continuous roads, carved Rossdam basin, Logan pass, Bandit/Orc route-arounds; richer Kentridge/Hightown remain intact.
- No second graph, planner weakening, eager destination hierarchy, or Kentridge-only direct voxel path.
- Current evidence-only repair `989e626161e4c18e815f449e9cdd37bf9c49c276`: remove remote `GenerateRegionBlocking` prewarm entirely, retain the validation-only 4x opening timeline, restore normal time before CharacterMotor movement/teardown, keep >=2.4s + published-near-coverage gating, and order adjacent evidence as Rossdam->lake and Orc->ridge to reuse normal resident streaming. Production world generation/streaming is untouched.
- Current master `ff781ed26b1d9182fa8cd76e2d2da08abfa3765c` was already integrated before this validation-only repair; final master/scope refresh remains required before promotion.

## Remaining gate
Freeze the final ledger head and run `VoxelEngine.Tests.PlayMode.KentridgeMacroWorldPhysicalProductionAcceptanceTests.PhysicalMacroWorldHasWalkableRoutesAndADeepStreamedWaterBody` through the existing CI transport. Require all seven named target frames plus road traversal, visually inspect settlements/lake/ridge/network, inspect runtime exceptions and cost telemetry, and complete every `tasks.md` item before open->pending. Only then perform pending->closed metadata, refresh master, verify exact branch head, and non-force promote to `master`.