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

## Current root-cause isolation
A missing one-shot macro selection initially looked plausible, but source inspection falsified it: the actual playable compatibility `KentridgeDefinition.Build` already calls `TopDownWorldLayoutSelection.Select(...)` on the exact failing source. Hightown authoring and campaign planning do not consume the handoff.

Queued run `33485694443` tests only the shared legacy-caller selector contract and is non-authoritative for the player symptom; leave it untouched. Feature head now adds `PlayableCompatibilityAuthoringLeavesMacroSelectionForCatalogueBuild`, which invokes the internal playable adapters by reflection and requires Fairy/Orc definitions after the real authoring order. No production behavior has changed.

## Next gates
1. After `33485694443` is terminal, run the corrected real-caller discriminator on the same CI transport.
2. If it passes, isolate runtime region generation/publication for exact Fairy/Orc authored regions before any production fix. If it fails, identify the actual selector consumer from the managed assertion.
3. After a demonstrated fix, require exact built-player stored shells plus readable Moordell/Rossdam/Fairy/Orc settlements, lake/constrained route, ridge/pass, network, and CharacterMotor traversal.
4. Measure final route/water/feature/streaming/render/FPS/memory and vertical-residency blast radius.
5. Merge current master before final exact-SHA gates; close and non-force promote only after every checklist/acceptance item is green.