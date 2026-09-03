# Tasks — WorldBuilder Secret Discovery Clue Generation

## Evidence and scope

- [x] Read `AGENTS.md`, `SceneIssues/issue-readme.md`, and `SceneIssues/README.md`.
- [x] Inspect captures/marked regions; none are present.
- [x] Inspect design/source systems and discriminate competing hypotheses with repository evidence.
- [x] Remove primitive/parallel visual proof approaches that failed production-quality review.
- [ ] **BLOCKER:** `issue.json` requires representative generated SecretDiscovery examples in `WorldbuildingGalleryShowcase`, while prior explicit user direction prohibited this feature-specific Gallery integration. Do not weaken acceptance; keep open until explicitly resolved.

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
- [x] Dedicated module-local `SecretDiscovery` validation scene uses production voxel world generation, meshing/rendering, materials/coatings, vegetation, and destruction.
- [x] Two repeated requested-filter zero-match failures isolated to persistent optional-request infrastructure (`experiment-014`); no third retry.
- [x] Workflow-green blank-surface output was visually rejected; CPU-renderer merge restored cave rendering.
- [x] Subsequent teardown failure isolated to renderer lifecycle cleanup (`experiment-015`) after WorldBuilder evidence completed.
- [x] Exact run `33801222778` passed WorldBuilder EditMode, dedicated SecretDiscovery built player, Kentridge integration, and SceneIssue replay with clean teardown.
- [x] Full-resolution 9/12/15s frames show sparse non-glowing fracture evidence; 18/21s frames show breached route/open hidden interior; logs show 35 clue voxels and 607 destroyed voxels.
- [x] Exact built `WorldbuildingGalleryShowcase` replay reaches a usable rendered state without runtime exceptions.
- [ ] Representative SecretDiscovery examples are visibly understandable in `WorldbuildingGalleryShowcase` at gameplay scale — blocked by instruction conflict.
- [ ] Gallery visual review proves feature-specific geometry/material/environmental clue language rather than placeholder signs/universal glow — blocked by instruction conflict.

## Cost / integration / closure

- [x] Planner/discovery work is one-shot/event-driven; no per-frame search/polling added.
- [x] Cave composition is bounded by traversal candidates; clue fracture authors 35 coating voxels once.
- [x] Blast-radius/cost rechecked against green run `33801222778`; no recurring runtime/search cost regression.
- [x] Current master `431b7a5b501a8e1160d4b8ec90aeaa1752f72881` is merged into the feature through two-parent merge `51fd1d3dbd440823a0f11448804126e2e6e6e3cf`.
- [x] Updated repository workflow reviewed: final SceneIssue promotion is PR-based; do not push a SceneIssue feature head directly to protected `master`.
- [ ] All acceptance criteria green and fresh exact-SHA targeted gate green — pending Gallery conflict resolution and subsequent exact-SHA revalidation.
- [ ] Move assigned SceneIssue `open -> closed`, set `status=fixed` and `resolvedUtc`, and complete supported resolution fields.
- [ ] Fetch/merge current master after closure bookkeeping.
- [ ] Open/update `fixes/agent-5` -> `master` PR, enable auto-merge, and require the `affected` PR gate plus canonical standalone Kentridge full-app test to pass.
- [ ] Confirm closed SceneIssue is visible on `origin/master` after PR merge.
