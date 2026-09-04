# Tasks — WorldBuilder Secret Discovery Clue Generation

## Evidence and scope

- [x] Read `AGENTS.md`, `SceneIssues/issue-readme.md`, and `SceneIssues/README.md`.
- [x] Inspect captures/marked regions; none are present.
- [x] Inspect design/source systems and discriminate competing hypotheses with repository evidence.
- [x] Remove primitive/parallel visual proof approaches that failed production-quality review.
- [x] Resolve prior Gallery-integration instruction conflict by following the current assignment directive and immutable repository acceptance.
- [x] Fetch/merge master `13b3c6a752deb030effba0f6e430863d0c1fd115`; feature merge head `43c2afc083bdf2d25a101bcc609f361fe18819d0` is 0 behind master.
- [x] Reassess the assignment against the merged module-local validation requirement: `WorldBuilder` already has focused validation; `Composition/CaveWorldBuilder` and `Composition/Showcase` require their own focused scenes.

## Planning / behavior

- [x] Stable secret/route/clue IDs and immutable plan metadata.
- [x] Deterministic plan for same seed/inputs independent of candidate enumeration order.
- [x] Standard/Major clue count/channel rules behaviorally tested.
- [x] Pre-solve observability and circular dependency rejection behaviorally tested.
- [x] Reusable semantic anchors avoid prefab names/capture coordinates.
- [x] Interactable-backed and natural traversal routes both supported without duplicate interaction authority.
- [x] Multiple legal routes resolve to one canonical discovery identity; revisit/reload/repeated activation is idempotent.
- [x] `ProtectedShell`, `AuthoredBreakablesOnly`, and `SystemicBypassAllowed` represented and behaviorally tested, including accidental bypass/leakage prevention.

## Production-path regression / visual validation

- [x] Generated cave secret composition and verified topology exercised through production authoring.
- [x] Deterministic branching fracture clue restricted to cave-facing false-wall layer; complete barrier remains solid before destruction.
- [x] `BoundaryEvidenceIsDeterministicFractureOnCaveFaceAndPreservesVerifiedSeal` passed exact run `33537413920`.
- [x] Dedicated module-local `Assets/Game/WorldBuilder/Validation/SecretDiscovery` scene uses production voxel world generation, meshing/rendering, materials/coatings, vegetation, and destruction.
- [x] Two repeated requested-filter zero-match failures isolated to persistent optional-request infrastructure (`experiment-014`); no third retry.
- [x] Workflow-green blank-surface output was visually rejected; CPU-renderer merge restored cave rendering.
- [x] Subsequent teardown failure isolated to renderer lifecycle cleanup (`experiment-015`) after WorldBuilder evidence completed.
- [x] Exact run `33801222778` passed WorldBuilder EditMode, dedicated SecretDiscovery built player, Kentridge integration, and SceneIssue replay with clean teardown.
- [x] Full-resolution 9/12/15s frames show sparse non-glowing fracture evidence; 18/21s frames show breached route/open hidden interior; logs show 35 clue voxels and 607 destroyed voxels.
- [x] Restore thin production `WorldbuildingGalleryShowcase` SecretDiscovery composition plus compatibility/physical/surface-route regressions.
- [x] Isolate exact run `33821322632` failure to restored Gallery regressions being outside the current module-owned test asmdef; standalone replay failed from the same compile break (`experiment-016`).
- [x] Move restored Gallery regressions under `Assets/Game/WorldBuilder/Tests/EditMode` so they compile in the existing `VoxelEngine.Tests.EditMode` assembly.
- [x] Exact targeted run `33822800307` succeeded against feature SHA `5ce4b97bc4bd3b69556b2c41ce8c995319f4278d` after the test-assembly ownership fix.
- [ ] Add focused `Assets/Game/Composition/CaveWorldBuilder/Validation` production-path scene for cave secret pocket + clue + authored breakable breach.
- [ ] Add focused `Assets/Game/Composition/Showcase/Validation` production-path scene for Worldbuilding Gallery SecretDiscovery natural and breakable clue framing.
- [ ] Representative SecretDiscovery examples are visibly understandable in `WorldbuildingGalleryShowcase` at gameplay scale.
- [ ] Gallery visual review proves feature-specific geometry/material/environmental clue language rather than placeholder signs/universal glow.

## Cost / integration / closure

- [x] Planner/discovery work is one-shot/event-driven; no per-frame search/polling added.
- [x] Cave composition is bounded by traversal candidates; clue fracture authors 35 coating voxels once.
- [x] Blast-radius/cost rechecked against green run `33801222778`; no recurring runtime/search cost regression.
- [x] Updated repository workflow reviewed: final SceneIssue promotion is PR-based; do not push a SceneIssue feature head directly to protected `master`.
- [ ] Fresh exact-SHA targeted gate green after required module-local validation scenes.
- [ ] All acceptance criteria green.
- [ ] Move assigned SceneIssue `open -> closed`, set `status=fixed` and `resolvedUtc`, and complete supported resolution fields.
- [ ] Fetch/merge current master after closure bookkeeping.
- [ ] Open/update `fixes/agent-5` -> `master` PR, enable auto-merge, and require the `affected` PR gate plus canonical standalone Kentridge full-app test to pass.
- [ ] Confirm closed SceneIssue is visible on `origin/master` after PR merge.
