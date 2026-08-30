# Tasks — WorldBuilder Spatial Reservation System

## Audit findings / ownership map (2026-08-29)

- Canonical reservation contracts belong in `Assets/Game/WorldBuilder/Generation/Core` under the existing `MountingForce.WorldGen` engine-free integer-decimeter layer (`Int2`/`Int3`).
- Kentridge production migration target is `Content/Kentridge/KentridgeTownPlanner`: `IntersectsPlaced` and `IntersectsPlaza` currently own bespoke footprint/clearance rejection.
- Settlement/macro/road-facing boundaries already live in `SettlementPlotLayout`, `SettlementRoadFacingPlacement`, settlement model/composition policy, and planned route/access contracts; reservation integration must consume those semantics rather than take over topology or route solving.
- Vegetation integration belongs at `RegionEcologyPolicy` / `VegetationLayout`; ecology remains authoritative for species and baseline density while reservations only suppress/yield placement.
- Underground semantics already have an engine-free home in `HiddenSpaceContracts`; the proving harness should adapt those concepts rather than invent cave topology.
- The expected typed `StructuralSocket` symbol is not present on current `master`; integration must use the production structure-composition/attachment boundary that exists on this branch and must not depend on another agent's unmerged work.
- No mutable global registry, GameObject/collider authority, Unity Physics query, insertion-order tie break, or second permanent spatial index is allowed.
- Cost instrumentation is required in the reservation query result itself (buckets/candidates/narrow-phase counts plus elapsed ticks), with representative stress assertions before closure.

## Audit / architecture

- [ ] Trace current `KentridgeTownPlanner` placement, clearance, plaza, bounds, and public-access logic.
- [ ] Trace current macro-world settlement/region envelope and route-corridor implementation from the completed top-down world-layout feature.
- [ ] Trace the actual production implementation corresponding to world-feature-authoring R-001/R-002; identify whether placement-lattice/precedence claim code exists under renamed/refactored paths.
- [ ] Trace road-network constraint inputs and identify the exact boundary where reservation queries belong.
- [ ] Trace typed structural-socket clearance/overlap logic and identify the exact boundary where shared reservations should replace/localize it.
- [ ] Trace vegetation/ecology placement exclusions and find the production path suitable for shared reservation consumption.
- [ ] Trace existing cave/underground/protected-zone/occupancy abstractions that may already overlap this feature.
- [ ] Write/update an ownership map separating spatial reservation, terrain suitability, semantic topology/compatibility, and presentation/influence responsibilities.
- [ ] Add any discovered required migrations or regressions to this task list before implementation continues.

## Canonical contracts

- [ ] Define stable `ReservationId`, owner/source identity, provenance, category, precedence, and deterministic tie-break semantics.
- [ ] Define typed query/category masks; do not use string-name matching as the compatibility model.
- [ ] Define hard occupancy semantics.
- [ ] Define clearance/keep-open semantics.
- [ ] Define protected approach/travel corridor semantics.
- [ ] Define explicit compatible handoff/consumer semantics.
- [ ] Define soft-yield/exclusion semantics suitable for vegetation or similar consumers without moving ecology ownership into this layer.
- [ ] Define integer-space 2D footprint geometry.
- [ ] Define true 3D volume geometry with explicit vertical interval.
- [ ] Define route/tunnel corridor geometry (segment/capsule/polyline or equivalent) with a bounded broad-phase representation.
- [ ] Define stable conflict/result reason codes with conflicting ids/categories, relevant geometry, precedence result, compatibility result, and provenance.
- [ ] Document which concerns are explicitly **not** reservations: slope, soil/material suitability, water depth, biome preference, aesthetics, quest state, and other specialized scoring unless exposed through a deliberate adapter.

## Deterministic query/resolution implementation

- [ ] Implement/extend canonical immutable/derived reservation sources from authoritative semantic inputs.
- [ ] Implement a bounded reservation query view/snapshot for a region or planning window.
- [ ] Implement planner-local reservation state for bounded deterministic solvers without making it global world authority.
- [ ] Implement a deterministic integer-space broad phase; reuse existing placement-lattice indexing when appropriate instead of duplicating it.
- [ ] Ensure large macro envelopes and long corridors can be queried without eagerly materializing the entire world.
- [ ] Ensure authoritative reservation logic has no dependency on Unity GameObjects, colliders, Physics queries, streaming order, or thread scheduling.
- [ ] Reuse/generalize R-002-style precedence + stable identity for independently derived candidate conflicts.
- [ ] Prove planner-local commit ordering is deterministic wherever a local mutable reservation set is used.
- [ ] Add bounded-work instrumentation: scanned buckets/candidates, narrow-phase tests, accepted/rejected counts, and elapsed query/build cost.

## Kentridge settlement migration

- [ ] Replace/route `IntersectsPlaced` behavior through the shared reservation API.
- [ ] Replace/route `IntersectsPlaza`/plaza keep-clear behavior through the shared reservation API.
- [ ] Represent building physical footprint and required clearance as canonical reservations.
- [ ] Represent public entrance/approach space so generated buildings cannot block required access.
- [ ] Preserve district affinity, bounded candidate count, frontage/access semantics, and fixed-seed determinism.
- [ ] Add regression proving shuffled independent candidate processing cannot change the deterministic winner/layout contract.
- [ ] Add regression proving Kentridge cannot place two incompatible building claims in overlapping/clearance-conflicting space.

## Macro world + roads

