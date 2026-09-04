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

## Evidence / results

Two independently authored bridge fixtures, exact-placement reuse regressions, missing-realization diagnostics, Kentridge macro-layout replacement, and dependency-boundary coverage are implemented. Prior exact-SHA attempts found and fixed two scoped compile defects: the `EncounterRealization` namespace/type collision and the WorldBuilder `Campaign` factory symbol collision.

The feature was reconciled with authoritative master twice as master advanced. The final pre-closure sync merged master `abe9602e1025b5e02f11a7aa6b8a5965aa1c7fe3` into the feature as two-parent merge `73741dc8532ee693f706ac8ec528a1f486d9187f`. Exact request `3eab3fa4b08a96d9a43b1f8018ea930c9365c162`, whose parent is exactly that feature source, completed successfully in workflow `33836877402`. Automatic required module validation passed, the module-local Kentridge player validation passed through repository discovery, and the SceneIssue standalone player replay passed with screenshot/artifact publication and final commit status succeeding.

## Blast radius / closure state

No world generator, encounter lifecycle, shared Runtime dependency, global budget, or unrelated assertion is changed. Exact-SHA feature validation is complete at `73741dc8532ee693f706ac8ec528a1f486d9187f`; all feature acceptance is satisfied. Remaining repository integration work after this bookkeeping commit is the final pull request, required `affected` gate, auto-merge, and verification that this closed SceneIssue is visible on `origin/master`.
