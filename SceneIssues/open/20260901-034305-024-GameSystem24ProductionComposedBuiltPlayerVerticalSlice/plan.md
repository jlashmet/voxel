# 24 Production-composed built-player vertical slice — implementation plan

**Target ownership:** Kentridge production composition plus shared standalone-player validation. No new generic gameplay API/Runtime module.

## Observed baseline / acceptance

Current `master` already has System 14 session orchestration, but Kentridge is not yet a production-composed application entry. `KentridgePlayableSlice` directly creates `KentridgeSessionRuntimeGraphFactory`/`GameSessionOrchestrator`; `KentridgeForestBanditEncounter` still creates `InputContextService`, `UnityPlayerInputReader`, `VitalityRegistry`, `CombatService` and `EncounterRegistry`; `KentridgeWellQuestInventoryPresentation` owns another `InputContextService`, reflects private slice fields, and polls legacy input; `KentridgeUnityInputBridge` intentionally preserves raw `UnityEngine.Input` polling. System 23 Application is still open on `master`, and `Assets/Game/Application` does not exist yet.

Acceptance remains: FrontEnd -> New Game -> `GameplayReady` -> real movement/interaction/progression/encounter/combat/loot/presentation -> save -> ordered teardown -> Continue -> restored live gameplay, all through production public boundaries and the shared built-player harness.

## Hypotheses / discriminating result

1. **System 14 already eliminated alternate Kentridge runtime ownership.** Falsified: the forest extension and well presentation still own private runtime/input services, and the playable slice still owns startup.
2. **A thin Kentridge consumer can satisfy #24 once #23 lands, without another gameplay runtime.** Selected. Discriminating gate after #23 merges: Kentridge must compile with Application owning lifecycle/NewGame/Continue and #14 owning session graph construction, while repository search finds no scene-local authority or raw-input fallback.

## Canonical composition

Application (#23) owns Boot/FrontEnd/NewGame/Continue/Leave and the production local input/navigation lifetime. It delegates run creation/restore/teardown to SessionOrchestration (#14) and Persistence (#16). Kentridge supplies only world seed/content/sites/NPC/cutscene/placement policy and a Kentridge session-graph/content factory. Unity-bound Kentridge adapters receive public production capabilities from that composed graph; they do not construct authority. Forest encounter code retains authored realization/proximity/presentation only. Well/inventory presentation binds through explicit read-only composition capabilities, never reflection/private fields.

## Blocker / next work

**External prerequisite:** SceneIssue 23 is still `open/` on current `origin/master`; its Application API/runtime and Input-System migration are binding dependencies for T24-003/004/010/020-024/035. Do not implement #23 inside #24. Continue independent Kentridge cleanup only where it cannot create a parallel lifecycle/input authority. After #23 lands, merge current master, wire the canonical consumer, add milestone-driven shared-harness proof, run exact-SHA CI, inspect built-player evidence, then close only after every task passes.
