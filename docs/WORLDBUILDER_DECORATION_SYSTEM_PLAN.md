# WorldBuilder Procedural Decoration System Plan

Status: active implementation plan
Branch: `agent/worldbuilding-decorations`
Base: `agent/worldbuilding-structures-caves`

## Goal

Build a reusable procedural decoration layer capable of furnishing castles, houses, inns, churches, ruins, caves, mines, camps, dungeons, and later world structures without hand-authoring every prop placement or creating thousands of one-off assets.

The system must support large content volume, deterministic regeneration, meaningful room composition, style/wealth/condition variation, and multiple visual representations while preserving the existing ownership boundary between game-authored structure vocabulary and engine/runtime geometry.

## Architectural placement

The existing project already separates structure vocabulary/configuration from authoring/build logic:

- `Assets/Game/Structures/Api` owns game-facing structure concepts and configuration.
- `Assets/Game/Structures/Runtime` owns deterministic authoring/build behavior.
- `Assets/Game/WorldBuilder` owns broader blueprint/world orchestration.

Decorations will follow the same model. The decoration vocabulary is game content, not a castle-only feature and not an engine primitive.

Recommended hierarchy:

`world -> structure -> space/room -> decoration scene -> prop placement -> render/build backend`

A castle, cottage, cave chamber, or mine tunnel provides semantic spaces. The decoration system converts those spaces plus context into deterministic decoration scenes. Prop authoring then emits geometry/render hooks through the appropriate backend.

## Core design principles

### 1. Recipes, not one-off assets

A small set of parameterized prop families should create large visual variety. For example, one bed family can vary size, materials, posts, canopy, blankets, pillows, wealth, style, and damage instead of requiring separate implementations for every bed.

Initial families:

- bed
- dresser/cabinet
- rug/carpet/runner
- painting/frame/tapestry
- wall torch

Later families include tables, chairs, benches, shelves, chests, barrels, crates, bookcases, fireplaces, chandeliers, banners, curtains, weapon racks, altars, candles, pottery, food, books, clutter, cave props, mining props, and natural formations.

### 2. Scene composition, not independent random placement

A believable room comes from relationships between props. Decoration scenes choose anchors and place dependent props relative to them.

Examples:

- bedroom: bed -> rug -> nightstand -> dresser -> painting/mirror -> chest -> light
- dining room: table -> chairs -> rug -> centerpiece -> cabinet -> wall lighting
- guard post: rack -> bench -> torch -> crates -> equipment clutter
- cave camp: fire -> bedrolls -> sacks/crates -> lantern -> tools
- shrine: altar -> candles -> banners/symbols -> offerings -> seating

The scene layer is therefore the main content multiplier.

### 3. Semantic placement constraints

Props must request meaningful surfaces/sockets rather than random coordinates.

Initial socket vocabulary:

- floor
- wall
- corner
- ceiling
- tabletop
- beside-anchor
- above-anchor
- doorway-side
- window-side

Placement constraints include footprint, clearance, facing, wall span, ceiling height, navigation clearance, exclusion zones, and relationships to existing anchors.

Most sockets should be derived procedurally from room/cave geometry rather than hand-authored.

### 4. Context drives variation

Every decoration pass receives deterministic context such as:

- structure type
- space/room type
- culture/faction/style
- wealth tier
- condition/damage tier
- biome/environment
- occupancy/use state
- world/structure/space seed

This allows the same recipes to create a servant bedroom, merchant bedroom, noble bedroom, abandoned bedroom, cave hideout, or ruined chamber without separate hard-coded builders.

### 5. Hybrid visual representation

Do not force all decorations into 10 cm world voxels.

Supported backends should eventually include:

- voxel stamps / structure operations for chunky world-integrated forms
- box assemblies for furniture and architectural props
- thin surfaces for rugs, paintings, banners, maps, and tapestries
- procedural meshes for smaller or curved details
- optional light emitters, emissive surfaces, particles, collision, and gameplay hooks

A prop recipe describes semantic intent; rendering/build backends decide how that intent becomes visible geometry.

### 6. Deterministic identity and serialization

Generated decorations must have stable identities derived from deterministic inputs, conceptually:

`world seed / structure id / space id / scene id / prop slot`

The generated baseline should not require serializing every prop in the world. Persistence should store deltas for props that were moved, destroyed, looted, opened, or otherwise changed.

### 7. Scale from the start

Static decoration must not imply one heavyweight Unity entity/GameObject per tiny prop across the entire world.

The architecture should permit:

- deterministic regeneration
- chunk/structure batching
- static geometry combination
- distance-dependent detail
- interaction metadata separate from rendering data
- promotion of only nearby/interactive props to richer runtime objects

