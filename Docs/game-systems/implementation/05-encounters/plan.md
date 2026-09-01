# 05 Encounter activation, membership & lifecycle — implementation plan

**Target module:** `Assets/Game/Encounters/Api` / `Runtime` (`Game.Encounters.Api`, `Game.Encounters.Runtime`).

## API

`EncounterId`, definition/config, lifecycle state, membership snapshots, semantic activation requests/facts, participant join/leave, resolution result/reason, and encounter events.

## Runtime

1. Implement deterministic lifecycle and stable membership over Character IDs.
2. Separate encounter activation from cutscene and combat activation.
3. Permit persistent characters and temporary encounter-created characters through explicit ownership semantics.
4. Integrate Combat through API-level requests/results; Encounters owns whether/when combat participates.
5. Emit semantic resolution/cleanup facts for Story/Progression and composition.
6. Capture/restore current encounter state and expose replication projection.

## Dependencies

03 Characters; 01 consumes this module later. #12 supplies generated-world realization context through composition.

## Tests / proof

Proximity/semantic activation, membership changes, combat and non-combat encounter completion, persistent character survival, cleanup, restore, and an independent authored fixture.

## Do not build

No final-boss/game-outcome semantics, world-generation placement logic, or scene-trigger authority.
