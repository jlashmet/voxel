# 02 Actor vitality, damage & defeat — implementation plan

**Target module:** `Assets/Game/Vitality/Api` (`Game.Vitality.Api`) and `Assets/Game/Vitality/Runtime` (`Game.Vitality.Runtime`).

## API

Stable vitality state/snapshot, semantic damage request/result, defeat transition/event, restoration/healing only if current content requires it. Identity references come from `Game.Characters.Api`; no Unity objects.

## Runtime

1. Implement authoritative vitality registry keyed by `CharacterId`.
2. Validate damage, clamp state deterministically, and emit exactly one defeat transition when crossing the terminal threshold.
3. Keep defeat distinct from combat result and game outcome.
4. Expose capture/restore hooks for #16 and replication projection hooks for #06 without depending on those runtimes.
5. Migrate combat-prototype health ownership to adapters over this runtime; remove duplicate authoritative health stores after parity.

## Dependencies

03 Characters API first or in the same coordinated wave; Combat may consume Vitality API, never the reverse.

**BLOCKED (2026-09-01 UTC):** `Assets/Game/Characters` / `Game.Characters.Api.CharacterId` remains absent on current `origin/master` SHA `ef5240c7b24550dab86d0ed75388d6c99a44d47b`; repository commit search also finds no published `CharacterId` implementation. System 03's binding plan confirms `CharacterId` belongs to `Game.Characters.Api`. Public Vitality API/Runtime work that would commit a competing identity shape is blocked until that prerequisite contract lands or is available in a coordinated wave. Do not introduce a temporary actor identity or weaken acceptance; continue independent inventory/migration planning meanwhile.

## Ownership baseline (T02-001)

| Existing owner | Current life-state truth | Classification | Migration boundary |
| --- | --- | --- | --- |
| `Game.Combat.Runtime.CombatState` in `CombatCore.cs` | `CombatStats.MaxHp` plus `_hpById`; damage mutates HP and dead actors are rejected by HP <= 0 | **Migrate** | Combat resolution should request/query Vitality; Combat keeps hit/attack rules, not actor life truth. |
| `Game.Combat.Runtime.CombatService` in `CombatRuntime.cs` | Independent `_hitPoints` dictionary, fixed participant HP/damage, `IsAlive`, defeated-turn skipping and winner evaluation | **Migrate / adapter** | Replace HP mutation/query with Vitality while retaining combat turn/session/team winner policy in Combat. |
| `MountingForce.CombatPrototype.ChainUnitState` / `ChainCombatBoard` | Prototype-owned `MaxHp`, mutable `Hp`, `IsAlive`; board applies damage and derives battle-over state | **Obsolete authority / adapter while prototype remains** | Prototype may compose/display vitality, but must not remain a second authoritative character-life store after parity. Battle/cascade policy remains prototype/composition-owned. |
| `Assets/CombatPrototype/*` scene/controller scripts | Presentation/demo layer over the chain-combat board | **Presentation-only** | May read projected vitality state; must not become a new authoritative store. |

The baseline intentionally distinguishes actor defeat from combat/session outcome: Vitality owns current/max/defeated truth; Combat continues to own encounter participation, turns, teams, and winner/settlement policy.

## Tests / proof

Damage ordering, duplicate requests/idempotency where applicable, one defeat event, non-combat damage, restore of defeated/alive state, and an independent character consumer outside Combat.

## Do not build

No respawn/revive policy, UI bars, game-over rules, or combat-team semantics.
