# Tasks — WorldBuilder Spatial Reservation System

## Audit / ownership

- [x] Trace Kentridge town placement, plaza, frontage, public access, road, ecology, architecture and hidden-space ownership.
- [x] Confirm no competing canonical reservation service exists on reconciled master.
- [x] Keep route solving/width/grade in `WorldRoadNetwork`, form/orientation/support in architecture, species/density in ecology, topology in hidden-space planners, and rendering in presentation.
- [x] Confirm typed `StructuralSocket` is not required by the current production seam; do not import another assignment.
- [x] Reconcile current master into `fixes/agent-7` before implementation (`1dd53c5cfd809845b6a5bb5d26eadf17bd44e4cc`).
- [x] Reconcile `plan.md`/`tasks.md` to actual authoritative production paths before implementation resumes.

## Canonical reservation contract

- [x] Stable semantic `ReservationId`, owner/provenance/category/precedence and stable-id tie break.
- [x] Typed consumer/category masks; no string compatibility policy.
- [x] Hard occupancy, clearance, protected corridor, compatible handoff and soft-yield semantics.
- [x] Integer-decimetre half-open 3D box/corridor geometry with explicit vertical ranges.
- [x] Engine-free authority: no GameObjects, colliders, Physics queries, streaming order or thread scheduling.
- [x] Deterministic diagnostics and bounded query metrics.
- [x] Immutable bounded snapshots and deterministic integer XZ broad phase.
- [x] Planner-local idempotent replay, replacement/release and owner release.
- [x] Resolved local+global snapshot avoids double global capture during grouped planning.
- [ ] Add exact half-open touching/equality regression coverage.
- [ ] Add owner-excluded shared-snapshot query seam needed by production structure realization without per-role snapshot rebuilds.

## Kentridge town / roads

- [x] Town placement rejection uses shared reservations instead of bespoke overlap tests.
- [x] Publish building footprint/clearance, plaza and public approach claims.
- [x] Preserve candidate sequence, 256-attempt cap, district affinity and frontage/access semantics.
- [x] Canonical `WorldRoadNetwork` adapter exists and preserves road ownership of route geometry/width/clearance.
- [ ] In `TopDownWorldVoxelCatalogue`, solve `WorldRoadNetwork` once before reservation construction, use it for settlement + bounded tree snapshots, validate road handoffs, and reuse it for road rasterization.
- [ ] Add/adjust production regression proving TopDown uses the canonical road network and compatible settlement handoff.

## Production architecture integration

- [x] Identify real production path: `KentridgeCombinedVoxelCatalogueCanonical` -> `KentridgeSharedStructureVoxelCatalogue`; `KentridgeGrammarVoxelCatalogue` is comparison/gallery compatibility.
- [ ] Build one caller-owned canonical town snapshot in the combined catalogue and thread the already-resolved `SettlementPlan` + snapshot into shared-structure generation.
- [ ] Validate each production structure realization/site clearance against other owners while allowing its matching host plot owner.
- [ ] Do not move form/orientation/support/piece selection into reservation policy.
- [ ] Add production regression proving an incompatible external structure clearance is rejected and host-owner overlap is allowed.

## Vegetation / ecology

- [x] `KentridgeVegetationPlanner` groups trees+boulders and filters them against one caller-supplied resolved snapshot.
- [x] Vegetation yields/suppresses against structure/road/access/clearance claims while ecology retains species/density authority.
- [x] Grouped planning is device-independent; reservation acceptance does not depend on device tier.
- [x] `KentridgeDecorativeVegetationPlanner` is non-authoritative surface dressing (ground plants/moss/vines), not independent tree/boulder/world-occupancy placement; do not add a parallel reservation authority.
- [ ] Ensure focused vegetation regressions pass on the final exact SHA.

## True-3D hidden-space integration

