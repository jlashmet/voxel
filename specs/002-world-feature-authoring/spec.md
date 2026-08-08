# Feature Specification: World Feature Authoring

**Feature Directory**: `002-world-feature-authoring`
**Created**: 2026-08-07
**Status**: Draft
**Input**: User description: "we need a way to specify entities and have the voxel system generate them. this should include houses, castles, underground caves, cliffs, waterfalls, etc. how can we do this"

## Overview

Today the world is terrain and nothing else: a height field with materials, plus whatever players carve into it. This feature adds a way to describe recognisable things — a house, a castle, a cave system, a cliff face, a waterfall — once, as data, and have every client materialise them identically as voxels wherever the world says they belong. Authors describe *what a thing is* and *where it may appear*; the world generator does the rest, at streaming time, from the seed.

Two decisions shape the rest of this specification. First, shapes are described **parametrically** — a house is a set of rules and numbers, not a captured lump of voxels — which keeps the data tiny and the variation rich, and makes it possible to ask a definition what it puts inside one particular box. Second, instances have **identity**: a castle is not just castle-shaped voxels, it is *that* castle, addressable by other systems, ownable, and protectable.

The distinction that shapes everything else: these are **generated world content**, not placed objects. They come out of the seed the way terrain does, they are made of the same voxels, and they are destructible on the same terms. A castle is a region of the brickmap that happens to look like a castle, and a player can take it apart wall by wall.

## User Scenarios & Testing *(mandatory)*

### Primary User Story

A world designer wants villages scattered through the foothills. They describe a house parametrically — footprint range, wall height, roof pitch, where openings go, which materials — and a placement rule saying houses cluster in groups of four to nine on ground flatter than a given slope, below a given altitude, never inside a cave mouth. They save it. Players exploring that terrain find villages that look deliberate rather than random, in different places for different world seeds, and identical for every player in the same world. The village square is a named anchor other systems can find. A player who burns a house down leaves a shell that stays burnt.

### Acceptance Scenarios

1. **Given** a catalogue containing a house definition and a placement rule, **When** two players in the same world walk to the same coordinates, **Then** both see the same houses in the same places with the same variations, voxel for voxel.

2. **Given** a castle whose footprint spans four regions, **When** a player approaches from any direction and the regions stream in one at a time, **Then** the castle assembles without seams, gaps, or duplicated walls, regardless of the order the regions arrived in.

3. **Given** a house standing on sloping ground, **When** the region generates, **Then** the house meets the ground on every side — no floating corners, no walls buried to the windows.

4. **Given** a player has destroyed half a castle wall, **When** they walk far enough away that the region is evicted and then return, **Then** the wall is still destroyed.

5. **Given** a cave system passing beneath a village, **When** both generate, **Then** neither opens unintentionally into the other, and any deliberate connection is present on both sides.

6. **Given** a designer adds a new feature definition to the catalogue, **When** they reload the world, **Then** the new feature appears without any engineering change to the generator.

7. **Given** a cliff feature, **When** it generates, **Then** the terrain it cuts remains structurally supported — the cliff does not leave unsupported terrain that immediately collapses.

8. **Given** a castle marked as protected, **When** a player attempts to destroy part of it, **Then** the alteration is rejected and the player is told why.

9. **Given** a house instance with a named door anchor, **When** any system asks that instance where its door is, **Then** it receives the same world coordinate on every client.

10. **Given** one house definition, **When** a designer changes only its parameter ranges, **Then** the variety of houses in the world changes without any new definition being written.

### Edge Cases

- Two features whose footprints overlap — resolution must be deterministic and produce the same winner on every client, not a race between whichever generated first.
- A placement rule that finds no valid site in a region: the region generates normally with nothing placed, rather than forcing a bad placement.
- A feature larger than a single region, generated while its other regions are not resident.
- A feature placed at the edge of the world's vertical extent — clipped or rejected rather than wrapping or overflowing.
- A catalogue that changes between sessions: previously generated worlds regenerate from the new catalogue, so a remembered landmark may move or vanish, and identities that referred to it no longer resolve.
- A cave carved through voxels a player has already filled in, or a structure generated where a player has already dug.
- A region whose feature content would exceed its memory budget.
- A definition that is malformed, references an unknown material, or produces content outside its declared footprint.
- Parameter ranges that admit a combination producing degenerate geometry — a roof steeper than the walls are tall, a door taller than the wall it sits in.
- **A player destroys the cliff behind a waterfall.** Water is static, so the fall remains in mid-air. Accepted and specified below rather than hidden.
- An instance that has been almost entirely destroyed but still carries ownership and protected status.
- Two instances placed close enough that their identities must still be distinct and stable.

