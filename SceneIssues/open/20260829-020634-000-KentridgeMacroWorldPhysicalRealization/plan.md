# Plan

## Observed gap / acceptance
`captures` is empty, so the feature note is the repro contract. Closure requires the authoritative Kentridge macro graph to become a deterministic physical world: grounded settlements, continuous terrain-aware hard routes, reusable geographic constraints, substantial lake/ridge geography, CharacterMotor traversal, built-player evidence, and measured cost. `SceneIssues/feature-readme.md` is absent, so canonical `SceneIssues/README.md` governs.

## Hypotheses / results
1. **Scale up legacy markers/roads.** Rejected: no reusable geography relationships/crossings, generic settlement realization, or blocked-route rejection.
2. **Keep `TopDownWorldLayout` authoritative and add a reusable physical-plan layer.** Selected/implemented: regions, route semantics, settlement envelopes, and deterministic terrain-aware solving resolve before voxel emission.
3. **Rossdam/Bandit failure was only an oversized lake.** Rejected. The substantial lake genuinely grazes the verified corridor; explicit dry-shore `GoAround` is now authored/regressed.
4. **Orc/ridge failure needed another pass/smaller ridge.** Rejected. The Orc branch only grazes the Logan ridge shoulder; explicit `GoAround` preserves the ridge and has a travel-margin regression.
5. **Run `33258816868` proved master was broken.** Rejected. Final diff audit found stale agent-6 `StorySpecs.cs`; restoring the current-master blob at `eb6b77cc13bf5b1850a81df9a73a6250f7d2ba5b` removed the exact missing APIs and the file from the feature diff.
6. **Run `33259572439` was closure-quality because both workflow steps were green.** Rejected after artifact inspection. Logic + built player were green, but the 60s durable evidence reached only macro-road/Moordell; the ~44s opening left insufficient time for Rossdam, Fairy Village, Orc Village, lake, ridge/pass, and overview captures.

## Implemented direction / blast radius
- Shared physical region vocabulary/constraints, terrain-aware route solver, reusable settlement blockouts, continuous roads, carved Rossdam basin, Logan pass, Bandit/Orc route-arounds.
- Existing richer Kentridge/Hightown output remains; no second graph, planner weakening, eager destination hierarchy, or direct Kentridge-only voxel path.
- Evidence-only repair at `9d6f4595ddf96c57bcd747b8d223f986aec33699`: while the normal opening runs unchanged, the validation profile pre-generates only named evidence camera/focus regions through the existing `ShowcaseWorld`, then shortens local/road traversal and target dwell so all seven named captures fit the fixed 60s harness. Ordinary gameplay pays zero cost because the driver installs only for `kentridge-macro-world` validation.

## CI / remaining gate
Target: `VoxelEngine.Tests.PlayMode.KentridgeMacroWorldPhysicalProductionAcceptanceTests.PhysicalMacroWorldHasWalkableRoutesAndADeepStreamedWaterBody`.

Run `33259572439` proved the focused acceptance and built player are functionally green: `regions=6`, `settlements=6`, `buildings=16`, `hardRoutes=20`, `routeTiles=833`, `constrainedRoutes=5`, `solveSteps=1108`, max road rise `2` voxels/30dm, water depth `46` voxels; CharacterMotor traversed both local and macro-road segments. Final evidence still requires the new timing source to produce/inspect every named screenshot plus budget telemetry. Then complete all `tasks.md`, pending/closed bookkeeping, refresh master, and non-force promote the exact feature head.
