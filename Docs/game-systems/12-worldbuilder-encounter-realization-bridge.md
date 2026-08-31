# 12. WorldBuilder encounter realization bridge

**Status:** Approved

## Purpose

Connect authored encounter meaning to the concrete generated world without teaching encounter gameplay about WorldBuilder internals or teaching WorldBuilder about encounter runtime state.

The existing WorldBuilder already resolves authored `SiteRef` roles to deterministic `ResolvedSiteId` values and separately binds authored `NpcRef` placements to those resolved sites. This system is therefore a thin composition bridge from that realized world state into the encounter runtime designed in system 05.

Conceptually:

`EncounterRef + SiteRef + optional NpcRefs`

→ WorldBuilder realization

→ `EncounterWorldBinding`

→ encounter activation/membership/lifecycle

## Existing foundation to reuse

- `SiteRoleResolver` resolves semantic authored site roles to generated `ResolvedSiteId` values while enforcing archetype, hierarchy, capability, reachability, and distance constraints.
- `NpcPlacementResolver` binds semantic `NpcRef` values to the resolved generated sites.
- Physical positions are intentionally left to the selected site's realization adapter rather than embedded in semantic planning.
- Gameplay character identity is handled by system 03; encounter lifecycle is handled by system 05.

This design does not create a second world-placement or spawn-search system.

## 1. Bind encounters to semantic sites, not coordinates

Campaign/content authors encounter placement using semantic identities such as:

- `EncounterRef`
- `SiteRef`
- optional persistent `NpcRef` participants

Shared encounter gameplay must not contain scene coordinates or know which generated candidate WorldBuilder selected.

WorldBuilder remains responsible for deciding where an authored site physically exists in a generated world.

## 2. Narrow encounter-world binding contract

Composition produces a narrow semantic binding for encounter runtime consumption, conceptually `EncounterWorldBinding`.

It may contain only the information encounter lifecycle actually requires, such as:

- `EncounterRef`
- authored `SiteRef`
- resolved site/world identity
- resolved persistent character identities where authored NPCs participate
- an opaque placement/entry capability usable for temporary participant realization

It must not expose the full WorldBuilder planning graph or solver implementation to encounter gameplay.

## 3. Resolve from the realized world once

Encounter-world bindings should normally be produced from the same realized world/session state used by cutscenes, NPC placement, story locations, and other gameplay composition.

Conceptually:

`CampaignBlueprint + SiteResolutionResult + NPC/site realization`

→ encounter-world bindings

Activation can then use deterministic lookup rather than rerunning spatial searches each time an encounter starts.

## 4. Persistent NPC participants remain existing characters

When an encounter references authored persistent NPCs:

`NpcRef → CharacterId`

must resolve through the authoritative gameplay character registry/composition established by system 03.

The bridge passes those existing character identities to system 05. It does not instantiate encounter-local duplicates.

This preserves vitality, inventory, autonomous-life state, history, and other character-owned state before and after the encounter.

## 5. Temporary characters use site realization capabilities

Temporary encounter participants are requested by system 05 through character definitions/lifecycle.

This bridge supplies the realized site context in which they may enter. The site's realization layer remains responsible for producing valid physical placement.

Encounter code must not create a parallel generic solution for:

- terrain probing
- random spawn-coordinate search
- collision-clearance placement
- spatial reservation
- path/entrance discovery

unless a demonstrated encounter-specific requirement cannot be expressed through existing world-realization capabilities.

## 6. Encounter spatial needs are semantic site capabilities

If an encounter requires a spatial property that materially affects whether a generated site is usable—for example, sufficient staging/entry capacity—that requirement should be expressed as a semantic WorldBuilder site capability where practical.

WorldBuilder then validates and resolves a compatible generated site before gameplay activation.

The encounter runtime should not discover only after activation that its authored location is physically unsuitable.

## 7. Activation policy remains outside this bridge

This system answers:

> Where and against which existing world identities does this authored encounter exist?

System 05 answers:

> Is the encounter active, who is enrolled, when does it resolve, and how is membership cleaned up?

Campaign/story/interaction policy decides when an `EncounterRef` should be requested.

The bridge does not decide that entering a particular site automatically begins combat or an encounter.

## 8. WorldBuilder remains independent of encounter state

WorldBuilder continues to resolve authored world intent without depending on runtime encounter state.

It must not query or own concepts such as:

- encounter active/completed state
- combat winner
- character defeat state
- encounter reset rules

Dependency direction is:

WorldBuilder realization → composition bridge → encounter runtime.

## 9. Deterministic binding diagnostics

Where possible, invalid authored integration should fail during world/session composition rather than during live encounter activation.

Examples include:

- required `SiteRef` has no successful realization
- persistent authored `NpcRef` has no character binding
- required world capability cannot be satisfied

Failures should be semantic diagnostics, not late `GameObject.Find()`/null-reference failures.

## 10. Keep world placement and gameplay identity distinct

The bridge preserves the distinction among:

- `SiteRef`: authored semantic place
- `ResolvedSiteId`: generated realization selected for this world
- `CharacterId`: authoritative gameplay character identity

These identities must not be collapsed into Unity `GameObject` or `Transform` references in shared gameplay APIs.

## 11. Follow the existing composition-adapter pattern

This bridge should follow the same architectural philosophy as the existing cutscene/world actor integration: semantic identities are translated in composition into a narrow runtime-facing contract.

`Game.Encounters` should not need a broad dependency on all of `Game.WorldBuilder.Runtime`.

## 12. Boundary with system 13

System 12 covers realized places and NPCs needed by encounter gameplay.

System 13 covers realized world objects/features becoming generic actionable gameplay interactables.

Examples:

- ambush/confrontation location context → system 12
- chest, door, lever, secret panel, conversation target → system 13

## Reuse / integration proof

### Temporary road encounter

1. Campaign authors an `EncounterRef` against a semantic world site/route location.
2. WorldBuilder resolves that location.
3. This bridge supplies the realized placement context.
4. System 05 creates/enrolls temporary characters there.
5. Encounter gameplay contains no hardcoded world coordinates.

### Persistent town confrontation

1. WorldBuilder has already placed persistent town NPCs.
2. Their `NpcRef` values resolve through gameplay composition to existing `CharacterId` values.
3. This bridge supplies those identities and their realized site context.
4. System 05 temporarily enrolls the same characters.
5. After the encounter they remain the same persistent town characters and resume normal character/AI lifecycle.

The second scenario is the primary reuse proof because it demonstrates integration with persistent world characters rather than an encounter-only actor universe.

## Out of scope

- world/site generation and constraint solving
- spatial-reservation implementation
- character lifecycle and identity (system 03)
- character AI (system 04)
- encounter lifecycle/membership (system 05)
- combat rules (system 01)
- story/campaign trigger policy
- generic interactables (system 13)
- scene-specific spawn coordinates

## Architectural constraints

- Shared APIs remain semantic and configuration-driven.
- Scene/place/campaign-specific identities and policy remain in content/composition.
- Reuse existing WorldBuilder site/NPC realization instead of duplicating it.
- Do not expose WorldBuilder solver internals across the gameplay boundary.
- Persistent authored NPCs must resolve to existing authoritative gameplay characters.
- Physical placement remains a world-realization responsibility rather than encounter policy.