## Requirements *(mandatory)*

### Functional Requirements

**Describing features**

- **FR-001**: Authors MUST be able to define a world feature as data, without changing engine code, and add it to a world's catalogue.
- **FR-002**: Feature shapes MUST be described parametrically — as rules and named parameters — rather than as captured voxel content.
- **FR-003**: A definition MUST declare each parameter's permitted range, and the system MUST reject values outside it.
- **FR-004**: A definition MUST declare the materials it uses, drawn from the world's material palette, so destruction behaviour follows the same material rules as terrain.
- **FR-005**: A definition MUST declare a footprint — the volume it may occupy — and the system MUST reject or clip any generated content falling outside it.
- **FR-006**: A definition MUST support controlled variation between instances through its parameters, so repeated instances are not identical.
- **FR-007**: Features MUST be composable: a definition may be assembled from other definitions, so a castle can be expressed as a keep, walls, towers, and a gatehouse rather than as one monolithic description.
- **FR-008**: A definition MUST be evaluable over an arbitrary sub-volume of its footprint, returning only the content falling inside that sub-volume. This is what allows a feature spanning several regions to be generated a region at a time.
- **FR-009**: The system MUST validate a catalogue and report definitions that are malformed, reference unknown materials, exceed their declared footprint, admit degenerate parameter combinations, or cannot satisfy their own placement constraints.

**Placing features**

- **FR-010**: Authors MUST be able to describe where a feature may appear using world properties — ground slope, altitude, proximity to other features, minimum spacing, clustering, and density per unit area.
- **FR-011**: Authors MUST be able to place a specific feature at a specific location, overriding rule-based placement, for landmarks that must exist at a known place.
- **FR-012**: Placement MUST be able to exclude regions of the world, so areas reserved for other purposes stay clear.
- **FR-013**: When two features contend for the same space, the system MUST resolve the conflict by a deterministic, declared precedence rather than by generation order.

**Generating features**

- **FR-014**: Feature generation MUST be deterministic: the same world seed and catalogue MUST produce identical voxels on every client and platform.
- **FR-015**: A region MUST be able to generate its own portion of any feature without requiring neighbouring regions to be resident, and portions generated separately MUST join without seams.
- **FR-016**: Feature generation MUST be repeatable in isolation: regenerating a single region after eviction MUST reproduce exactly the same voxels.
- **FR-017**: Features MUST adapt to the terrain they meet — foundations filled, ground cut, openings kept clear — so structures sit on the ground rather than floating above or sinking into it.
- **FR-018**: Features MUST be able to remove terrain as well as add to it, so caves and structure interiors are expressible.
- **FR-019**: Cave systems MUST generate connected passages, and any opening to the surface MUST be traversable from both sides.
- **FR-020**: Terrain-scale features (cliffs, ravines) MUST leave surrounding terrain structurally supported, so generation does not immediately trigger collapse.

**Water**

- **FR-021**: A definition MUST be able to place static water volumes — a source pool, a falling sheet, a receiving pool — as ordinary voxel content of a water material.
- **FR-022**: Water volumes MUST NOT flow, settle, or redistribute. They are shapes, fixed at generation, and remain where the definition put them.
- **FR-023**: Water volumes MUST be alterable by players on the same terms as any other material, and MUST NOT refill once removed.
- **FR-024**: Removing terrain that a water volume rests on or against MUST leave the water in place. This is a known consequence of static water and MUST be visible to authors so they can place water where it is unlikely to look wrong.

**Identity**

