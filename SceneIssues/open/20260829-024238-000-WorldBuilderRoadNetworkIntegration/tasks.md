# Tasks

## Investigation
- [x] Trace current production owners for macro/top-down connectivity, WorldBuilder composition, Kentridge roads/streets, voxel density/surface generation, terrain material/coating, vegetation/ecology, and streaming/LOD.
- [x] Inspect road behavior/history at `336cb6e63e19bc6039f3f89bb4d2056e2d0efb60` and slope-material safety at `8cd28a5ea7133a4012a17112375f70384bee79ec`.
- [x] Record the discriminator result in `plan.md` and add discovered required work here.

## Implementation
- [ ] Make road/trail intent first-class on stable semantic world endpoints with reusable profile data and provenance/seed behavior.
- [ ] Deterministically resolve logical connections to terrain-aware physical routes with grade/cut-fill constraints and explicit invalid-barrier handling.
- [ ] Provide one compact, chunk-safe road influence consumed by terrain deformation, surface/shoulder presentation, and vegetation falloff.
- [ ] Produce genuinely graded/walkable voxel roads through normal WorldBuilder generation without scene-local voxel edits or non-destructible cover meshes.
- [ ] Implement/reuse continuous primary+secondary terrain coverage for natural Dirt↔local-terrain shoulders while preserving exposed-top/slope material correctness.
- [ ] Keep semantic/resolved roads available to navigation/map/travel/NPC/encounter consumers.
- [ ] Migrate equivalent Kentridge road generation to the shared primitive; do not duplicate the macro-world physical-realization ticket’s road graph/solver.
- [ ] Migrate modern Kentridge `SettlementPlan.Routes` (the production planner emits zero legacy `Streets`) while retaining a compatibility adapter for authored legacy streets.
- [ ] Convert diagonal `PlannedRoute` legs into deterministic shared voxel-road geometry without restoring Kentridge-only axis assumptions.
- [ ] Replace vegetation's legacy `BlockedByStreet`-only exclusion with the shared road influence so both `Routes` and compatibility `Streets` suppress/recover vegetation from the exact physical corridor.
- [ ] Keep regional ecology policy authoritative; apply road influence only as a local suppression/recovery modifier using existing route exclusion ownership.
- [ ] Replace `TopDownWorldVoxelCatalogue`'s Manhattan `PaintSurface` tile realization with the shared terrain-aware resolved route/influence so macro semantic connections and Kentridge circulation use one physical road spine.
- [ ] Preserve deterministic chunk/LOD/streaming behavior and avoid per-segment GameObject/primitive explosion.

## Regressions
- [ ] Top-level semantic connection yields one traceable semantic road and deterministic resolved route.
- [ ] Fixed input/seed yields stable route geometry.
- [ ] Non-flat routing respects maximum grade and cut/fill limits.
- [ ] Impossible blocked routes reroute/reject or require explicit crossing/pass semantics.
- [ ] Terrain and surface presentation consume the same influence.
- [ ] Shoulder coverage is continuous/monotonic and does not require discrete band stacks.
- [ ] Replace the legacy `KentridgeRoadShoulderRegressionTests` assertion that requires ten Moss shoulder bands with a regression proving shared continuous influence/coverage and no repeated-band dependency.
- [ ] Vegetation is suppressed in the core and recovers through the shoulder.
- [ ] Segment/chunk/LOD boundaries preserve road geometry/material continuity.
- [ ] Semantic/resolved road remains queryable by travel/navigation/map consumers.
- [ ] Existing Kentridge connectivity remains valid after migration.

## Validation / cost
- [ ] Run focused exact-SHA targeted CI through `ci-test/fixes/agent-1` only.
- [ ] Run exact-SHA built-application scene harness for `KentridgePlayableSlice`; verify no startup/runtime exceptions.
- [ ] Capture durable elevated endpoint-to-endpoint road evidence.
- [ ] Capture player-height traversal with collision/streaming active.
- [ ] Capture both shoulders on uneven/sloped terrain showing natural Grass↔Dirt transition with no repeated bands/staircase/hard line.
- [ ] Capture medium/far views proving no chunk/LOD seams.
- [ ] Capture vegetation suppression/recovery and route/influence traceability evidence.
- [ ] Measure route/world-build time, voxel/brick work, primitive/GameObject count, resident memory, CPU/GPU cost, and streaming/LOD impact against existing budgets.
- [ ] Review final feature-only diff for unrelated capture/workflow/CI-request changes.

## Promotion / closure
- [ ] Complete `issue.json` pending metadata (`status`, `resolutionSummary`, `regressionTest`, `fixCommit`) after all promotion gates pass.
- [ ] Move only this assignment `open` → `pending` in a separate bookkeeping commit.
- [ ] After green exact-SHA targeted CI and built-app validation, set `status=fixed` and `resolvedUtc`, then move only this assignment `pending` → `closed`.
- [ ] Merge current `origin/master` into `fixes/agent-1`, resolve only in-scope conflicts, push feature head, then fast-forward/non-force push that exact head to `origin/master`; if master advances, fetch/merge/retry.
