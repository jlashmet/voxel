# 02 Actor vitality, damage & defeat — implementation plan

**Target module:** `Assets/Game/Vitality/Api` (`Game.Vitality.Api`) and `Assets/Game/Vitality/Runtime` (`Game.Vitality.Runtime`).

## API

Stable vitality snapshot, semantic damage request/result, defeat transition/event, and capture/restore seam. Identity references use `Game.Characters.Api.CharacterId`; no Unity objects. Current content demonstrates no in-session heal/revive operation, so no healing/respawn contract is added.

## Runtime

1. Authoritative vitality registry keyed by `CharacterId`.
2. Deterministic validation/clamping and exactly one defeat transition when crossing zero.
3. Defeat remains distinct from Combat winner/session and game outcome.
4. API-level capture/restore plus state/event projection seams without Persistence or GameplayReplication Runtime dependencies.
5. Production Combat reads/damages actor life only through Vitality; Combat retains turn/team/winner/session policy.

## Dependencies

System 03 Characters API prerequisite is resolved through `Game.Characters.Api.CharacterId`.

The System 01 SceneIssue design identifies the minimum identity seam required by T02-015: a Combat participant preserves the production `CharacterId`, exposes whether it is Character-backed, and derives only the Combat-local participant id from that canonical identity. Per coordinator direction, System 02 carries only that compatibility slice in `Game.Combat.Api`: `CombatParticipant.CharacterId`, `IsCharacterBacked`, the character-backed constructor, and `FromCharacter(CharacterId, CombatTeam)`. System 01 Encounter contracts are not copied. This avoids inventing a `CombatParticipantId` -> `CharacterId` policy. Combat may consume Vitality API; Vitality never depends on Combat.

## Ownership baseline and disposition

| Existing owner | Life-state truth | Disposition | Evidence / boundary |
| --- | --- | --- | --- |
| `Game.Combat.Runtime.CombatService` in `CombatRuntime.cs` | Previously independent `_hitPoints` plus `IsAlive`/winner reads | **Migrated** | `_hitPoints` is removed. `CombatService` requires `IVitalityService`; attack damage, alive checks, turn skipping, winner evaluation, and `TryGetHitPoints` projection read canonical Vitality through `CombatVitalityAdapter`. |
| `MountingForce.CombatPrototype.CombatBoard` / `UnitState` in `CombatCore.cs` | Prototype-local integer unit ids, `Hp`/`MaxHp`, reaction sandbox battle state | **Independent prototype, retain** | File namespace is `MountingForce.CombatPrototype`; units have integer sandbox ids and no `CharacterId`. Migrating this would require inventing production actor identity and would violate the reuse/ownership boundary. |
| `MountingForce.CombatPrototype.ChainUnitState` / `ChainCombatBoard` | Separate chain-combat lab state | **Independent prototype, retain** | Same isolated prototype family; no demonstrated mapping to production `CharacterId`. System 01 design explicitly preserves this lab unless a demonstrated acceptance defect requires otherwise. |
| `Assets/CombatPrototype/*` | Presentation/demo layer over prototype boards | **Presentation-only** | Not production Character vitality authority. |
| Kentridge playable composition | Previously let Combat initialize fixed HP | **Migrated composition** | Composition owns the concrete `VitalityRegistry`, registers the real player/bandit `CharacterId`s at the existing six-point parity value, and starts Combat with `CombatParticipant.FromCharacter`. |

## Selected implementation / proof

`Game.Vitality.Api` and Runtime are engine-free assemblies. `VitalitySnapshot`, `DamageRequest/Result`, `DefeatEvent`, and `IVitalityService` are semantic and keyed by `CharacterId`. `VitalityRegistry` owns authoritative actor life state, clamps damage, rejects unknown/invalid/already-defeated requests, emits one defeat event, captures in stable identity order, and restores atomically.

`CombatVitalityAdapter` is a stateless Runtime adapter over `IVitalityService`; it never stores HP and rejects legacy participants rather than inventing identity. `Game.Combat.Runtime` references `Game.Vitality.Api` only, not `Game.Vitality.Runtime`. Production `CombatService` has no health dictionary. Kentridge composition supplies the concrete registry and initial value.

Regression coverage includes damage boundaries, exactly-once defeat, non-Combat reuse, restore, Character-backed Combat identity, direct Combat adapter semantics, production `CombatService` damage/winner behavior against canonical Vitality, rejection of legacy/unregistered participants, and the Combat Runtime API-only dependency boundary.

Foundation exact-SHA request `49e2d5bb0153451263195b9c3c787bd2f8763a23` for feature parent `0fc4e0ae1f58f6ea7bfba405a4a2406c6c88d7de` passed workflow run `33485053919`. Because T02-015 materially changed Combat and Kentridge afterward, a new exact-SHA automatic module/player validation run is required before closure.

## Remaining gates

Run exact-SHA CI for the migrated feature and fix any compile/regression call sites it exposes. Then perform the final ownership/boundary proof, merge current `origin/master`, rerun/reconcile gates if the exact feature head changes, and only then close/promote. No respawn/revive policy, UI bars, game-over rules, or Combat-team semantics are added.
