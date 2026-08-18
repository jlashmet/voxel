# WorldBuilder Decoration Fantasy Scope

Branch: `agent/worldbuilding-decorations`

This document is an addendum to `WORLDBUILDER_DECORATION_SYSTEM_PLAN.md`.

## Canonical documents

- `WORLDBUILDER_DECORATION_SYSTEM_PLAN.md` — architecture, generation pipeline, constraints, rendering backends, persistence and integration strategy.
- `WORLDBUILDER_DECORATION_MASTER_MANIFEST.md` — canonical exact stable content identities. This is the source of truth for the hundreds of individual objects; IDs 1-260 are implemented and IDs 261-400 are explicitly reserved by name.
- `WORLDBUILDER_DECORATION_CONTENT_CATALOG.md` — broader brainstorm and pack ideas. It may contain more ideas than are currently assigned stable IDs.
- `WORLDBUILDER_DECORATION_TASKS.md` — progress/checklist.

## World direction

The target is a fantasy RPG in a magical world. Decoration work should emphasize:

- magical homes, wizard towers, enchanted workshops and schools;
- castles, villages, inns, guild halls, markets and fantasy shops;
- temples, shrines, crypts, graveyards and occult spaces;
- monster lairs, beast nests, spider dens and occupied caves;
- adventurer guilds, quest boards, caravans and expedition camps;
- enchanted forests, fae clearings, druid spaces and magical nature;
- fantasy traps, puzzle chambers, treasure vaults and dungeon mechanisms;
- haunted ruins, magical corruption, cursed rooms and ancient seals;
- ordinary lived-in medieval/fantasy household, craft, farm, food and merchant content.

Do not spend content-library capacity on modern offices, factories, streetscapes, military bases, contemporary infrastructure, or other concepts that do not fit the fantasy setting. Ordinary fantasy guards, armor, weapons and fortifications are allowed where naturally required by a castle, dungeon, shop or story space, but they are not a primary content theme.

## Rendering rule

Content breadth is the priority during catalog expansion.

1. If a box/voxel assembly is substantially faster, use it as the first visible implementation.
2. If a curved/SDF/procedural primitive is approximately the same amount of work, use the smoother form immediately.
3. Keep semantic identity and placement independent from visual backend so later visual upgrades do not affect stable IDs, persistence, collision intent or scene composition.
4. Upgrade signature/high-visibility silhouettes first: wheels, barrels, cauldrons, urns, bowls, cushions, portals, magic circles, mushrooms, roots, eggs, crystals, chains and ornate furniture.
5. Crisp box geometry remains correct for many planked, timber, shelving, crate, rack, slab, board and sign forms.

## Scale target

The immediate stable-ID target is 400 archetypes and 60+ relational scene recipes. The current encoding leaves room through ID 1023 before a format revision is required. Reaching 400 should favor reusable content grammars and scene composition over bespoke per-object systems.
