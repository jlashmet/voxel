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
- Exact production-planning discriminator run `33503231955` is green on source `e43c40579563cc72ded9027aadd1c485d5526475`: the shipped Kentridge authoring -> Hightown authoring -> opening campaign planning -> settlement/hidden-space `KentridgeCombinedVoxelCatalogue.Build(...)` sequence retains Fairy and Orc macro definitions, and automatic module validation also passes.
- Prior `PlayableCatalogueRetainsFairySettlementAfterHightownAndCorridorCombine` proves final Kentridge+Hightown+corridor combination, `ShowcaseWorld.ConfigureGeneratedContentForGameplay`, signed negative-Z streaming, and storage preserve macro content when macro input is present.

## Root cause and minimal fix under validation
Source inspection found the first present->absent ownership transition. `KentridgeDefinition.Build` publishes the top-down world through the one-shot `TopDownWorldLayoutSelection`, but shipped `KentridgePlayableSlice.OnEnable` constructed `ShowcaseWorld` before building its intended Kentridge/Hightown/corridor catalogue. `ShowcaseWorld` immediately builds a temporary `ShowcaseCatalogue`; that path authors Kentridge through `WorldBuilderVoxelCatalogue`, reaches `KentridgeCombinedVoxelCatalogue.Build`, and consumes the one-shot macro selection. The playable slice then builds its real catalogue without the macro content and immediately replaces/disposes the temporary bootstrap catalogue. This exactly explains the built player's local-only 434-definition catalogue.

The product fix is intentionally scene-composition-local: build/combine the intended playable catalogue before constructing `ShowcaseWorld`, so the intended Kentridge catalogue consumes the one-shot selection first. Shared WorldBuilder and macro-catalogue semantics remain unchanged. Commit `7cbef3ac...` contains only that lifecycle ordering change; `085c976f...` updates the focused ownership regression and restores the required macro API imports.

The previous focused CI request `33522587461` failed before product execution because the then-current regression fixture omitted `Game.WorldBuilder.Macro.Api` / `.Runtime` imports. Treat it as a test-harness compile failure, not product evidence. Those imports are corrected on the feature branch.

## Next gates
1. Run exact-SHA focused PlayMode validation for `KentridgeMacroWorldBootstrapOwnershipTests`; require generated settlement street/building definitions, planned route definitions, and graph reachability for every macro settlement.
2. Re-run the existing built-player runtime catalogue/storage evidence on the same corrected product path. Require macro definitions to survive into the shipped world and authored Moordell/Rossdam/Fairy/Orc shell/roof probes to read non-air.
3. Capture readable settlement, lake/constrained-route, ridge/pass, network, and CharacterMotor traversal evidence from the supported built-player runner.
4. Measure final route/water/feature/streaming/render/FPS/memory and vertical-residency blast radius.
5. Merge current master before final exact-SHA gates; close and non-force promote only after every checklist/acceptance item is green.