- [x] Real `SiteHiddenSpaceRealization` -> `HiddenSpaceVolume` 3D claim adapter.
- [x] `KentridgeHiddenSpaceBatchPlanner` consumes one caller snapshot plus provisional accepted 3D claims.
- [x] Production regression covers vertically separated XZ overlap accepted and true XYZ collision rejected.
- [x] Connector handoff is explicit and cannot be exploited by unrelated underground consumers.
- [x] Keep cave/WFC/dungeon topology out of reservation ownership.

## Determinism / lifecycle / cost regressions

- [x] Stable ids and insertion-order-independent precedence winner.
- [x] Equal-precedence stable-id tie break.
- [x] Hard/clearance/soft/compatible-handoff outcomes.
- [x] Planner-local replay preserves prior committed claim on rejected replacement; deterministic release/release-owner coverage.
- [x] Bounded-window stress excludes distant claims and bounds bucket/candidate/narrow-phase work.
- [ ] Add exact owner-exclusion regression for shared production snapshots.
- [ ] Record representative snapshot/query work metrics and source-construction cost after final integration.
- [ ] Check managed/native allocation or repository-supported memory evidence where available.
- [ ] Verify no generation candidate/device budgets changed and no unrelated world-generation behavior moved.

## Gallery / runtime evidence

- [ ] Trace `WorldbuildingGalleryReservationInspection` from the actual `WorldbuildingGalleryAuditHarness`/showcase runtime path and confirm it is presentation-only.
- [ ] Ensure surface hard/clearance/access and underground 3D claims plus a deliberate rejected candidate are visibly/readably inspectable.
- [ ] Verify physical scene content corresponds to the visualized claims; no visual acceptance from source inspection alone.
- [ ] Verify named benchmark scene paths in issue evidence and update stale paths if necessary.
- [ ] Add/update `Assets/Scripts/EditorTools/MCPEditorTests/SceneTestHighlightPolicy.csv` entries required by current `AGENTS.md`, if the affected scenes require them.
- [ ] Run repository scene-test classifier/highlight-policy gate and record durable output.
- [ ] Run exact `WorldbuildingGalleryShowcase` built-player/runtime audit and visually inspect required captures.
- [ ] Run real `KentridgePlayableSlice` built-player/runtime check including representative buildings/plaza/public approaches and CharacterMotor traversal.

## Validation / blast radius

- [ ] Run focused `SpatialReservationTests` and affected Kentridge EditMode regressions.
- [ ] Run repository compile/static/ProjectValidator gates required for changed paths.
- [ ] Run affected scene/runtime/built-player validation required by `AGENTS.md` and capture durable evidence.
- [ ] Review final master diff for assignment-only scope and blast radius.
- [ ] Record exact commands/runs, cost evidence, acceptance mapping and fix summary in SceneIssue metadata.

## Workflow / closure

- [x] `SceneIssues/feature-readme.md` is absent; use repository-declared `SceneIssues/README.md` plus assignment README.
- [x] Do not edit `.github/test-request.json` on `fixes/agent-7`; do not create extra CI transports or replace queued CI.
- [ ] Complete all implementation/runtime/validation boxes above while assignment remains open.
- [ ] Move assignment open -> pending with required testing metadata only after open-phase acceptance is complete.
- [ ] Fetch/merge current `origin/master` into `fixes/agent-7` and rerun affected focused gates.
- [ ] Use `ci-test/fixes/agent-7` only for the final targeted-CI request against the exact feature SHA; verify no request is already queued before writing it.
- [ ] Obtain green exact-SHA CI and record request/run/tested SHA evidence.
- [ ] Complete pending metadata after the green exact-SHA workflow gate.
- [ ] Move pending -> closed, set `status=fixed` and `resolvedUtc` only when every acceptance criterion is complete.
- [ ] Merge current `origin/master` again if required; if tree changes, revalidate per workflow before promotion.
- [ ] Push the exact validated `fixes/agent-7` head to `origin/master` non-force; if master advances, fetch/merge/revalidate/retry.