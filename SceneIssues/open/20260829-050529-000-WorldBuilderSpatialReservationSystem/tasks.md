# Tasks — WorldBuilder Spatial Reservation System

## Audit / ownership

- [x] Trace `KentridgeTownPlanner` placement, plaza, bounds, clearance, frontage and public-access behavior.
- [ ] Trace current macro-world settlement/region envelopes and the actual road-network constraint boundary from current `master`.
- [ ] Trace the completed R-001/R-002 placement/precedence implementation under its current names and reuse it where it is actually present.
- [x] Trace vegetation/ecology ownership (`RegionEcologyPolicy` / `VegetationLayout`): ecology keeps species and density authority; reservations may only suppress/yield placement.
- [x] Trace underground ownership (`HiddenSpaceContracts`): reservations may adapt realized volumes/connectors but do not own cave/WFC topology.
- [x] Trace current architecture geometry/attachment boundary. The feature-requested typed `StructuralSocket` symbol is not on the source `master` used for this branch, so this branch must not depend on agent-5/unmerged work.
- [x] Record ownership split in `plan.md`: reservation identity/geometry/query/conflict belongs in engine-free Core; terrain suitability, topology, compatibility/orientation/support, ecology policy, quest state and presentation stay with their owners.
- [ ] Re-check current `origin/master` before final integration in case the typed-socket or macro-road prerequisites have landed; integrate only after merging current master and without editing another assignment.

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
- [ ] Verify/align precedence behavior with the current R-002 implementation if/when found on current master; do not maintain two competing canonical precedence models.

## Kentridge production migration

- [x] Remove bespoke `IntersectsPlaced` / `IntersectsPlaza` placement rejection and route candidate placement through shared reservations.
- [x] Publish building hard footprint and 18dm clearance claims.
- [x] Publish plaza keep-clear claim.
- [x] Publish per-site public entrance/approach protected corridors.
- [x] Publish inferred Kentridge road segments as protected compatible road corridors in the derived reservation snapshot.
- [x] Preserve district affinity, fixed hash/candidate sequence, bounded 256-attempt budget and existing frontage/access semantics.
- [x] Add deterministic fixed-seed shared-claim regression.
- [x] Add regression that Kentridge building footprints do not violate another building's clearance/plaza claim.
- [ ] Run all pre-existing Kentridge layout/vegetation regressions and repair any layout/access regression caused by the migration.
- [ ] Validate real `KentridgePlayableSlice` in the built application, including startup, representative buildings/plaza, open public approaches and CharacterMotor traversal.

## Macro world + roads

- [ ] Adapt the actual production macro settlement/region envelope into canonical claims without duplicating source bounds.
- [ ] Make lower-level settlement/road/POI planning query those claims through a bounded snapshot.
- [ ] Publish the real settlement arrival/gate/public-access handoff claim.
- [ ] Make real road resolution consume relevant hard/envelope/corridor constraints at its existing policy boundary; reservations must not take over route solving/grade policy.
- [ ] Publish resolved road core/clearance corridor claims for later consumers.
- [x] Core semantics support explicit road-to-settlement compatible handoff.
- [x] Add positive/negative semantic regressions: intended road handoff succeeds; unrelated building is rejected.
- [ ] Add production regression proving the actual macro envelope/road path uses the shared service.

## Structural composition integration

- [x] Add a shared `StructuralChildClearance` claim adapter in Core that preserves attachment-policy ownership.
- [ ] Route one **actual production architecture attachment/child-clearance** path through shared reservation queries using the architecture boundary available on current master.
- [ ] If typed `StructuralSocket` lands on master before closure, merge master and route that real typed-socket path; do not reimplement or copy another assignment's socket code.
- [ ] Add production regression proving incompatible child clearances cannot overlap.
- [ ] Add production regression proving only a declared compatible attachment/connector handoff is relaxed; orientation/support/piece selection remain architecture-owned.

## Vegetation/ecology integration

- [x] Core semantics support vegetation yield against clearance/soft reservations.
- [ ] Make an actual production vegetation layout path consume the shared snapshot before accepting a placement.
- [ ] Suppress vegetation inside hard structure/road claims and yield in clearance/approach zones as appropriate.
- [ ] Preserve `RegionEcologyPolicy` as authority for species/types and baseline density.
- [ ] Add regression proving reservation consumption does not make world existence/placement device-tier dependent.

## True 3D underground integration

- [x] Add `HiddenSpaceVolume` adapter from actual `SiteHiddenSpaceRealization` + site origin into canonical 3D claims.
- [x] Add semantic regression: XZ-overlapping tunnel below a surface building is legal, while a real XYZ collision is rejected.
- [x] Add semantic regression: explicit connector handoff succeeds only for `Connector`; unrelated underground consumer cannot exploit it.
- [ ] Exercise multiple actual hidden-space/underground production claims through one bounded snapshot.
- [ ] Add production regression using real hidden-space realization data, not only synthetic boxes/corridors.
- [ ] Keep WFC/full dungeon/cave topology/content dressing out of scope.

## Inspection / gallery visualization

- [x] `ReservationQueryResult.Describe()` provides a deterministic textual placement inspector with decision/reason and conflicting claim metadata.
- [ ] Add `WorldbuildingGalleryShowcase` physical/debug visualization for surface hard footprints, clearance and route/access corridors using real production claims.
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
- [x] Kentridge production planner publishes shared building/plaza/access/road claims.
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

- [ ] Read current `SceneIssues/feature-readme.md` from current `origin/master` before gate execution (it was absent from the initial feature head).
- [ ] Re-fetch current `origin/master`, reconcile allowed prerequisite changes into `fixes/agent-7`, and ensure only this assignment is modified.
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
