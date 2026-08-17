# Worldbuilding Repository Inventory

This note records evidence for Phase 0 of `worldbuilding-plan.md`. It is intentionally factual: checklist items stay open until every part of the corresponding inventory task has been verified.

## Castle authoring

The dedicated structure module is currently castle-specific.

### Public surface

- `Assets/Game/Structures/Api/CastlePlan.cs` is the public castle plan data surface.
- `Assets/Game/Structures/Runtime/CastlePlanner.cs` owns deterministic castle planning policy.
- `Assets/Game/Structures/Runtime/CastleAuthoringBuild.cs` orchestrates the authored castle build and delegates geometry to focused authorers.

### Runtime authoring pieces

The current castle implementation is split across focused files rather than one reusable architectural-component library:

- `CastleSiteAuthoring.cs` — site/terrain integration.
- `CastleKeepCoreAuthoring.cs` — keep core.
- `CastleKeepRoomAuthoring.cs` — keep rooms/interior volumes and openings.
- `CastleKeepRooflineAuthoring.cs` — keep roofline/top treatment.
- `CastleKeepOrielAuthoring.cs` — keep oriel detail.
- `CastleTowerAuthoring.cs` — towers.
- `CastleCurtainAuthoring.cs` — curtain walls.
- `CastleGatehouseAuthoring.cs` — gatehouse.
- `CastleCourtyardAuthoring.cs` — courtyard.
- `CastleGreatHallWingAuthoring.cs` — great-hall wing.
- `CastleChapelAuthoring.cs` — chapel.
- `CastleDungeonAuthoring.cs` and `CastleDungeonSideChambers.cs` — dungeon/interior underground spaces.
- `CastleCaveAuthoring.cs` — cave carving and cave-specific decoration.
- `CastleLandscapeAuthoring.cs` — surrounding landscape treatment.

### Existing tests

The structure module currently has castle-focused tests:

- `Assets/Game/Structures/Tests/CastlePlannerTests.cs`
- `Assets/Game/Structures/Tests/CastleAuthoringBuildTests.cs`

The remaining WB002 work is to trace every showcase/call site and record the public defaults/expected compatibility output before checking WB002 complete.

## Castle cave reuse verdict — WB004 / WB009

`Assets/Game/Structures/Runtime/CastleCaveAuthoring.cs` is castle-local, not a reusable cave generator.

It owns the cave layout, castle-relative coordinates, material/decorative choices, carving behavior, and its ellipsoid/noise carving routine. It does not delegate to a generic cave core. The authoritative authoring path also uses floating-point trigonometric/math operations, so preserving the implementation as a generic deterministic cave engine would conflict with the project's integer deterministic generation constraint.

**Reuse verdict:** there is no generic cave algorithm here that should remain as the shared cave implementation. Useful intent/defaults may be migrated as a castle cave compatibility preset, but the algorithm itself must move to the generic deterministic cave path described by Phase 4.

**Chosen migration path:**

1. Define the reusable deterministic cave configuration/generation path in WB051-WB062.
2. Preserve relevant castle entrance/layout/material intent as data/configuration rather than private cave code.
3. Route the castle's `Cave` attachment through the same generic path used by standalone caves (WB049/WB062-WB063).
4. Retire/deprecate the duplicate castle-local carving algorithm after compatibility and reachability tests cover the migration.

This resolves WB004 and WB009.

## Shared architectural helper inventory — partial WB006

There is already useful castle-specific code for walls, towers, openings/interiors, roofline treatment, courtyards, foundations/site adaptation, and related details. The refactor should harvest these behaviors into shared components instead of cloning them. WB006 remains open until the helper contracts and dependencies are inspected in detail enough to distinguish reusable geometry from castle-only semantics.

## House/cottage and city/settlement — open WB001/WB003

No house/cottage or city/settlement implementation exists in the dedicated `Assets/Game/Structures` module. The broader world hierarchy lives under `Assets/Game/WorldBuilder`; Phase 0 still needs to trace the actual generation/call path (or conclusively record that no current implementation exists) before WB001 or WB003 can be checked.

## Checklist discipline

Only WB004 and WB009 are complete from this inventory batch. WB001-WB003 and WB005-WB008 remain open until their full requested evidence is recorded.