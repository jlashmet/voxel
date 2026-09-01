# 13 Authoritative world-object interaction — implementation plan

**Target module:** `Assets/Game/WorldObjects/Api` / `Runtime` (`Game.WorldObjects.Api`, `Game.WorldObjects.Runtime`). Migrate/generalize existing authoritative world-object behavior from WorldBuilder/runtime code rather than duplicating it.

## API

`WorldObjectId`, semantic object state snapshot, interaction intent/context, interaction result/fact, behavior capability/handler seam, and state-change events. Actor is referenced by CharacterId; spatial context stays semantic/minimal.

## Runtime

1. Establish stable registry/binding for realized world objects.
2. Route character interaction intent to the authoritative object behavior with validation (identity, reach/context, current state, capability).
3. Apply deterministic state transitions and emit semantic facts.
4. Provide adapters so Loot and Progression can react without direct runtime coupling.
5. Add capture/restore and replication projections.
6. Migrate scene-local `E`/raycast-to-behavior shortcuts to shared Input + interaction requests.

## Dependencies

03 Characters, existing WorldBuilder realization API, existing Input API at the client/composition edge.

## Tests / proof

Two different object behaviors through one runtime, invalid actor/range/state rejection, repeated interaction, save/restore, and independent non-Kentridge consumer.

## Do not build

No UI prompt ownership, inventory mutation inside generic WorldObjects, or scene-specific object ids/policies.
