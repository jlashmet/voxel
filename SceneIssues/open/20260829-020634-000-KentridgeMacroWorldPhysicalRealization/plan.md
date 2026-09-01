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

## Current root-cause isolation
The synchronous production path through the exact Kentridge catalogue overload is now proven correct. Hightown authoring is not the consumer, campaign planning does not drain the handoff, and the settlement/hidden-space overload returns macro content under the same production sequence.

The prior `PlayableCatalogueRetainsFairySettlementAfterHightownAndCorridorCombine` also proves final Kentridge+Hightown+corridor combination, `ShowcaseWorld.ConfigureGeneratedContentForGameplay`, signed negative-Z streaming, and storage preserve macro content when macro input is present. Therefore another generic combine/configure test would duplicate already-green evidence.

The remaining discrepancy is player lifecycle/ownership between the synchronous catalogue return and the built-player runtime diagnostic: duplicate `KentridgePlayableSlice` enablement, another one-shot consumer, deferred presentation lifecycle, or diagnostic/instance ownership could explain why the player reports a local-only 434-definition catalogue while focused production sequencing is green. Isolate that exact player-only boundary before any product fix.

## Next gates
1. Inspect exact feature source and scene composition for `KentridgeTopDownWorldLayoutPresentation` lifecycle, every `TopDownWorldLayoutSelection.TryConsume`/Kentridge catalogue consumer, the `runtime-catalogue` diagnostic, and all `KentridgePlayableSlice` instances/enable paths.
2. Add the smallest lifecycle/duplicate-consumer regression or diagnostic that discriminates the first proven player-only owner; do not make another product fix until this minimal repro identifies it.
3. After a demonstrated product fix, require exact built-player macro definitions, authoritative stored shell/roof probes, and readable Moordell/Rossdam/Fairy/Orc settlements plus lake/constrained route, ridge/pass, network, and CharacterMotor traversal.
4. Measure final route/water/feature/streaming/render/FPS/memory and vertical-residency blast radius.
5. Merge current master before final exact-SHA gates; close and non-force promote only after every checklist/acceptance item is green.
