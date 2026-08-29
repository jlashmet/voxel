# Plan — WorldBuilder Spatial Reservation System

## Objective

Build a canonical deterministic spatial claim/clearance/compatibility substrate that WorldBuilder planners can share without changing ownership of settlement topology, road solving, structural sockets, ecology, cave/dungeon topology, or future WFC/local-grammar generation.

The design must reconcile three scales of existing behavior:

- **macro semantic reservations** such as settlement/region envelopes and travel corridors;
- **independently derived feature candidates** whose overlap winner must be order-independent;
- **bounded planner-local solving** such as placing buildings, room pieces, or structural children one deterministic candidate at a time.

A mutable global occupancy registry is explicitly the wrong architecture because region eviction/regeneration and generation order must not change the world.

---

## Phase 1 — Audit and establish ownership boundaries

### 1.1 Trace current production spatial decisions

Inspect and document the actual production paths for:

- `KentridgeTownPlanner` candidate generation, `IntersectsPlaced`, `IntersectsPlaza`, planning bounds, frontage/access, and clearance;
- macro-world settlement/region envelopes and route/reachability reservations from the completed top-down world-layout work;
- current implementation status of `specs/002-world-feature-authoring`, especially R-001/R-002 placement-lattice/claim semantics and any production successor classes;
- road-network planning inputs that need settlement/building/crossing constraints;
- typed structural socket child-clearance/overlap checks;
- vegetation/ecology placement/exclusion checks;
- cave/underground feature generation and any existing occupancy/protected-zone abstractions.

### 1.2 Produce an ownership map before coding

Classify every discovered spatial rule as one of:

1. **reservation/claim responsibility** — belongs in this feature;
2. **terrain/geographic suitability** — remains with terrain/route/region systems;
3. **semantic/topological compatibility** — remains with settlements, roads, typed sockets, caves/dungeons, quests, etc.;
4. **presentation/influence** — remains with rendering/ecology/road influence, though it may consume reservation data.

Do not migrate a rule merely because it uses bounds. Avoid creating a universal constraint engine.

### Exit criteria

- Existing duplicate/adjacent mechanisms are listed.
- One canonical production owner is selected for spatial claims.
- R-002 order-independent precedence is either reused directly or deliberately adapted, not contradicted.
- `tasks.md` is updated with any newly discovered migrations.

---

## Phase 2 — Define the canonical reservation contracts

### 2.1 Identity and provenance

Define stable, inspectable reservation identity:

- stable `ReservationId`;
- semantic owner/source id;
- source kind/category;
- provenance sufficient to trace a claim back to macro world, route, settlement plot, generated feature candidate, structural child, or underground feature;
- deterministic precedence/tie-break data where independent candidates can compete.

Identity must survive eviction/regeneration for derived world content.

### 2.2 Reservation semantics

The contract must distinguish at least:

- **Hard occupancy** — incompatible physical overlap is forbidden.
- **Clearance / keep-open** — space must stay available around or in front of an owner.
- **Protected corridor / approach** — travel, doorway, gate, road arrival, tunnel connector, or similar path must not be blocked.
- **Compatible handoff** — designated consumer categories may consume/overlap a reservation intentionally, e.g. a road entering a settlement or a child attaching through a socket clearance.
- **Soft yield/exclusion** — a consumer such as vegetation should reduce/suppress placement rather than invalidating the owner.

Do not collapse these into one `bool occupied`.

### 2.3 Geometry

Support integer-space geometry sufficient for real consumers:

- axis-aligned 2D footprints/rectangles for cheap surface planning;
- true 3D boxes/volumes with explicit vertical interval;
- segment/capsule/polyline-style corridor claims or an equivalent exact-enough corridor representation;
- conservative AABB broad phase is allowed, but narrow phase must preserve legal vertical separation and corridor semantics.

Use the existing WorldBuilder/Core integer unit convention. Keep Unity transforms/Physics out of authoritative logic.

### 2.4 Compatibility and conflict results

Define explicit category/query masks and compatibility rules.

Every query/attempted reservation must be able to return:

- accepted/rejected/yield outcome;
- conflicting reservation ids/categories;
- geometry/bounds involved;
- precedence/tie-break decision where relevant;
- compatibility rule used;
- stable reason code suitable for tests and inspection.

### Exit criteria

- Contracts cover Kentridge buildings/plaza, macro envelopes, road/public-access corridors, structural-child clearance, vegetation yield, and a 3D underground example without special-case APIs.
- Legal tunnel-under-building overlap is representable.
- Illegal tunnel-through-foundation conflict is representable.
- Road-through-designated-settlement-entry is representable without globally allowing road/building overlap.

---

## Phase 3 — Implement deterministic bounded query/resolution

### 3.1 Reservation sources and snapshots

Provide a clean separation between:

- immutable/derived reservation **sources** reconstructed from authoritative world inputs;
- bounded query **views/snapshots** for a region/planning window;
- planner-local reservation **sets** used while deterministically solving one bounded plan.

Do not require all world reservations to be resident simultaneously.

### 3.2 Broad phase

Implement a bounded deterministic broad phase using integer-space bucketing/spatial hashing, existing placement-lattice indexing, or the closest production abstraction.

Requirements:

- deterministic enumeration;
- no GameObject/collider-per-reservation authority;
- bounded work for a region/planning window;
- large envelopes/corridors do not cause unrelated whole-world scans;
- no hidden dependence on insertion order for independent candidate conflict outcomes.

### 3.3 Conflict resolution

