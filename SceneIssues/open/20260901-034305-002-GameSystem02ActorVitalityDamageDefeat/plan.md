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

## Tests / proof

Damage ordering, duplicate requests/idempotency where applicable, one defeat event, non-combat damage, restore of defeated/alive state, and an independent character consumer outside Combat.

## Do not build

No respawn/revive policy, UI bars, game-over rules, or combat-team semantics.
