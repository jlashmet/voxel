# Plan

## Acceptance authority
`issue.json` is the contract (`captures: []`). Preserve the source-backed Mounting Force graph while delivering physical settlements, terrain-aware hard routes, reusable geography, Rossdam Lake, Southern Ridge/pass, CharacterMotor traversal, durable built-player evidence, and bounded cost. Follow `AGENTS.md`, `SceneIssues/README.md`, and `feature-readme.md`.

## Proven results
- Production physical planning covers 20 hard routes, 6 settlements, 16 generic buildings, deterministic geography, constrained-route solutions, lake/ridge realization, bounded slopes, storage, and feature-aware vertical residency/readiness.
- Shared spatial reservations retain planner ownership; independent non-Kentridge conflict fixture passed (`33441865025`).
- Exact source `b500683...` passed focused storage/module/player validation (`33464366092` attempt 3).
- Exact source `3e34713...` completed all seven 180-second evidence targets (`33469175243`), but visual settlement/geography quality was insufficient.
- Signed negative-Z storage streaming passed (`33474641146`); playable three-catalogue combine retained Fairy timber (`33476499718`).
- Exact source `fb4bc6d...` passed focused storage + 180-second player (`33480730488`), yet corrected authored wall/roof probes read `material=0` at every Moordell/Rossdam/Fairy/Orc capture. Full-resolution Fairy/Orc frames contain no four-building blockout. Camera/readiness/final-combine hypotheses are falsified.
- Shared selector contract run `33485694443` is green, confirming the seed catalogue requires and consumes the explicit semantic handoff.
- Corrected real-caller discriminator run `33495686226` passed the requested PlayMode test and repository-derived module validation on exact source `1e7bb5f8...`; its standalone replay did not run because the request used a bare SceneIssue id.
- Corrected runtime-trace run `33500236600` is fully green on exact source `91997c77e7b4218bc2c2c2877526211e107f80b0`: focused production storage, module validation, and 60-second standalone replay all passed. The player logs `runtime-catalogue definitions=434` while Fairy, Orc, Moordell, Rossdam, roads, ridge, and water are all absent (`0/0/0@none`) before remote generation. This selects catalogue composition/handoff as the failing boundary, not evaluator, rasterizer, region publication, or later storage overwrite.

## Current root-cause isolation
The existing green `PlayableCompatibilityAuthoringLeavesMacroSelectionForCatalogueBuild` already mirrors Kentridge then Hightown authoring, so Hightown does not consume the one-shot macro selection. Campaign planning does not directly access `TopDownWorldLayoutSelection`.

The remaining production-only delta is the catalogue overload: the green discriminator consumes through `KentridgeCombinedVoxelCatalogue.Build(seed, settings, allocator)`, while shipped `KentridgePlayableSlice.OnEnable` consumes after real opening campaign planning through `Build(settlement, settings, generation.HiddenSpaces, allocator)`.

Feature head `48614eb3fba318e05d734cb9a698053d0ba41d57` adds `PlayableProductionPlanningLeavesMacroSelectionForGeometryCatalogueBuild`, mirroring Kentridge authoring -> Hightown authoring -> real opening campaign planning -> exact settlement/hidden-space catalogue build. No production behavior changed.

## Next gates
1. Run the new exact production-planning discriminator on the sole `ci-test/fixes/agent-6` transport without replacing any queued/running request.
2. If it fails, use its first failing assertion as the minimal repro/root cause and fix only that demonstrated handoff/overload defect. If it passes, isolate the final Kentridge+Hightown+corridor composition/configure boundary before any product fix.
3. After a demonstrated product fix, require exact built-player macro definitions, authoritative stored shell/roof probes, and readable Moordell/Rossdam/Fairy/Orc settlements plus lake/constrained route, ridge/pass, network, and CharacterMotor traversal.
4. Measure final route/water/feature/streaming/render/FPS/memory and vertical-residency blast radius.
5. Merge current master before final exact-SHA gates; close and non-force promote only after every checklist/acceptance item is green.
