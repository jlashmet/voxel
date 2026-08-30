# Tasks — WorldBuilder Spatial Reservation System

## Audit / ownership

- [x] Trace `KentridgeTownPlanner` placement, plaza, bounds, clearance, frontage and public-access behavior.
- [x] Trace current macro-world settlement/region envelopes and the actual road-network constraint boundary from current `master`; `WorldRoadNetwork` is now the canonical resolved road aggregate and explicitly owns shared road geometry/clearance sampling.
- [x] Trace the completed R-001/R-002 placement/precedence references under current names; no separate reusable canonical reservation/precedence implementation is present on the reconciled source tree, so this feature remains the canonical reservation owner.
- [x] Trace vegetation/ecology ownership (`RegionEcologyPolicy` / `VegetationLayout`): ecology keeps species and density authority; reservations may only suppress/yield placement.
- [x] Trace underground ownership (`HiddenSpaceContracts`): reservations may adapt realized volumes/connectors but do not own cave/WFC topology.
- [x] Trace current architecture geometry/attachment boundary (`StructureSiteGeometry` plus Kentridge architecture grammar/placement). The feature-requested typed `StructuralSocket` symbol is not on current `master`, so this branch must not depend on agent-5/unmerged work.
- [x] Record ownership split in `plan.md`: reservation identity/geometry/query/conflict belongs in engine-free Core; terrain suitability, topology, compatibility/orientation/support, ecology policy, quest state and presentation stay with their owners.
- [x] Re-check current `origin/master` and reconcile it into `fixes/agent-7`; current master is `e95324aeaef619cb49d84bf2b07f770184bead81` and the two-parent reconciliation commit is `2b6c5b7912d30b4b923298a4d394e813cc3228d5`.

## Canonical reservation contracts

- [x] Add stable semantic `ReservationId`, owner identity, provenance, category, precedence and deterministic stable-id tie break.
- [x] Add typed consumer/category masks; no string-name compatibility rules.
- [x] Add hard occupancy, clearance/keep-open, protected corridor, explicit compatible-handoff and soft-yield semantics.
- [x] Add integer-decimetre half-open 3D bounds and route/corridor geometry with explicit Y ranges.
- [x] Keep reservation authority engine-free: no GameObjects, colliders, Physics queries, streaming order or thread scheduling.
- [x] Document non-reservation concerns in code/plan (terrain suitability, biome/ecology scoring, water/soil, aesthetics, quest state, route solving, attachment compatibility).
- [x] Add stable query diagnostics carrying decision/reason, conflict id/owner/category/semantics/bounds/precedence/compatibility/provenance.
- [x] Add per-query work/timing metrics (buckets, broad-phase candidates, narrow-phase tests, intersections, reject/yield counts, stopwatch ticks).

## Deterministic bounded query/resolution

- [x] Add immutable `SpatialReservationSnapshot` scoped to a caller-owned planning window.
- [x] Add deterministic integer XZ broad phase clipped to the planning window so macro envelopes/long claims do not materialize world-scale buckets.
- [x] Add planner-local mutable reservation state without creating global world authority.
- [x] Add independent-candidate resolver ordered by precedence then stable identity so caller insertion order cannot change winners.
- [x] Add true vertical separation before narrow-phase conflict evaluation.
- [ ] Validate planner-local commit order and source construction cost with exact tests/measurements.
- [x] Verify there is no competing current-master reservation precedence model to align with; road resolution policy remains separate and is consumed only through its published aggregate.

## Kentridge production migration

