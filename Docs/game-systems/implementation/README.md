# Game-systems implementation plans and tasks

These documents turn the approved game-system designs into assignment-ready implementation work. Each system owns one folder containing:

- `plan.md` — module ownership, API/runtime responsibilities, dependencies, implementation direction, validation proof, and explicit non-goals.
- `tasks.md` — ordered, executable checklist with stable task ids, migration work, regressions, module-local validation, integration proof, cleanup, and close criteria.

Agents should work the next unchecked non-blocked task in the assigned system folder. A task may be skipped only when its own text says the condition can be satisfied by demonstrating that no implementation is currently required. Blocked tasks stay unchecked and record the external blocker; agents continue independent tasks rather than weakening acceptance.

## Module convention

For reusable gameplay systems, the default shape is:

- `Assets/Game/<System>/Api` / `Game.<System>.Api`: stable semantic ids, commands, results, snapshots/read models, events, interfaces, and configuration contracts. No Unity scene objects, prefabs, `MonoBehaviour`, or dependency on another module's Runtime assembly.
- `Assets/Game/<System>/Runtime` / `Game.<System>.Runtime`: authoritative or local implementation. It may depend on its own API and other modules' APIs, but must not reach into other modules' Runtime internals.
- Composition assemblies wire Runtime implementations together and own campaign/scene-specific policy.
- Presentation modules consume semantic APIs/read models and do not mutate authoritative gameplay state directly.
- Validation belongs with the owning module and follows repository module-local validation discovery; agents do not manually enumerate tests.

Existing Combat, Inventory, Quests, Story, WorldBuilder, and Input already demonstrate or are converging on the API/Runtime boundary. Plans extend/generalize existing ownership rather than introducing parallel replacements.

## Intentional exceptions

Not every checklist system gets a ceremonial API/Runtime pair. System 12 is a composition bridge, system 24 is canonical Kentridge production composition/validation, system 25 is multi-process validation infrastructure/scenarios, and system 26 is campaign content/composition. Their tasks keep these responsibilities out of unnecessary generic runtime modules.

## Dependency waves

1. **Core authoritative domains:** 02, 03, 04, 05, 09, 11, 13 plus the existing Input migration.
2. **Domain integration and networking:** 01, 06, 07, 08, 10, 12.
3. **Run lifecycle:** 14, 15, 16.
4. **Player-facing runtime:** 17-23.
5. **Production proof/content:** 24-26.

Agents may own several folders in one wave when they share a bounded context, but a branch has one explicit owner for each production module it changes. Cross-module changes consume APIs; if an API must change, update the owning system's tasks/contract rather than bypassing its Runtime boundary.

## Systems

- **01 Production combat integration:** [plan](01-production-combat-integration/plan.md) · [tasks](01-production-combat-integration/tasks.md)
- **02 Actor vitality, damage & defeat:** [plan](02-actor-vitality/plan.md) · [tasks](02-actor-vitality/tasks.md)
- **03 Gameplay character runtime:** [plan](03-characters/plan.md) · [tasks](03-characters/tasks.md)
- **04 Character AI, autonomous life, perception & intent:** [plan](04-character-ai/plan.md) · [tasks](04-character-ai/tasks.md)
- **05 Encounter activation, membership & lifecycle:** [plan](05-encounters/plan.md) · [tasks](05-encounters/tasks.md)
- **06 Gameplay-state replication:** [plan](06-gameplay-replication/plan.md) · [tasks](06-gameplay-replication/tasks.md)
- **07 Multiplayer party & session formation:** [plan](07-sessions/plan.md) · [tasks](07-sessions/tasks.md)
- **08 Player disconnect, reconnect & continuity:** [plan](08-continuity/plan.md) · [tasks](08-continuity/tasks.md)
- **09 Gameplay inventory ownership & transactions:** [plan](09-inventory/plan.md) · [tasks](09-inventory/tasks.md)
- **10 World loot, pickup & item transfer:** [plan](10-loot/plan.md) · [tasks](10-loot/tasks.md)
- **11 Unified quest & objective progression:** [plan](11-progression/plan.md) · [tasks](11-progression/tasks.md)
- **12 WorldBuilder encounter realization bridge:** [plan](12-encounter-realization/plan.md) · [tasks](12-encounter-realization/tasks.md)
- **13 Authoritative world-object interaction:** [plan](13-world-objects/plan.md) · [tasks](13-world-objects/tasks.md)
- **14 Game session & campaign orchestration:** [plan](14-session-orchestration/plan.md) · [tasks](14-session-orchestration/tasks.md)
- **15 Game outcome & completion policy:** [plan](15-outcomes/plan.md) · [tasks](15-outcomes/tasks.md)
- **16 Authoritative session persistence & restore:** [plan](16-persistence/plan.md) · [tasks](16-persistence/tasks.md)
- **17 Production gameplay HUD & semantic presentation:** [plan](17-hud/plan.md) · [tasks](17-hud/tasks.md)
- **18 Inventory UI & authoritative inventory interaction:** [plan](18-inventory-presentation/plan.md) · [tasks](18-inventory-presentation/tasks.md)
- **19 Quest & objective UI / progression presentation:** [plan](19-progression-presentation/plan.md) · [tasks](19-progression-presentation/tasks.md)
- **20 Multiplayer party, teammate & session presentation:** [plan](20-session-presentation/plan.md) · [tasks](20-session-presentation/tasks.md)
- **21 Gameplay audio integration & semantic cue presentation:** [plan](21-audio/plan.md) · [tasks](21-audio/tasks.md)
- **22 Combat / interaction VFX & semantic feedback:** [plan](22-vfx/plan.md) · [tasks](22-vfx/tasks.md)
- **23 Application frontend, menus, settings & session start flow:** [plan](23-application/plan.md) · [tasks](23-application/tasks.md)
- **24 Production-composed built-player vertical slice:** [plan](24-production-slice/plan.md) · [tasks](24-production-slice/tasks.md)
- **25 Multiplayer end-to-end gameplay validation:** [plan](25-multiplayer-e2e/plan.md) · [tasks](25-multiplayer-e2e/tasks.md)
- **26 Authored full-run campaign progression & completion:** [plan](26-full-run-campaign/plan.md) · [tasks](26-full-run-campaign/tasks.md)

## Definition of task completion

For every system, completing implementation means more than checking code-writing tasks. The owning agent must also complete the system's regression/reuse proof, run all automatically selected module tests, run any required module-local standalone-player scenario, validate affected top-level integration scenarios, search for superseded/duplicate ownership paths, and satisfy every close criterion in `tasks.md`. A green narrow unit test does not justify leaving an alternate production authority path in place.
