# 01 Production combat integration — implementation plan

**Target ownership:** extend existing `Game.Combat.Api` / `Game.Combat.Runtime`; add only thin composition adapters where Encounters/Characters/Vitality meet Combat. Do **not** create a second combat runtime.

## Dependencies

02 Vitality, 03 Characters, 05 Encounters, existing `Game.Input.Api/Runtime` migration.

## Current baseline inventory

- `Game.Combat.Runtime.CombatService` is currently authoritative for life state through its private `_hitPoints` dictionary, with fixed `ParticipantHitPoints = 6` and `AttackDamage = 2`. Team identity is carried directly on `CombatParticipant`.
- `KentridgeForestBanditEncounter` is a scene-local composition/runtime owner. In `Awake` it constructs `InputContextService`, `UnityPlayerInputReader`, and `CombatService`; in `BeginBanditCombat` it creates player/enemy `CombatParticipant` identities by hand; it directly spawns the three bandit GameObjects and owns encounter completion/cleanup.
- `Game.Composition.Kentridge.Playable.asmdef` directly references both `Game.Combat.Runtime` and `Game.Input.Runtime`, so the desired API-only cross-module boundary is not yet true at this composition seam.
- `Assets/Game/Composition/CombatEnvironmentRuntime` is a separate older `MountingForce.CombatPrototype`/environment composition path rather than a consumer of the production `Game.Combat.Runtime` seam; do not fold that adjacent experiment into this ticket without an acceptance-driven defect.
- `Game.Input.Api` exists and is already referenced by `Game.Combat.Runtime`. Its current semantic surface is `IPlayerInputReader` + `PlayerInputSnapshot` and `IInputContextService`; `CombatInputController` consumes movement semantically and has no raw key/button polling.
- Production `Assets/Game/Characters` is now present on master, including engine-free `Game.Characters.Api.CharacterId`. Production `Assets/Game/Vitality` and production `Assets/Game/Encounters` / `Game.Encounters.Api` remain absent.
- The nominal GameSystem01 replacement scene from the earlier generated description is also not present; binding acceptance for this ticket is the checked-in plan/tasks and `KentridgePlayableSlice` issue metadata.

## Dependency blocker

Character identity is no longer blocked: T01-003 now uses the real `Game.Characters.Api.CharacterId`. Implementation tasks that require `Game.Vitality.Api` or `Game.Encounters.Api` remain blocked until those prerequisite contracts land on `origin/master`. Acceptance is unchanged. Do not invent shadow Vitality/Encounter contracts in Combat: doing so would create the duplicate runtime/API paths this integration ticket exists to remove.

Independent work that remains valid while blocked: keep the branch merged with current master, consume landed prerequisite APIs directly, inventory existing authority/bypasses, verify API/runtime references, and avoid committing dependency-shaped substitutes for missing modules.

## Intended reusable boundaries once prerequisites land

- **Character binding:** composition maps production `CharacterId` plus production team identity to a combat participant identity through API contracts only; Combat must never hold Character runtime objects. T01-003 implements this as `CombatParticipant.FromCharacter(CharacterId, CombatTeam)`, preserving the CharacterId and deriving the Combat participant id from the same stable serialized value.
- **Vitality binding:** Combat submits accepted damage through a semantic Vitality API and reads resulting alive/defeated truth from Vitality. Combat retains no second authoritative HP store.
- **Encounter binding:** Encounters requests combat participation/activation and consumes a minimal `CombatResolved` fact. Combat does not own spawn/despawn policy, encounter cleanup, campaign outcome, or final-boss semantics.
- **Input binding:** Combat consumes semantic `Game.Input.Api` state/context. Scene composition may supply the production implementation, but reusable Combat code contains no Unity key/button or Kentridge-specific policy.
- **Composition:** scene/Kentridge code selects participants, authored encounter policy, presentation, and concrete production implementations. Reusable module APIs remain scene-agnostic.

## Combat state preservation boundary

Vitality migration must change only the production life-state authority needed by `Game.Combat.Runtime.CombatService`; it must not absorb Combat orchestration. The existing Combat assembly also contains an older `MountingForce.CombatPrototype` surface used for tactical/chain-combat blast-radius coverage. Preserve its combat-specific responsibilities in place unless a demonstrated acceptance defect requires otherwise:

- `ChainRoundReadinessCoordinator`: per-command-group ready ownership, tracked round, and enemy-phase handoff.
- `ChainEnemyTacticalAI`: deterministic committed intents, planned round, current intent cursor, and enemy-phase progress.
- `ChainReactionReservationCoordinator`: physical-event reservation/claim ownership and opportunity synchronization.
- `ChainExecutionPlan`: ordered collaborative plan, revision/history, undo/redo, and reaction attachment semantics.
- `ChainCombatBoard`: board/motion/round/reaction authority consumed by the above coordinators.

Those prototype classes currently contain their own experimental unit HP as part of that separate combat lab. This ticket does **not** opportunistically refactor that adjacent model merely because it lives in the same assembly; T01-031 treats it as blast radius. T01-030 removes duplicate **production** life authority and documents any justified internal experimental state.

## Implementation

1. Define semantic integration contracts in APIs: encounter participant/character binding to combat participant/team, combat-start request/result, and combat-resolution fact.
2. Adapt the existing Combat runtime so participant health/alive truth comes from system 02 rather than combat-owned prototype health.
3. Route accepted combat damage/defeat through Vitality; Combat observes resulting alive/defeated state.
4. Have Encounters request/own combat participation and consume `CombatResolved`; ordinary Combat completion never resolves the game directly.
5. Replace scene-local `new CombatService`, local input context services, and raw Kentridge combat bootstrap code with production composition.
6. Keep combat input semantic through `Game.Input.Api`; no key/button knowledge in Combat.

## Tests / proof

- `CombatCharacterBindingTests` provides the first independent non-Kentridge fixture, proving stable CharacterId-to-Combat binding without scene policy;
- module tests remain required for vitality-backed participants, repeated resolution idempotency, and encounter-to-combat mapping once those real APIs land;
- Kentridge remains assembled integration proof later in #24.

## Do not build

No new combat engine, final-boss flag, game-victory logic, or scene-specific combat policy in shared modules.