For independently derived candidates, use/generalize the existing stable `(precedence, stable identity)` concept so winners are invariant under shuffled generation/insertion order.

For planner-local placement, deterministic ordered candidate evaluation may commit into the local set, but the order itself must be stable and tested.

### Exit criteria

- Same seed/input creates identical reservation ids and decisions.
- Shuffling independent candidate insertion or region generation order produces identical winners.
- Query cost is bounded and measured in a representative stress window.
- Conflict diagnostics do not require scene objects.

---

## Phase 4 — Migrate real production consumers

### 4.1 Kentridge settlement placement

Replace or route the production behavior currently owned by bespoke building/plaza rectangle checks through the shared reservation API.

Preserve:

- deterministic bounded candidate sampling;
- district affinity;
- public access/frontage;
- lot/structure semantics;
- coherent non-overlapping layout;
- existing traversal.

Do not rewrite the town planner merely to demonstrate the API.

### 4.2 Macro-world envelopes

Adapt completed top-down settlement/region reserved envelopes into canonical reservation sources.

Lower-level planners must query the same semantic envelope rather than re-encoding its bounds.

### 4.3 Road/public-access corridors

Integrate the open/shared road path at the reservation boundary:

- road solver queries hard/conflicting claims;
- settlement exposes compatible arrival/gate/public-access corridor;
- road publishes its own protected core/clearance corridor;
- later structures/POIs cannot block required travel.

Keep route search, grade, cut/fill, surface influence, and rendering out of this feature.

### 4.4 Typed structural composition

Route at least one production child-clearance/overlap check through the shared reservation service.

Typed sockets continue to own:

- socket compatibility;
- orientation;
- attachment graph;
- support;
- module/piece selection.

### 4.5 Vegetation/ecology

Use the same reservation data in a production ecology placement path:

- hard structure/road cores suppress incompatible vegetation;
- clearance/approach zones yield appropriately;
- optional soft exclusion can reduce density;
- regional ecology remains authoritative for species and baseline density.

### Exit criteria

- At least four real consumers use the same canonical contract.
- No consumer-specific duplicate spatial index is introduced as a permanent parallel path.
- Existing output remains deterministic.

---

## Phase 5 — Prove true 3D underground use

Create a minimal deterministic underground reservation harness using the shared contracts, not a full cave/dungeon generator.

Required scenarios:

1. Surface building and underground tunnel overlap in XZ but not Y: **allowed**.
2. Tunnel/chamber actually intersects protected foundation/road volume: **rejected or candidate yields**.
3. Explicit entrance shaft/stair/connector is declared compatible with its intended surface handoff: **allowed**.
4. Unrelated underground consumer attempts the same protected overlap: **rejected**.
5. Multiple chamber/tunnel claims use the bounded query path.

If an existing cave lattice or cavern feature can be used safely as the proving consumer, prefer that over a synthetic duplicate.

### Exit criteria

- 3D semantics are proven by behavioral tests and runtime visualization.
- No surface-only projection hack is used.
- The work leaves a clean API for later dungeon/WFC/local-grammar generators.

---

## Phase 6 — Inspection and behavioral regressions

### 6.1 Placement inspector/debug overlay

Add a compact debug path that can answer:

> Why was this candidate allowed/rejected here?

Display/query at least:

- reservation id/owner/provenance;
- category;
- hard/clearance/corridor/soft semantics;
- bounds/shape;
- conflicting ids;
- precedence winner;
- compatibility decision/reason.

For the gallery, allow selected underground slices or another readable way to distinguish vertical claims.

Debug rendering is visualization only, never authority.

### 6.2 Automated regressions

Add focused tests for:

- stable identity/determinism;
- shuffled insertion/generation order;
- hard overlap;
- clearance conflict;
- compatible handoff;
- incompatible handoff;
- legal vertical separation;
- illegal true 3D collision;
- macro envelope query visibility;
- road/access corridor protection;
- Kentridge migration;
- structural-socket integration;
- vegetation yield/suppression;
- stable diagnostic reason codes;
- bounded broad-phase/query work.

### Exit criteria

- Tests exercise production paths, not only helper math.
- A regression would fail if a consumer silently reverts to bespoke overlap logic.

---

## Phase 7 — Built-application validation and cost

### 7.1 WorldbuildingGalleryShowcase

In the exact built application, create/extend a deterministic demonstration showing:

- surface building/plaza/envelope claims;
- road/public-access corridor and compatible handoff;
- a rejected conflicting candidate;
- underground tunnel/chamber claims at multiple heights;
- legal stacked content and an illegal true collision;
- structural/vegetation consumer behavior;
- reservation inspection/debug visualization.

### 7.2 Kentridge production validation

Validate the real Kentridge production path after migration:

- town loads and remains coherent;
- buildings do not overlap;
- public access remains open;
- road/arrival corridor remains usable where integrated;
- CharacterMotor can traverse representative paths;
- no startup/runtime exceptions.

### 7.3 Cost/blast radius

Measure and record:

- reservation source construction time;
- broad-phase candidates per query;
- query time distribution for representative planners;
- allocations and resident memory;
- world-build impact;
- region eviction/regeneration behavior;
- long corridor / macro envelope cost;
- impact on existing WorldBuilder scenes.

Do not weaken existing device/streaming/candidate budgets.

### Completion gate

Close only when every `tasks.md` checkbox and `issue.json` acceptance criterion is satisfied with behavioral regression evidence, exact built-application visual/runtime evidence, and measured cost.
