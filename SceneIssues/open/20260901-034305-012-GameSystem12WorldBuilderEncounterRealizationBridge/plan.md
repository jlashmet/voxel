# 12 WorldBuilder encounter realization bridge — implementation plan

**Target ownership:** composition-only assembly, `Assets/Game/Composition/EncounterRealization` / `Game.Composition.EncounterRealization`. Do not create `Api`/`Runtime` just to mirror the pattern.

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

## Investigation / implementation record

- Baseline `fixes/agent-9` started exactly at `origin/master` `81ffa4bbc76c3feb6e0bde2376065b4144f3f10a`.
- The demonstrated duplicate placement owner is `KentridgeForestBanditEncounter`: it reconstructs a pine-forest encounter anchor by combining Kentridge/Hightown authored coordinates and scanning `RegionThemeMap`, then invents three local bandit offsets and a hardcoded realization id. This is the concrete placement duplication to remove.
- `Game.WorldBuilder.Api` already exposes stable semantic `SiteRef`/`NpcRef`, `ResolvedSiteId`, site-role bindings, and NPC-to-realized-site assignments. It deliberately does not expose backend physical coordinates. Existing `KentridgeCampaignWorldRealization` demonstrates the intended later boundary: backend/campaign composition adapts exact generated placement facts rather than recomputing them in consumers.
- Therefore no speculative WorldBuilder API widening is currently justified. The shared bridge accepts an `IEncounterRealizationFacts` supplied by the realization owner and depends only on `Game.WorldBuilder.Api`, `Game.Encounters.Api`, and `Game.Characters.Api`.
- `Game.Composition.EncounterRealization` now contains a pure `EncounterRealizationComposer`, semantic failure results, exact site/NPC placement bindings, and no named Kentridge policy or Runtime dependencies.
- Module-local regressions now exercise two independently authored semantic fixtures with deliberately different exact supplied placements, missing-realization failure, and assembly dependency boundaries. CI validation is still pending.
- Remaining implementation work is to identify/adapt the authoritative Kentridge forest/spawn realization facts, make the assembled forest encounter consume bridge output, and remove its parallel coordinate/offset calculations. If no reusable physical fact exists for the three spawn positions, keep the required adapter in Kentridge composition rather than broadening WorldBuilder API without demonstrated reuse.