- **FR-025**: Every feature instance MUST have an identity that is identical on every client and stable across region eviction and regeneration within a world.
- **FR-026**: A definition MUST be able to declare named anchors — a door, a courtyard, a spawn point — which resolve to world coordinates for a given instance.
- **FR-027**: Other systems MUST be able to look up an instance by identity and obtain its definition, location, footprint, and anchors.
- **FR-028**: An instance MUST be able to carry mutable state that is not derived from the seed — at minimum ownership and protected status — with the server as the authority for it.
- **FR-029**: Mutable instance state MUST be replicated to clients, and a joining player MUST receive the current state rather than the initial state.
- **FR-030**: A protected instance MUST reject player alterations within its footprint, and the player MUST be told the alteration was rejected and why.
- **FR-031**: Instance identity MUST remain valid while the instance is materially destroyed, so ownership and protection survive damage.

**Living with the rest of the world**

- **FR-032**: Generated features MUST be destructible on exactly the same terms as terrain, with no feature-specific exemption beyond what the material palette and protected status express.
- **FR-033**: Player alterations MUST take precedence over generated content: a destroyed or added voxel MUST survive region eviction and regeneration for the life of the session.
- **FR-034**: Features MUST be represented in the distant view, so a castle on a ridge is identifiable from far away rather than appearing only on approach.
- **FR-035**: Feature generation MUST fit within the region streaming budget, so a player moving at maximum speed does not outrun world generation.
- **FR-036**: A region's feature content MUST respect the memory budget for that region, and the system MUST report rather than silently truncate when a placement would exceed it.
- **FR-037**: All clients MUST agree on which features exist and where, with the server as the authority, and a joining player MUST see the same world as players already present.

**Working on features**

- **FR-038**: Authors MUST be able to preview a definition in isolation, vary its parameters, and see the effect without regenerating the whole world.
- **FR-039**: The system MUST provide a way to inspect why a feature was or was not placed at a given location, so unexpected placement can be diagnosed.

### Key Entities

- **Feature Definition**: The reusable, parametric description of a kind of thing — house, tower, cave system, cliff. Declares parameters and their ranges, materials, footprint, anchors, and how it meets the ground. Not tied to any location.
- **Feature Catalogue**: The set of definitions available to a world, with their placement rules and precedence. Part of a world's identity: the same seed with a different catalogue is a different world.
- **Placement Rule**: The conditions under which a definition may appear — terrain properties, density, spacing, clustering, exclusions — and its precedence when contending with other features.
- **Feature Instance**: One occurrence in the world: a definition, a location, an orientation, and the parameter values drawn for it. Its shape is derived from the seed; its identity is addressable; its mutable state is stored.
- **Instance Identity**: The stable handle by which an instance is referred to, identical on every client, valid for as long as the instance exists in the world.
- **Anchor**: A named point declared by a definition and resolved to world coordinates per instance — a door, a spawn point, the centre of a courtyard.
- **Footprint**: The volume an instance claims, used for conflict detection, terrain adaptation, protection, and budget accounting.
- **Terrain Adaptation**: How an instance reconciles itself with the ground it lands on — what is filled beneath it, what is cut away, what is left clear.
- **Composition Slot**: A named attachment point where another definition may be placed, enabling castles built from parts.
- **Water Volume**: A static body of water material declared by a definition. A shape, not a simulation.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Two clients generating the same world from the same seed and catalogue produce identical feature content everywhere they overlap, with zero divergence across a full traversal of a populated area.
- **SC-002**: A designer with no engineering support can add a new feature type to the catalogue and see it in the world in under 30 minutes.
- **SC-003**: Features spanning region boundaries assemble without visible seams or gaps in 100% of cases, regardless of the order regions are generated.
- **SC-004**: A player moving at maximum travel speed through the most densely populated terrain never sees a region arrive without its features.
- **SC-005**: 100% of player alterations to generated features survive region eviction and return for the duration of a session.
- **SC-006**: A large structure on a ridge is identifiable as a structure from the maximum supported view distance.
- **SC-007**: Structures placed on sloping ground meet the terrain on all sides — no instance floats above the ground or is buried beyond its ground-floor openings.
- **SC-008**: Cave systems are traversable end to end, with every surface opening reachable from inside.
- **SC-009**: Adding features to a world does not push any region beyond its established memory budget.
- **SC-010**: A malformed feature definition, or one admitting degenerate parameter combinations, is reported before it reaches the world rather than producing broken geometry a player can find.
- **SC-011**: An instance's identity and every one of its anchors resolve to the same values on every client, and continue to do so after the region has been evicted and regenerated, in 100% of cases.
- **SC-012**: A change to an instance's ownership or protected status is observed by every player in the session, including players who join afterwards.
- **SC-013**: Attempts to alter a protected instance are rejected in 100% of cases, and the player is told why.
- **SC-014**: One definition produces at least ten visually distinguishable instances through parameter variation alone, with no additional definitions authored.

