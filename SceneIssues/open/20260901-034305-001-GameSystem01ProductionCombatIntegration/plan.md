# 01 Production combat integration — implementation plan

**Target ownership:** extend existing `Game.Combat.Api` / `Game.Combat.Runtime`; add only thin composition adapters where Encounters/Characters/Vitality meet Combat. Do **not** create a second combat runtime.

## Dependencies

02 Vitality, 03 Characters, 05 Encounters, existing `Game.Input.Api/Runtime` migration.

## Current baseline inventory

- `Game.Combat.Runtime.CombatService` historically owned life state through private participant HP. The new `CombatService(IVitalityService)` path delegates character-backed current/alive state and accepted damage to `Game.Vitality.Api`; the parameterless legacy path remains temporarily for unmigrated Kentridge/tests.
- `KentridgeForestBanditEncounter` remains a scene-local composition/runtime owner: it constructs Input/Combat services, creates participant identities by hand, starts combat, and settles scene state.
- `Game.Input.Api` is already semantic and `CombatInputController` has no raw key/button polling.
- Production Characters and Encounters are present. `Game.Characters.Api` owns `CharacterId`; `Game.Encounters.Api` owns Encounter identity, membership, activation, queued combat requests, and terminal Encounter resolution consumption.
- Agent-3 is merged through current master `b1b69290a59278b0e7caba798641c76a9866aa5c`. Master now contains convention-owned `Assets/Game/Vitality/Api` and `Assets/Game/Vitality/Tests`, but still has no `Assets/Game/Vitality/Runtime`.
- During the master merge, master’s Vitality Unity GUIDs and its `IVitalityQuery`/revision projection contract were retained. Agent-3 adds only the damage/service semantics Combat requires. Master’s newer `ICombatService` semantic surface is also retained.

## Reusable boundaries now established

- **Character binding:** `CombatParticipant.FromCharacter(CharacterId, CombatTeam)` preserves the production CharacterId and derives only the Combat-local participant id/team view. Combat never holds Character runtime objects.
- **Encounter start:** `CombatStartRequest`/`CombatStartResult` use the real `EncounterId` and already-mapped Combat participants. Encounter role-to-team mapping remains composition policy.
- **Combat result:** `CombatResolved` contains only `EncounterId`, `CombatSessionId`, and `CombatTeam`. It contains no `EncounterResolution`, cleanup, campaign, or game-victory policy.
- **Encounter ownership runtime:** `EncounterCombatCoordinator` associates one Encounter with the Combat session and emits one terminal `CombatResolved` fact; `EncounterRegistry` remains authority for terminal Encounter resolution.
- **Vitality binding:** `Game.Combat.Runtime` references only engine-free `Game.Vitality.Api` plus `Game.Characters.Api` because the API signatures expose `CharacterId`. `CombatService(IVitalityService)` requires character-backed participants, preserves existing Vitality state, routes attack damage through `ApplyDamage`, and derives HP/alive from Vitality queries. It does not depend on a concrete Vitality Runtime.
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
3. **Partial:** `CombatService(IVitalityService)` reads and damages through Vitality; focused tests prove no shadow character-health read path in that constructor path.
4. **Blocked production cutover:** production Kentridge still constructs parameterless legacy `CombatService`; remove that production fallback only when a real System 02 Vitality Runtime is published/injectable without agent-3 taking ownership of it.
5. **Done at reusable seam:** real `EncounterRegistry` requests/owns combat participation and consumes terminal resolution in the independent integration fixture.
6. **Already semantic:** keep Combat input through `Game.Input.Api`; no raw key/button knowledge in Combat.
7. **Unblocked verification now:** the old API-only Vitality planner fallback is gone because current master publishes `Game.Vitality.Tests`. Run a fresh exact-SHA automatic module gate on the merged branch now.
8. **After real Runtime publication:** merge current master, wire Kentridge to the real Vitality implementation, remove production use of the legacy health path, run assembled player proof/blast-radius validation, then close only with one authority.

## Tests / proof

- `CombatCharacterBindingTests.cs`: independent non-Kentridge CharacterId binding.
- `CombatEncounterContractTests.cs`: real EncounterId start/result and policy-free terminal fact shape.
- `EncounterCombatIntegrationTests.cs`: real `EncounterRegistry` membership/activation queue mapped to Combat, bounded deterministic completion, exactly-once terminal fact polling, and idempotent repeated `ApplyCombatResolved`.
- `CombatVitalityIntegrationTests.cs`: independent `IVitalityService` fixture proves pre-existing Vitality is preserved, accepted attacks call `ApplyDamage`, Combat HP/alive reads observe the Vitality service directly, and lethal Vitality damage settles the Combat winner.
- Master now provides convention-owned `Game.Vitality.Tests`, so Vitality API changes no longer require broad fallback solely because the module root is undiscovered.
- Earlier exact feature SHA `746de4cae082cc456c000153048166c0e4f967e3` passed targeted CI run `33503348443` before Vitality migration.
- Exact feature SHA `dbd0344945a9775527feb5dae0c7e75fe5a053ba` reached and passed Combat/dependent suites in run `33525068619`; its overall failure was caused by the now-resolved API-only Vitality fallback selecting unrelated Materials tests.

## Do not build

No second combat engine, no agent-3-owned Vitality Runtime, no fake Vitality test module, no unrelated Materials fixes, no module-planner exemptions, no final-boss/game-victory logic, no scene-specific policy in shared modules, no divergent substitute Vitality contract, no obsolete validation manifest, and no opportunistic refactor of the older CombatPrototype path.