- [x] Remove bespoke `IntersectsPlaced` / `IntersectsPlaza` placement rejection and route candidate placement through shared reservations.
- [x] Publish building hard footprint and 18dm clearance claims.
- [x] Publish plaza keep-clear claim.
- [x] Publish per-site public entrance/approach protected corridors.
- [x] Publish temporary inferred Kentridge road segments as protected road corridors on the pre-road-integration base.
- [ ] Replace temporary inferred Kentridge road claims with claims adapted from canonical `WorldRoadNetwork` route geometry/clearance on the reconciled head; do not reproduce road polyline-distance math.
- [x] Preserve district affinity, fixed hash/candidate sequence, bounded 256-attempt budget and existing frontage/access semantics.
- [x] Add deterministic fixed-seed shared-claim regression.
- [x] Add regression that Kentridge building footprints do not violate another building's clearance/plaza claim.
- [ ] Run all pre-existing Kentridge layout/vegetation regressions and repair any layout/access regression caused by the migration.
- [ ] Validate real `KentridgePlayableSlice` in the built application, including startup, representative buildings/plaza, open public approaches and CharacterMotor traversal.

## Macro world + roads

- [ ] Add an adapter that converts canonical `WorldRoadNetwork` routes into bounded `RoadCore`/`RoadBuffer` reservation claims while preserving road-network ownership of widths, shoulders, clearance and routing.
- [ ] Adapt the actual production macro settlement/region envelope into canonical claims without duplicating source bounds.
- [ ] Make lower-level settlement/road/POI planning query those claims through a bounded snapshot at an existing policy boundary, without taking over route solving/grade policy.
- [ ] Publish the real settlement arrival/gate/public-access handoff claim.
- [x] Core semantics support explicit road-to-settlement compatible handoff.
- [x] Add positive/negative semantic regressions: intended road handoff succeeds; unrelated building is rejected.
- [ ] Add production regression proving the actual macro envelope/road path uses the shared service.

## Structural composition integration

- [x] Add a shared `StructuralChildClearance` claim adapter in Core that preserves attachment-policy ownership.
- [ ] Route one actual production architecture child/site-clearance path through shared reservation queries using `StructureSiteGeometry` / the Kentridge architecture placement boundary available on current master.
- [ ] If typed `StructuralSocket` lands on master before closure, merge master and route that real typed-socket path; do not reimplement or copy another assignment's socket code.
- [ ] Until typed sockets exist on master, exercise `StructuralSocketReservation` through the generic accepted-attachment reservation adapter so accepted attachment space beats vegetation without pretending to own socket compatibility/orientation/support.
- [ ] Add production regression proving incompatible child clearances cannot overlap.
- [ ] Add production regression proving only a declared compatible attachment/connector handoff is relaxed; orientation/support/piece selection remain architecture-owned.

## Vegetation/ecology integration

- [x] Core semantics support vegetation yield against clearance/soft reservations.
- [ ] Make `KentridgeVegetationLayout` / `KentridgeVegetationPlanner` consume a caller-supplied shared reservation snapshot before accepting placement.
- [ ] Publish accepted vegetation as `VegetationSoft`/`VegetationHard` claims where later consumers require ownership; suppress/yield inside road, structure, socket, clearance and public-approach claims as appropriate.
- [ ] Preserve `RegionEcologyPolicy` as authority for species/types and baseline density and preserve existing canonical road influence behavior rather than duplicating it.
- [ ] Add regression proving reservation consumption does not make world existence/placement device-tier dependent.

## True 3D underground integration

- [x] Add `HiddenSpaceVolume` adapter from actual `SiteHiddenSpaceRealization` + site origin into canonical 3D claims.
- [x] Add semantic regression: XZ-overlapping tunnel below a surface building is legal, while a real XYZ collision is rejected.
- [x] Add semantic regression: explicit connector handoff succeeds only for `Connector`; unrelated underground consumer cannot exploit it.
- [ ] Route `KentridgeHiddenSpaceBatchPlanner`/`KentridgeHiddenSpacePlanner` realized hidden-space volumes/connectors through one bounded reservation snapshot using real vertical extents.
- [ ] Add production regression using real hidden-space realization data, proving vertically separated overlaps are accepted and true XYZ collisions are rejected.
- [ ] Keep WFC/full dungeon/cave topology/content dressing out of scope.

## Inspection / gallery visualization

