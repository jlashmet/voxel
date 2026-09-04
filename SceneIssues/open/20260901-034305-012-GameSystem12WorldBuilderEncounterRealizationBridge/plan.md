# 12 WorldBuilder encounter realization bridge — implementation plan

**Ownership:** shared composition module `Assets/Game/Composition/EncounterRealization` (`Game.Composition.EncounterRealization`) plus the Kentridge consumer under `Assets/Game/Composition/Kentridge/Playable`. Do not create ceremonial Api/Runtime layers.

## Acceptance / architecture

Translate authored WorldBuilder site/NPC/spawn intent plus exact realized placement facts into `Encounters.Api` / `Characters.Api` bindings. Shared code remains place-agnostic and API-only; Kentridge owns named forest/formation policy. Encounter scene code must consume bridge output instead of recomputing generated placement.

## Selected approach

`EncounterRealizationComposer` accepts an `EncounterRealizationSpec` and an `IEncounterRealizationFacts` provider. It returns semantic success/failure plus exact site anchor and character bindings. WorldBuilder API was not widened because its existing semantic identities are sufficient; backend physical positions are adapted at composition boundaries. `KentridgeForestEncounterRealization` receives the exact selected `TopDownWorldLayout`, derives the forest physical anchor once, owns the three encounter-local formation slots, and feeds the shared composer. `KentridgeForestBanditEncounter` consumes the resulting bindings.

## Reuse / validation surfaces

- `Assets/Game/Composition/EncounterRealization` is pure headless composition: no meaningful scene behavior. Its module-local EditMode assembly is the required validation surface; no module-local scene applies.
- `Assets/Game/Composition/Kentridge/Playable` has player-visible/runtime behavior. It owns `Validation/KentridgeEncounterRealizationValidation.unity` plus the paired player scenario. The validation bootstrap supplies deterministic WorldBuilder input but invokes the real `KentridgeForestEncounterRealization` production adapter and shared composer; it does not reproduce bridge logic or presentation geometry.
- Repository-wide `KentridgePlayableSlice` remains the assembled integration gate and proves the production installer/character presentation path.

## Evidence / results so far

Two independently authored bridge fixtures, exact-placement reuse regressions, missing-realization diagnostics, Kentridge macro-layout replacement, and dependency-boundary coverage are implemented. Prior exact-SHA attempts found and fixed two scoped compile defects: the `EncounterRealization` namespace/type collision and the WorldBuilder `Campaign` factory symbol collision. Current `master` (`e27afc78bb47c2578fbd6b85d1604d588d78d854`) was merged into the feature branch by reconciliation merge `5a9fa033cd1dc373307fc5bb4a00716a3070519b`; the branch is now ahead of and not behind master.

## Blast radius / remaining gates

No world generator, encounter lifecycle, shared Runtime dependency, global budget, or unrelated assertion is changed. Remaining work: run convention-derived module tests, the Kentridge module-local standalone scene, and SceneIssue/Kentridge assembled exact-SHA validation; then complete closure bookkeeping, re-merge master if it advances, open the final PR, enable auto-merge, and require the PR `affected` gate to pass.
