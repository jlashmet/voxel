# Plan

## Observed gap / acceptance
`captures` is empty, so the feature note is the repro contract. Closure requires the authoritative Kentridge macro graph to drive deterministic physical settlements, continuous terrain-aware hard routes, reusable geographic constraints, substantial lake/ridge geography, CharacterMotor traversal, closure-quality built-player evidence, and measured cost. `SceneIssues/feature-readme.md` is absent, so canonical `SceneIssues/README.md` governs.

## Hypotheses / results
1. **Scale up legacy markers/roads.** Rejected: no reusable geography constraints/crossings, generic settlement realization, or blocked-route rejection.
2. **Keep `TopDownWorldLayout` authoritative and add a reusable physical-plan layer.** Selected/implemented: deterministic regions, route semantics, settlement envelopes, and terrain-aware solving resolve before voxel emission.
3. **Rossdam/Bandit and Orc/ridge failures required weaker geography.** Rejected. Preserve substantial geography and author explicit semantic `GoAround` solutions; both have focused production regressions.
4. **Run `33258816868` proved master was broken.** Rejected. Agent-6 carried stale `StorySpecs.cs`; restoring current-master bytes removed the missing APIs and that file from the feature diff.
5. **Green workflow alone proves visual acceptance.** Rejected twice. `33259572439` lacked time for most named captures; `33260139560` emitted all files but captured remote targets while renderer `coverage=False`. Earlier 2.4s Moordell evidence visibly contains its blockout, discriminating convergence from missing content (`experiment-005-remote-evidence-convergence.md`).

## Implemented direction / blast radius
- Shared region vocabulary/constraints, terrain-aware solver, reusable settlement blockouts, continuous roads, carved Rossdam basin, Logan pass, Bandit/Orc route-arounds; richer Kentridge/Hightown remain intact.
- No second graph, planner weakening, eager destination hierarchy, or Kentridge-only direct voxel path.
- Current validation-only convergence fix `e382527d2ffd1f14f73aabf6a1e241054f14b72c`: only the `kentridge-macro-world` evidence driver temporarily runs the opening timeline at 4x, restores the original `Time.timeScale` before CharacterMotor movement and teardown, prewarms named regions through the existing `ShowcaseWorld`, restores >=2.4s target dwell, and captures only after `RenderingComposition.HasCompletePublishedNearSurfaceCoverage()`. Normal gameplay does not install this driver.
- Current master remains `ff781ed26b1d9182fa8cd76e2d2da08abfa3765c`, already integrated; feature diff remains scoped to agent-6 Kentridge/WorldBuilder/tests/assignment files with no `.github/test-request.json`.

## Remaining gate
Run exact target `VoxelEngine.Tests.PlayMode.KentridgeMacroWorldPhysicalProductionAcceptanceTests.PhysicalMacroWorldHasWalkableRoutesAndADeepStreamedWaterBody` from a request commit whose parent is the final source SHA. Inspect every named built-player frame, traversal/runtime logs, and cost telemetry. Only after every `tasks.md` item/acceptance is supported: complete pending metadata, move open->pending, then pending->closed with `resolvedUtc`, refresh master, verify exact head, and non-force promote to `master`.