- [x] `ReservationQueryResult.Describe()` provides a deterministic textual placement inspector with decision/reason and conflicting claim metadata.
- [ ] Locate the current `WorldbuildingGalleryShowcase` runtime/scene hook outside Generation and add non-authoritative physical/debug visualization for surface hard footprints, clearance and route/access corridors using real production claims.
- [ ] Add readable underground slice/layer visualization using true 3D claims.
- [ ] Include a deliberate rejected candidate with visible reason/owner/provenance.
- [ ] Ensure debug rendering is strictly non-authoritative and the scene also contains physical content corresponding to the claims.

## Regression / cost coverage already authored (must still pass CI)

- [x] Fixed semantic inputs produce stable ids.
- [x] Shuffled independent candidates produce identical precedence winners.
- [x] Equal-precedence tie uses stable identity rather than insertion order.
- [x] Hard occupancy rejects deterministically.
- [x] Clearance rejects normal placement and yields vegetation.
- [x] Soft reservation yields.
- [x] Compatible handoff is limited to declared consumer kinds.
- [x] Legal vertical separation succeeds; true 3D collision fails.
- [x] Kentridge production planner publishes shared building/plaza/access/road claims on the pre-road-integration adapter; update this proof to canonical roads before closure.
- [x] Diagnostics include stable reason and conflicting claim metadata.
- [x] Bounded-window stress regression excludes distant claims and asserts bounded bucket/candidate/narrow-phase work.
- [ ] Add region eviction/regeneration/order regression against the actual production source adapter(s).
- [ ] Add production macro-road, architecture, vegetation and hidden-space regressions after those adapters are wired.
- [ ] Run targeted EditMode regression suite on an exact feature SHA and record the green request/run.

## Performance / blast radius

- [ ] Measure snapshot/source construction cost and representative query timing.
- [ ] Record bucket/candidate/narrow-phase metrics for town, road, structural, vegetation and underground workloads.
- [ ] Measure managed/native allocation/resident-memory impact with the repository-supported runtime tooling where available.
- [ ] Measure WorldBuilder generation/streaming/regeneration impact with the repository-supported gate.
- [x] Long macro envelope is clipped to the caller window rather than producing world-scale buckets (regression authored; not yet CI-validated).
- [x] No one-GameObject/collider-per-reservation authoritative implementation was introduced.
- [x] Existing planner candidate budget was not increased.
- [ ] Check blast radius across WorldBuilder/Kentridge existing tests/scenes and fix in-scope regressions.

## Workflow gates / closure

- [x] Check current `SceneIssues/feature-readme.md` on current `origin/master`; it is absent, while current `SceneIssues/README.md` explicitly declares itself the sole SceneIssue workflow authority, so use that file plus this assignment README.
- [x] Re-fetch current `origin/master`, reconcile allowed prerequisite changes into `fixes/agent-7`, and ensure only this assignment plus in-scope production/test files are modified (`2b6c5b7912d30b4b923298a4d394e813cc3228d5`).
- [ ] Validate compile/static source state and update `plan.md`/`tasks.md` for any discovered work.
- [ ] Request the final targeted CI only through `ci-test/fixes/agent-7`; never put `.github/test-request.json` on the feature branch and never replace a queued request.
- [ ] Obtain green exact-SHA targeted EditMode CI and record run/evidence.
- [ ] Complete pending metadata on `fixes/agent-7` after the green exact-SHA CI gate.
- [ ] Build/run exact `WorldbuildingGalleryShowcase`; capture durable surface + underground + rejected-candidate evidence and physical-content validation.
- [ ] Build/run real `KentridgePlayableSlice` and capture durable runtime/traversal evidence.
- [ ] Complete pending metadata after every workflow/runtime gate.
- [ ] Record regression tests, runtime evidence, cost results, blast radius, resolution summary and fix commit in issue metadata.
- [ ] Move this assignment `open -> pending -> closed` only when every checkbox and acceptance criterion is complete; set `status=fixed` and `resolvedUtc`.
- [ ] Merge current `origin/master` into `fixes/agent-7`, push feature branch, then non-force push that exact head to `origin/master`; if master advances, fetch/merge/retry.
