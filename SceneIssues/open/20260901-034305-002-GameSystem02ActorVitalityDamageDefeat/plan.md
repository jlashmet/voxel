# 02 Actor vitality, damage & defeat — implementation plan

**Target module:** `Assets/Game/Vitality/Api` (`Game.Vitality.Api`) and `Assets/Game/Vitality/Runtime` (`Game.Vitality.Runtime`).

## API

Stable vitality snapshot, semantic damage request/result, defeat transition/event, and capture/restore seam. Identity references use `Game.Characters.Api.CharacterId`; no Unity objects. Current content demonstrates no in-session heal/revive operation, so no healing/respawn contract is added.

## Runtime

1. Authoritative vitality registry keyed by `CharacterId`.
2. Deterministic validation/clamping and exactly one defeat transition when crossing zero.
3. Defeat remains distinct from Combat winner/session and game outcome.
4. API-level capture/restore plus state/event projection seams without Persistence or GameplayReplication Runtime dependencies.
5. Migrate Combat/prototype health ownership to adapters over Vitality; remove duplicate authoritative life stores after parity.

## Dependencies

System 03 Characters API prerequisite is **resolved**: `Game.Characters.Api.CharacterId` landed on `origin/master` `e98191876c104ff115a1828b1ce0a6b2d4d4480b` and was merged into `fixes/agent-9` at `ccce1ff19c1306a838a2e5fadcf023399cc1d6b3`.

The System 01 SceneIssue design defines the identity seam required by T02-015: a Combat participant must preserve the production `CharacterId`, expose whether it is Character-backed, and derive only the Combat-local participant id from that canonical identity. Per coordinator direction, System 02 now carries the **minimal compatibility slice only** in `Game.Combat.Api`: `CombatParticipant.CharacterId`, `IsCharacterBacked`, the character-backed constructor, and `FromCharacter(CharacterId, CombatTeam)`. `Game.Combat.Api` references only `Game.Characters.Api` for this slice. System 01's Encounter-specific contracts remain owned by System 01 and are not copied. This breaks the publication-order deadlock without inventing `CombatParticipantId` -> `CharacterId` mapping policy. Combat may consume Vitality API; Vitality never depends on Combat.

## Ownership baseline (T02-001)

| Existing owner | Current life-state truth | Classification | Migration boundary |
| --- | --- | --- | --- |
| `Game.Combat.Runtime.CombatState` in `CombatCore.cs` | `CombatStats.MaxHp` plus `_hpById`; damage mutates HP and dead actors are rejected by HP <= 0 | **Migrate** | Combat resolution should request/query Vitality; Combat keeps hit/attack rules, not actor life truth. |
| `Game.Combat.Runtime.CombatService` in `CombatRuntime.cs` | Independent `_hitPoints`, `IsAlive`, defeated-turn skipping and winner evaluation | **Migrate / adapter** | Replace HP mutation/query with Vitality while retaining turn/session/team winner policy in Combat. |
| `MountingForce.CombatPrototype.ChainUnitState` / `ChainCombatBoard` | Prototype-owned `MaxHp`, mutable `Hp`, `IsAlive`; board applies damage and derives battle-over state | **Obsolete authority / adapter while prototype remains** | Prototype may compose/display vitality, but must not remain a second authoritative character-life store after parity. |
| `Assets/CombatPrototype/*` scene/controller scripts | Presentation/demo layer over the chain-combat board | **Presentation-only** | May read projected vitality state; must not become authoritative. |

## Selected implementation / proof

`Game.Vitality.Api` and Runtime are engine-free assemblies. `VitalitySnapshot`, `DamageRequest/Result`, `DefeatEvent`, and `IVitalityService` are semantic and keyed by `CharacterId`. `VitalityRegistry` owns the only new life-state dictionary, clamps damage, rejects unknown/invalid/already-defeated requests, emits one defeat event, captures in stable identity order, and restores atomically. Tests cover damage boundaries, one-shot defeat, independent non-Combat reuse, alive/defeated restore, and dependency boundaries.

The minimal Combat identity seam is regression-covered from `Game.Vitality.Tests`: `CombatParticipant.FromCharacter` preserves the canonical `CharacterId`, derives the participant id from `CharacterId.Value`, carries only Combat team semantics, and reports `IsCharacterBacked=true`.

Exact-SHA request `49e2d5bb0153451263195b9c3c787bd2f8763a23` for feature parent `0fc4e0ae1f58f6ea7bfba405a4a2406c6c88d7de` completed successfully in workflow run `33485053919`: focused regression, automatic module validation, and standalone-player SceneIssue replay all passed.

## Remaining gates

Use the now-local minimal Character-backed participant seam to migrate current Combat health authority to Vitality, perform repository-wide duplicate-state cleanup and final ownership/boundary audit, then run final exact-SHA targeted/module validation CI. Before final promotion, merge current `origin/master` into `fixes/agent-9` and reconcile any overlapping System 01 contract publication without broadening System 02 ownership. No respawn/revive policy, UI bars, game-over rules, or Combat-team semantics.
