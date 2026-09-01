# 12 WorldBuilder encounter realization bridge — implementation plan

**Target ownership:** composition-only assembly, proposed `Assets/Game/Composition/EncounterRealization` / `Game.Composition.EncounterRealization`. Do not create `Api`/`Runtime` just to mirror the pattern.

## Responsibility

Translate authored semantic encounter/site/NPC intent plus realized `Game.WorldBuilder.Api` output into the narrow realization data required by `Game.Encounters.Api` and `Game.Characters.Api`.

## Implementation

1. Identify the minimal WorldBuilder realization facts Encounters actually needs: stable site/object/NPC bindings, positions/areas, spawn-capable points, etc.
2. Add missing semantic queries to WorldBuilder API only when reusable and demonstrated by at least one second consumer where practical.
3. Build a pure adapter/composer that produces encounter realization/binding records.
4. Move Kentridge or scene-specific placement policy into Kentridge/campaign composition; shared bridge contains no named places or encounters.
5. Remove duplicate placement calculations from encounter runtime/scene scripts.

## Dependencies

WorldBuilder API, 03 Characters API, 05 Encounters API. It should not depend on their Runtime internals.

## Tests / proof

At least two independently authored encounter/site fixtures consume the bridge; generated placements are reused rather than recomputed; module-local built-player scene only if player-visible realization needs proof.

## Do not build

No parallel world generator, encounter lifecycle, or Kentridge-specific policy in the shared bridge.
