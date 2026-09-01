# Game-systems implementation plans

These plans turn the approved system designs into assignment-ready module work.

## Module convention

For reusable gameplay systems, the default shape is:

- `Assets/Game/<System>/Api` / `Game.<System>.Api`: stable semantic ids, commands, results, snapshots/read models, events, interfaces, configuration contracts. No Unity scene objects, prefabs, `MonoBehaviour`, or dependencies on another module's Runtime assembly.
- `Assets/Game/<System>/Runtime` / `Game.<System>.Runtime`: authoritative or local implementation. It may depend on its own API and other modules' APIs, but should not reach into other modules' Runtime internals.
- composition assemblies wire Runtime implementations together and own campaign/scene-specific policy.
- presentation modules consume semantic APIs/read models and do not mutate authoritative gameplay state directly.
- validation belongs with the owning module and follows repository module-local validation discovery; agents do not manually enumerate tests.

Existing Combat, Inventory, Quests, Story, and Input already demonstrate API/Runtime separation in the repository. Plans prefer extending those boundaries over parallel replacements.

## Intentional exceptions

Not every checklist system deserves a ceremonial API/Runtime pair. #12 is a composition bridge, #24 is canonical Kentridge production composition/validation, #25 is multi-process validation infrastructure/scenarios, and #26 is campaign content/composition. Their plans explicitly keep those responsibilities out of new generic runtime modules.

## Dependency waves

1. **Core authoritative domains:** 02, 03, 04, 05, 09, 11, 13 plus the existing Input migration.
2. **Domain integration and networking:** 01, 06, 07, 08, 10, 12.
3. **Run lifecycle:** 14, 15, 16.
4. **Player-facing runtime:** 17-23.
5. **Production proof/content:** 24-26.

Agents can own several plans in one wave when they share a bounded context, but a branch should have one explicit owner for each module it changes.

## Plans

- [01 Production combat integration](01-production-combat-integration.md)
- [02 Actor vitality, damage & defeat](02-actor-vitality.md)
- [03 Gameplay character runtime](03-characters.md)
- [04 Character AI](04-character-ai.md)
- [05 Encounter lifecycle](05-encounters.md)
- [06 Gameplay-state replication](06-gameplay-replication.md)
- [07 Party & session formation](07-sessions.md)
- [08 Disconnect/reconnect continuity](08-continuity.md)
- [09 Inventory ownership & transactions](09-inventory.md)
- [10 World loot & transfer](10-loot.md)
- [11 Unified progression](11-progression.md)
- [12 Encounter realization bridge](12-encounter-realization.md)
- [13 World-object interaction](13-world-objects.md)
- [14 Session orchestration](14-session-orchestration.md)
- [15 Game outcomes](15-outcomes.md)
- [16 Persistence & restore](16-persistence.md)
- [17 Gameplay HUD](17-hud.md)
- [18 Inventory presentation](18-inventory-presentation.md)
- [19 Progression presentation](19-progression-presentation.md)
- [20 Party/session presentation](20-session-presentation.md)
- [21 Gameplay audio](21-audio.md)
- [22 Gameplay VFX](22-vfx.md)
- [23 Application frontend](23-application.md)
- [24 Production-composed vertical slice](24-production-slice.md)
- [25 Multiplayer E2E validation](25-multiplayer-e2e.md)
- [26 Full-run campaign composition](26-full-run-campaign.md)