## Proposed API model

The exact names may evolve during implementation, but the model should contain these concepts.

### DecorationContext

Carries deterministic style and world information used by all scene/prop generation.

Important fields:

- seed
- structure/space identifiers
- structure kind
- room/space kind
- style/culture id
- wealth tier
- condition tier
- biome/environment tags

### DecorationSpace

A semantic description of a furnishable volume or surface set. It should expose bounds plus derived usable surfaces/sockets and exclusion regions such as doors, stairs, major navigation lanes, hazards, and reserved gameplay areas.

### PlacementSocket

A candidate location with:

- socket kind
- position/facing
- usable dimensions
- supporting surface
- tags
- optional anchor relationship

### PropRecipe / PropDescriptor

Describes what a prop needs and how it may vary:

- prop family/kind
- accepted socket kinds
- footprint and clearance
- orientation rules
- style/material parameters
- optional secondary sockets
- render/build backend
- interaction flags

### DecorationScene

Selects and relates multiple props for a semantic purpose. Scenes are deterministic and may contain required, optional, and weighted prop slots.

### DecorationPlacement

Resolved output of generation. This is the stable, backend-independent description used for tests, serialization identity, and later geometry/render emission.

## Generation pipeline

1. Structure generation creates or identifies semantic spaces.
2. A space analyzer derives floor, wall, corner, ceiling, alcove, ledge, and exclusion information.
3. `DecorationContext` is constructed from structure/world metadata.
4. A room/space classifier selects one or more applicable `DecorationScene` recipes.
5. Scene generation chooses primary anchors first.
6. Dependent props are resolved relative to anchors.
7. Placement validation enforces clearance, navigation, bounds, overlap, and socket constraints.
8. Stable prop identities are assigned.
9. Placements are emitted to render/build backends.
10. Runtime persistence records only state deltas against deterministic identities.

## Castle integration

The first end-to-end target is one castle bedroom/interior space. Castle authoring should expose a semantic furnishable space rather than directly constructing beds or wall art.

The first vertical slice must place:

- bed
- dresser
- rug
- painting
- wall torch

The objects must be relationally composed, deterministic for a given seed, and visibly vary across seeds without violating room bounds/clearance.

After the basic bedroom works, add wealth/style/condition variation and integrate additional castle room scenes.

## Cave integration

Caves must use the same decoration pipeline rather than a separate cave-only furnishing implementation.

A cave space adapter will derive:

- walkable floor patches
- sufficiently vertical walls
- ceiling attachment candidates
- alcoves and ledges
- hazard/exclusion regions

The first cave scene will be a cave camp using the same placement/scene abstractions. Natural cave decoration families can then use the same system for stones, roots, mushrooms, crystals, bones, puddles, and formations.

## Testing strategy

Tests should favor deterministic semantic output rather than screenshots.

Required invariants include:

- same context/seed produces identical placements and identities
- changed seed can produce controlled variation
- no prop footprint escapes the space bounds
- required clearance/exclusion zones are respected
- primary anchors resolve before dependents
- required scene slots either resolve validly or fail explicitly
- wall props attach only to compatible wall sockets
- floor props remain supported
- stable identities do not depend on runtime enumeration order
- castle and cave adapters feed the same scene/placement system

Visual regression/look-dev scenes can be added after semantic correctness exists.

## Implementation order

1. Establish documentation and task tracking.
2. Add backend-independent decoration API vocabulary.
3. Add deterministic IDs/seeding and placement validation.
4. Add rectangular room surface/socket extraction.
5. Implement the first five prop families.
6. Implement `BedroomScene` and semantic tests.
7. Integrate one castle bedroom end-to-end.
8. Add style, wealth, and damage variation.
9. Add cave surface extraction and `CaveCampScene`.
10. Add richer render/build backends and lights/particles.
11. Add persistence delta hooks and batching/performance work.
12. Expand the prop/scene library after the foundation proves reusable.

## Non-goals for the first slice

- hundreds of prop families before the placement architecture is proven
- hand-placing decorations in each castle room
- requiring every prop to be a full Unity GameObject
- requiring every thin decorative object to occupy a 10 cm voxel slab
- cave-specific duplication of the room decoration architecture
- persistence of every untouched generated prop instance

## Definition of success for the first milestone

Given a castle bedroom space and seed, the system deterministically produces a coherent bedroom scene containing a bed, dresser, rug, painting, and torch; all placements satisfy semantic surface and clearance constraints; different seeds produce controlled variation; and the same API can subsequently accept a cave chamber without castle-specific assumptions.