## Assumptions

- Feature *shape* is generated content, derived from seed and catalogue rather than stored per instance. This follows the existing model where terrain regenerates from the seed and only alterations are recorded.
- Instance *identity* is derived from the placement that produced it, so it needs no storage and is reproducible on any client. Only mutable state — ownership, protected status — is stored and replicated.
- The catalogue is authored ahead of time and fixed for the duration of a session; changing it produces a different world, and identities from a previous catalogue are not expected to resolve.
- Both rule-based scattering and explicit placement are needed. Villages want rules; a story landmark wants a coordinate.
- Feature content uses the existing material palette, and destruction behaviour comes from material class. Features introduce no new destruction rules beyond protected status.
- Water is a material like any other, distinguished by appearance rather than by behaviour.
- Persistence stays session-scoped, consistent with the existing decision that alterations are discarded at session end. Ownership and protected status share that lifetime.
- "Entities" in the request means *things in the world made of voxels*, with identity but without behaviour. Creatures, vehicles, and NPCs are a different problem.
- Placement reads the world as terrain generation leaves it; features do not react to player alterations made later in the session.

## Constraints

- **Determinism**: feature selection, placement, parameter draws, and voxel generation must produce identical results on every client and platform. No authoritative outcome may depend on floating-point arithmetic or GPU output.
- **Region-local generation**: a region must generate its slice of a feature knowing only the seed, the catalogue, and its own coordinates. Features cannot depend on neighbours being resident, and cannot be produced by a pass that sweeps the whole world. This is why FR-008 requires definitions to be evaluable over a sub-volume.
- **Server authority**: the server is the authority on world content and on mutable instance state; clients generate the same world rather than being told its contents voxel by voxel.
- **Bounded memory**: features must live within the existing per-region and per-world memory budgets. World size must not determine memory use, and per-instance stored state must be bounded by the number of instances players have interacted with, not by the number that exist.
- **Same storage, same rules**: features occupy the same brickmap as terrain, are traversed by the same collision, and are replicated by the same mechanism. No parallel representation.
- **Presentation tiering only**: device class may affect how features are drawn, never whether they exist, where they are, what they are made of, or who owns them.

## Out of Scope

- Simulation actors — creatures, NPCs, vehicles, or anything with behaviour. This feature produces world geometry with identity, not agents.
- Flowing or settling water, and water that responds to the terrain around it changing.
- Captured or hand-built voxel templates as an authoring form. Shapes are parametric.
- Interior gameplay content: furniture with function, loot placement, quest triggers. Anchors may mark where such things would go; providing them is another feature.
- Cross-session persistence of features, of alterations to them, or of ownership.
- Runtime editing of the catalogue by players — this is an authoring capability, not a building mechanic.
- Biome definition. Placement rules consume terrain properties; defining what a biome is belongs elsewhere.
- Structural realism beyond the existing support and collapse rules.

## Dependencies

- Terrain generation, which establishes the ground features adapt to and the properties placement rules read.
- The brickmap storage and its region streaming, which determine when and how features materialise.
- The material palette and its destruction classes, which give feature materials their behaviour.
- Structural support and collapse, which features must not violate on generation.
- The alteration and replication path, which must let player edits override generated feature content and carry mutable instance state.
- The existing protection mechanism, which protected instances extend rather than replace.
- The distant-view representation, which features must appear in to satisfy SC-006.
