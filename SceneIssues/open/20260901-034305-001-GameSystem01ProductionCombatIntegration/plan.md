# 01 Production combat integration — implementation plan

**Target ownership:** extend existing `Game.Combat.Api` / `Game.Combat.Runtime`; add only thin composition adapters where Encounters/Characters/Vitality meet Combat. Do **not** create a second combat runtime.

## Dependencies

02 Vitality, 03 Characters, 05 Encounters, existing `Game.Input.Api/Runtime` migration.

## Current baseline inventory

- `Game.Combat.Runtime.CombatService` is currently authoritative for life state through its private `_hitPoints` dictionary, with fixed `ParticipantHitPoints = 6` and `AttackDamage = 2`. Team identity is carried directly on `CombatParticipant`.
- `KentridgeForestBanditEncounter` remains a scene-local composition/runtime owner: it constructs Input/Combat services, creates participant identities by hand, starts combat, and settles scene state.
- `Game.Composition.Kentridge.Playable.asmdef` still directly references `Game.Combat.Runtime` and `Game.Input.Runtime`; final API-only composition migration waits for the production Vitality seam and T01-015.
- `Assets/Game/Composition/CombatEnvironmentRuntime` is the separate older `MountingForce.CombatPrototype` experiment and remains out of scope unless a demonstrated acceptance defect requires it.
- `Game.Input.Api` is already semantic and `CombatInputController` has no raw key/button polling.
- Production Characters and Encounters are now present. `Game.Characters.Api` owns `CharacterId`; `Game.Encounters.Api` owns Encounter identity, membership, activation, queued combat requests, and terminal Encounter resolution consumption.
- Production `Assets/Game/Vitality` / `Game.Vitality.Api` remains absent from current master `1b6d5db96ea150bd0cb573bfaff7e220f19afbeb`.
- The nominal GameSystem01 replacement scene from the earlier generated description is not present; assembled proof for this ticket remains Kentridge plus the repository's automatic module/player validation path.

## Dependency blocker

Character and Encounter contracts are no longer blocked and are consumed directly. The remaining external prerequisite is production `Game.Vitality.Api`; do not invent a substitute life-state contract or create a second HP authority. Until Vitality lands, continue independent work that does not entrench the existing Combat-owned `_hitPoints` store.

## Reusable boundaries now established

- **Character binding:** `CombatParticipant.FromCharacter(CharacterId, CombatTeam)` preserves the production CharacterId and derives only the Combat-local participant id/team view. Combat never holds Character runtime objects.
- **Encounter start:** `CombatStartRequest`/`CombatStartResult` use the real `EncounterId` and already-mapped Combat participants. Encounter role-to-team mapping remains composition policy.
- **Combat result:** `CombatResolved` contains only `EncounterId`, `CombatSessionId`, and `CombatTeam`. It contains no `EncounterResolution`, cleanup, campaign, or game-victory policy.
- **Encounter ownership runtime:** `EncounterCombatCoordinator` is a thin engine-free adapter over the existing `CombatService`; it associates one Encounter with the Combat session and emits one terminal `CombatResolved` fact. `EncounterRegistry` remains the authority that accepts that fact via `ApplyCombatResolved`.
- **Vitality binding:** still pending. Combat must eventually submit accepted damage through `Game.Vitality.Api` and read alive/defeated truth from Vitality, retaining no second authoritative HP store.
- **Input binding:** Combat consumes semantic `Game.Input.Api` state/context; scene composition supplies implementations.
- **Composition:** scene/Kentridge code owns authored role-to-team mapping, winner-to-EncounterResolution mapping, presentation, spawn/despawn policy, and concrete runtime construction.

## Combat state preservation boundary

Vitality migration must change only production life-state authority needed by `Game.Combat.Runtime.CombatService`; it must not absorb Combat orchestration. Preserve the older `MountingForce.CombatPrototype` blast-radius responsibilities unless a demonstrated defect requires otherwise:

- `ChainRoundReadinessCoordinator`: round/readiness and enemy-phase handoff.
- `ChainEnemyTacticalAI`: deterministic committed intents and enemy-phase progress.
- `ChainReactionReservationCoordinator`: reaction reservation/claim ownership.
- `ChainExecutionPlan`: collaborative plan/history/undo/redo/reaction attachment semantics.
- `ChainCombatBoard`: board/motion/round/reaction state.

Those prototype classes may contain experimental unit HP as part of that separate lab; this ticket removes duplicate **production** life authority and does not opportunistically refactor the experiment.

## Implementation status and next work

1. **Done:** semantic Character/Encounter integration contracts (`CombatParticipant.FromCharacter`, `CombatStartRequest/Result`, `CombatResolved`).
2. **Done:** engine-free `EncounterCombatCoordinator` over the existing Combat runtime, preserving Encounter ownership and emitting one terminal result.
3. **Blocked on Vitality:** replace `_hitPoints` production authority with system 02 and route accepted damage/defeat through Vitality.
4. **Done at reusable seam:** real `EncounterRegistry` requests/owns combat participation and consumes terminal resolution in the independent integration fixture. Kentridge wiring/removal of local bootstrap remains T01-015.
5. **Blocked on final production composition/Vitality seam:** replace scene-local `new CombatService`, local Input runtime bootstrap, and direct runtime assembly coupling in Kentridge.
6. **Already semantic:** keep Combat input through `Game.Input.Api`; no raw key/button knowledge in Combat.
7. **Done:** module-owned Combat tests live under `Assets/Game/Combat/Tests` in `Game.Combat.Tests`, matching the current convention-based `tools/module-validation-plan.py` discovery path, and exact-SHA CI has validated that path.

## Tests / proof

- `Assets/Game/Combat/Tests/CombatCharacterBindingTests.cs`: independent non-Kentridge CharacterId binding.
- `Assets/Game/Combat/Tests/CombatEncounterContractTests.cs`: real EncounterId start/result and policy-free terminal fact shape.
- `Assets/Game/Combat/Tests/EncounterCombatIntegrationTests.cs`: real `EncounterRegistry` membership/activation queue mapped to Combat, bounded deterministic Combat completion, exactly-once terminal fact polling, and idempotent repeated `ApplyCombatResolved` with no second Encounter revision.
- `Game.Combat.Tests.asmdef` gives those fixtures module ownership; production Combat diffs are discovered structurally by the current validation planner. Any production diff also adds the repository-wide Kentridge player integration target automatically.
- Exact feature SHA `746de4cae082cc456c000153048166c0e4f967e3` passed targeted CI run `33503348443`. The automatic plan selected `Game.Combat.Tests` and dependent `Game.CharacterAI.Tests`; both test assemblies passed. The Kentridge standalone-player build succeeded, ran the validation scenario for 80 seconds, and captured seven real-player screenshots. The published `ci/single-test` status for that SHA is success.
- Earlier run `33500855298` exposed a validation-registration defect before Unity: the planner rejects obsolete `*.module-validation.json`. That manifest was removed and replaced with the structural test assembly convention; the corrected convention is what passed run `33503348443`.
- Existing `CombatAuthorityMigrationTests`, `CombatInputModuleBoundaryTests`, and `KentridgeCombatEncounterTests` remain blast-radius/integration coverage.
- Vitality-backed participant tests and final Kentridge production-path proof remain pending.

## Do not build

No new combat engine, final-boss flag, game-victory logic, scene-specific combat policy in shared modules, substitute Vitality contract, obsolete validation manifest, or opportunistic refactor of the older CombatPrototype path.
