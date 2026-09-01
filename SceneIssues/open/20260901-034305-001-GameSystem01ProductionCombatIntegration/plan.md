# 01 Production combat integration — implementation plan

**Target ownership:** extend existing `Game.Combat.Api` / `Game.Combat.Runtime`; add only thin composition adapters where Encounters/Characters/Vitality meet Combat. Do **not** create a second combat runtime.

## Dependencies

02 Vitality, 03 Characters, 05 Encounters, existing `Game.Input.Api/Runtime` migration.

## Current baseline inventory

- `Game.Combat.Runtime.CombatService` historically owned life state through private participant HP. The new `CombatService(IVitalityService)` path delegates character-backed current/alive state and accepted damage to `Game.Vitality.Api`; the parameterless legacy path remains temporarily for unmigrated Kentridge/tests.
- `KentridgeForestBanditEncounter` remains a scene-local composition/runtime owner: it constructs Input/Combat services, creates participant identities by hand, starts combat, and settles scene state.
- `Game.Composition.Kentridge.Playable.asmdef` still directly references `Game.Combat.Runtime` and `Game.Input.Runtime`; final composition migration waits for publication/injection of the real Vitality Runtime and T01-015.
- `Assets/Game/Composition/CombatEnvironmentRuntime` is the separate older `MountingForce.CombatPrototype` experiment and remains out of scope unless a demonstrated acceptance defect requires it.
- `Game.Input.Api` is already semantic and `CombatInputController` has no raw key/button polling.
- Production Characters and Encounters are present. `Game.Characters.Api` owns `CharacterId`; `Game.Encounters.Api` owns Encounter identity, membership, activation, queued combat requests, and terminal Encounter resolution consumption.
- Current `master` `1b6d5db96ea150bd0cb573bfaff7e220f19afbeb` still lacks `Assets/Game/Vitality`, but `fixes/agent-9` already contains completed System 02 API/Runtime/tests. System 02's own task log identifies a publication-order deadlock: it waits for System 01's character-backed participant contract, while System 01 was waiting for Vitality publication.
- Per explicit user/coordinator direction, agent-3 breaks only that API-order deadlock by copying System 02's already-defined **API artifacts verbatim** (same source and Unity GUIDs) onto `fixes/agent-3`. Agent-3 does not copy/own System 02 Runtime, tests, or SceneIssue files.

## Reusable boundaries now established

- **Character binding:** `CombatParticipant.FromCharacter(CharacterId, CombatTeam)` preserves the production CharacterId and derives only the Combat-local participant id/team view. Combat never holds Character runtime objects.
- **Encounter start:** `CombatStartRequest`/`CombatStartResult` use the real `EncounterId` and already-mapped Combat participants. Encounter role-to-team mapping remains composition policy.
- **Combat result:** `CombatResolved` contains only `EncounterId`, `CombatSessionId`, and `CombatTeam`. It contains no `EncounterResolution`, cleanup, campaign, or game-victory policy.
- **Encounter ownership runtime:** `EncounterCombatCoordinator` associates one Encounter with the Combat session and emits one terminal `CombatResolved` fact; `EncounterRegistry` remains authority for terminal Encounter resolution.
- **Vitality binding:** `Game.Combat.Runtime` references only engine-free `Game.Vitality.Api`. `CombatService(IVitalityService)` requires character-backed participants, preserves any pre-existing Vitality state, registers the legacy 6-point initial state only when the actor is unknown, routes attack damage through `ApplyDamage`, and derives HP/alive from `TryGet`. It does not depend on `Game.Vitality.Runtime`.
- **Input binding:** Combat consumes semantic `Game.Input.Api` state/context; scene composition supplies implementations.
- **Composition:** scene/Kentridge code owns role-to-team mapping, winner-to-EncounterResolution mapping, presentation, spawn/despawn policy, and concrete runtime construction.

## Combat state preservation boundary

Vitality migration changes only production life-state authority. Preserve Combat orchestration responsibilities:

- `ChainRoundReadinessCoordinator`: round/readiness and enemy-phase handoff.
- `ChainEnemyTacticalAI`: deterministic committed intents and enemy-phase progress.
- `ChainReactionReservationCoordinator`: reaction reservation/claim ownership.
- `ChainExecutionPlan`: collaborative plan/history/undo/redo/reaction attachment semantics.
- `ChainCombatBoard`: board/motion/round/reaction state.

Those prototype classes may contain experimental unit HP as part of that separate lab; this ticket removes duplicate **production** life authority and does not opportunistically refactor the experiment.

## Implementation status and next work

1. **Done:** semantic Character/Encounter integration contracts (`CombatParticipant.FromCharacter`, `CombatStartRequest/Result`, `CombatResolved`).
2. **Done:** engine-free `EncounterCombatCoordinator`, preserving Encounter ownership and emitting one terminal result.
3. **Partial:** copied the pre-existing System 02 API contract verbatim and added `CombatService(IVitalityService)`. Character-backed combat now reads and damages through Vitality; focused tests prove no shadow character-health read path.
4. **Pending final authority cutover:** production Kentridge still constructs parameterless legacy `CombatService`; remove that production fallback only when the real System 02 Runtime can be injected without agent-3 taking ownership of it.
5. **Done at reusable seam:** real `EncounterRegistry` requests/owns combat participation and consumes terminal resolution in the independent integration fixture.
6. **Already semantic:** keep Combat input through `Game.Input.Api`; no raw key/button knowledge in Combat.
7. **Next verification:** run exact-SHA targeted CI for the new Vitality API dependency, Combat runtime changes, and `CombatVitalityIntegrationTests`; automatic planner should also exercise dependent assemblies and Kentridge player validation.

## Tests / proof

- `CombatCharacterBindingTests.cs`: independent non-Kentridge CharacterId binding.
- `CombatEncounterContractTests.cs`: real EncounterId start/result and policy-free terminal fact shape.
- `EncounterCombatIntegrationTests.cs`: real `EncounterRegistry` membership/activation queue mapped to Combat, bounded deterministic completion, exactly-once terminal fact polling, and idempotent repeated `ApplyCombatResolved`.
- `CombatVitalityIntegrationTests.cs`: independent `IVitalityService` fixture proves pre-existing Vitality is preserved, accepted attacks call `ApplyDamage`, Combat HP/alive reads observe the Vitality service directly (including externally changed defeat), and lethal Vitality damage still settles the Combat winner.
- `Game.Combat.Tests.asmdef` owns these fixtures and now references `Game.Vitality.Api`; production Combat diffs are discovered structurally by `tools/module-validation-plan.py`.
- Earlier exact feature SHA `746de4cae082cc456c000153048166c0e4f967e3` passed targeted CI run `33503348443`, including module-owned Combat tests, dependent CharacterAI tests, and automatic Kentridge standalone-player validation. It predates the Vitality migration and therefore is not the final gate.

## Do not build

No second combat engine, no agent-3-owned Vitality Runtime, no final-boss/game-victory logic, no scene-specific policy in shared modules, no divergent substitute Vitality contract, no obsolete validation manifest, and no opportunistic refactor of the older CombatPrototype path.
