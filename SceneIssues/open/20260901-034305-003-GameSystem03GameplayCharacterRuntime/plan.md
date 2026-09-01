# 03 Gameplay character runtime — implementation plan

**Target module:** `Assets/Game/Characters/Api` / `Runtime` (`Game.Characters.Api`, `Game.Characters.Runtime`).

## API

`CharacterId`, stable character definition/role metadata needed by consumers, authoritative transform/kinematic semantic state, lifecycle state, registry/query interfaces, character-created/removed/state events. Keep player/NPC/enemy distinctions as composition/traits rather than separate runtime hierarchies.

## Runtime

1. Implement one authoritative registry and lifecycle for all gameplay characters.
2. Bind generated/world/campaign character identities to stable `CharacterId` values.
3. Move scene/bootstrap-owned actor records behind the registry.
4. Provide movement/world-query integration through existing world/collision APIs without embedding voxel implementation details in the public API.
5. Supply narrow hooks for Vitality, AI, Encounters, Sessions, replication, persistence, and cutscene actor adapters.

## Tests / proof

Create player/NPC/enemy compositions through the same runtime; stable identity across save/restore; removal/defeat distinction; headless deterministic tests; independent non-Kentridge fixture.

## Do not build

No enemy subclass tree, inventory ownership implementation, AI planner, combat rules, or presentation GameObject authority.