- [ ] Adapt existing top-down settlement/region reserved envelopes into canonical reservation sources without duplicating their bounds.
- [ ] Make lower-level settlement/road/POI planners able to query macro reservations in a bounded planning window.
- [ ] Define and publish settlement road-arrival/gate/public-access corridor reservations.
- [ ] Make road route resolution consume relevant hard/envelope/corridor constraints through the shared query boundary.
- [ ] Make the resolved road publish protected road-core/clearance corridor reservations for later consumers.
- [ ] Add explicit compatibility for intended road-to-settlement entrance handoff.
- [ ] Add negative regression proving an unrelated building/POI cannot occupy the protected road/access corridor.
- [ ] Add positive regression proving the intended road is not rejected simply because it enters the settlement envelope through its declared handoff.

## Structural socket integration

- [ ] Route at least one real typed-socket child clearance/overlap validation path through the shared reservation service.
- [ ] Preserve typed-socket ownership of compatibility, orientation, support, attachment graph, and piece selection.
- [ ] Add regression proving two incompatible child clearance claims cannot silently overlap.
- [ ] Add regression proving a declared compatible attachment/handoff is allowed without globally relaxing overlap rules.

## Vegetation/ecology integration

- [ ] Make a production vegetation/ecology placement path query shared hard/clearance/soft reservations.
- [ ] Suppress incompatible vegetation inside hard structure/road claims.
- [ ] Yield/reduce placement in required clearance/approach zones as appropriate.
- [ ] Preserve regional ecology policy as the authority for allowed species/types and baseline density.
- [ ] Add regression proving reservation consumption does not alter world existence/placement by device tier.

## True 3D underground proving case

- [ ] Reuse an existing cave/underground production abstraction where appropriate; otherwise add the smallest deterministic reservation harness necessary.
- [ ] Reserve an underground chamber/tunnel volume beneath or adjacent to surface content.
- [ ] Prove XZ overlap with a surface building is allowed when Y ranges are safely separated.
- [ ] Prove a tunnel/chamber that truly intersects a protected building foundation/road volume is rejected or yields.
- [ ] Add an explicit shaft/stair/entrance connector handoff and prove the intended connector is allowed.
- [ ] Prove an unrelated underground consumer cannot exploit the connector compatibility.
- [ ] Exercise multiple underground claims through the same bounded broad-phase/query path.
- [ ] Keep WFC, full dungeon topology, full cave topology, room graph design, and content dressing out of scope.

## Inspection / debugging

- [ ] Add a placement/reservation inspector that answers why a candidate was accepted or rejected.
- [ ] Surface reservation owner/id, provenance, category, semantics, shape/bounds, conflicts, precedence decision, compatibility decision, and stable reason code.
- [ ] Add `WorldbuildingGalleryShowcase` visualization for surface footprints/clearances/corridors.
- [ ] Add a readable underground slice/layer visualization for 3D reservations.
- [ ] Keep debug GameObjects/rendering strictly non-authoritative.
- [ ] Add a deliberate rejected candidate to the gallery so failure reasoning is visually inspectable.

## Behavioral regression suite

- [ ] Fixed seed/world intent produces identical reservation ids and final resolved claims.
- [ ] Shuffled independent candidate insertion order produces identical winners.
- [ ] Shuffled region generation/eviction/regeneration order does not change derived reservation outcomes.
- [ ] Hard occupancy conflict rejects deterministically.
- [ ] Clearance conflict rejects/yields deterministically.
- [ ] Compatible handoff succeeds only for declared compatible consumer kinds.
- [ ] Legal vertical separation succeeds.
- [ ] True 3D collision fails.
- [ ] Macro settlement envelope is visible to lower-level queries.
- [ ] Protected road/public-access corridor remains unobstructed after settlement placement.
- [ ] Kentridge production path uses the shared reservation service.
- [ ] Structural-socket production path uses the shared reservation service.
- [ ] Vegetation/ecology production path uses the shared reservation data.
- [ ] Conflict diagnostics/reason codes are stable across repeated runs.
- [ ] Representative world-scale query work is bounded and does not devolve into whole-world scans.

## Built-application validation

- [ ] Build and run exact `WorldbuildingGalleryShowcase`.
- [ ] Capture durable evidence of surface hard claims, clearance, road/access corridor, compatible handoff, and an intentionally rejected conflict.
- [ ] Capture durable evidence of underground reservations showing legal vertical overlap and an illegal true collision/connector distinction.
- [ ] Validate physical content corresponding to the reservations, not debug overlays alone.
- [ ] Build/run the real `KentridgePlayableSlice` production integration after migration.
- [ ] Verify Kentridge loads without startup/runtime exceptions.
- [ ] Verify representative buildings/plaza remain non-overlapping and visually coherent.
- [ ] Verify public approach/access remains open and representative CharacterMotor traversal works.
- [ ] Verify road/settlement arrival corridor remains usable when road integration is present.

## Performance / blast radius / closure

- [ ] Measure reservation source construction cost.
- [ ] Measure broad-phase buckets/candidates and narrow-phase tests per representative query.
- [ ] Measure query timing under representative town, road, structural, vegetation, and underground workloads.
- [ ] Measure managed/native allocations and resident memory attributable to reservations.
- [ ] Measure impact on WorldBuilder generation time and region streaming/regeneration.
- [ ] Test long corridor and large macro-envelope behavior for bounded memory/query work.
- [ ] Confirm no one-GameObject/collider-per-reservation implementation was introduced.
- [ ] Confirm existing device/streaming/candidate budgets were not weakened.
- [ ] Check blast radius across existing WorldBuilder scenes/tests and fix regressions.
- [ ] Update `plan.md`/`tasks.md` with any discovered required work; no unchecked acceptance work may be omitted.
- [ ] Record regression test(s), runtime evidence, cost results, resolution summary, and fix commit in issue metadata.
- [ ] Complete all SceneIssues workflow gates and exact-SHA CI requirements before moving the issue to `closed`.
