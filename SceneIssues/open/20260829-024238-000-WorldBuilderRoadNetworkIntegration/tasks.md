# Tasks

## Investigation
- [x] Trace production owners for macro connectivity, Kentridge routes/streets, voxel terrain/surface generation, ecology, and streaming/LOD.
- [x] Inspect historical strip-road behavior at `336cb6e63e19bc6039f3f89bb4d2056e2d0efb60` and slope-material safety at `8cd28a5ea7133a4012a17112375f70384bee79ec`.
- [x] Record the representation-gap discriminator and selected shared-road design in `plan.md`.
- [x] Diagnose the pre-merge targeted CI compile failure and repair the missing `MountingForce.WorldGen` test import without changing product behavior.
- [x] Audit terrain/crossing flag authority: current production `TerrainQuery`, `PlannedRoute`, and `TopDownWorldRouteSpec` expose no water/reserved/barrier field, so adapters must not fabricate flags; generic resolver policy remains ready for a future authoritative owner.
- [x] Classify final targeted CI `33281599556`: product/test-contract failures, not runner noise; 7 tests executed, 2 failed.
- [x] Resolve the non-monotonic shoulder sample (`19 -> 26`): adjacent samples were independently hashing edge width; semantic and physical paths now sample coherent 64dm world-space variation at the nearest centerline point.
- [x] Resolve authored vegetation point `(900,455)` collision: master already had fixed preferred anchors against an organic seeded building layout; authored non-residential anchors now use deterministic bounded clearance search while preserving species/zone intent.
- [x] Identify the sanctioned built-player gate: same `tests-single.yml` transport, `PlayMode` + focused test + `scene_issue` + `replay_seconds`; no extra CI branch/workflow required.

## Implementation
- [x] Make road/trail intent first-class on stable semantic endpoints with reusable profile data, provenance, and deterministic seed.
- [x] Deterministically resolve logical connections to terrain-aware physical routes with grade/cut-fill constraints and invalid-barrier/crossing policy.
- [x] Provide one compact generic road network/influence with grading, shoulder, ecology, tangent, junction, and keep-clearance queries.
- [x] Produce destructible graded voxel roads through normal WorldBuilder generation; no scene-local voxel edits or cover meshes.
- [x] Replace overlapping square/strip road stamps with bounded one-primitive `EmitTerrainCorridor` pieces evaluated per voxel column.
- [x] Preserve local terrain material through continuous road shoulder coverage and persist the same 0..31 RoadInfluence detail for presentation/LOD.
- [x] Ensure semantic and physical surface paths consume the same 0..31 influence scalar.
- [x] Keep semantic/resolved roads queryable for navigation/map/travel/NPC/encounter consumers through `WorldRoadNetwork` route IDs, endpoints, junctions, local frames, and influence queries.
- [x] Migrate modern Kentridge `SettlementPlan.Routes` to the generic network/primitive while retaining legacy `Streets` compatibility.
- [x] Preserve arbitrary/diagonal `PlannedRoute` legs without Kentridge-only axis assumptions.
- [x] Make Kentridge vegetation consume the shared scalar suppression/recovery instead of hard street/clearance exclusion.
- [x] Keep regional ecology authoritative; road influence only thins existing candidates locally.
- [x] Author Kentridge keep-clearance on generic routes and expose it through the generic aggregate without Kentridge types in the reusable API.
- [x] Replace top-down hard-route Manhattan surface realization with `TopDownWorldRoadNetwork` + the same shared terrain corridor lowering.
- [x] Preserve bounded generation: one primitive per bounded piece, FeatureBudget-capped definitions/footprints, no per-segment GameObjects, no per-frame road-generation work.
- [x] Make optional road-edge irregularity spatially coherent without changing profile width/transition budgets or semantic↔physical sampling parity.
- [x] Treat authored Kentridge non-residential tree coordinates as preferred anchors and relocate only blocked anchors through a deterministic <=120dm search; clear anchors remain unchanged.

## Regressions implemented
- [x] Modern Kentridge semantic routes map to traceable generic physical road definitions.
- [x] Fixed intent/seed/terrain yields deterministic resolved geometry.
- [x] Non-flat deterministic routing respects maximum grade and cut/fill envelopes.
- [x] Water/barrier fixture rejects a route without authored crossing policy and resolves when policy allows it.
- [x] Physical corridor distance/height/coverage matches semantic influence on an intermediate shoulder sample.
- [x] Shoulder coverage recovers continuously/monotonically without the legacy ten-band dependency; exact-source CI run `33284733815` passed.
- [x] Physical catalogue asserts one `EmitTerrainCorridor`, zero legacy road `EmitBox` stamps, and bounded footprints/definitions.
- [x] Vegetation suppresses in the core and progressively recovers through shared shoulder influence; production planner exact-source CI passed in run `33284733815`.
- [x] Kentridge authors positive generic placement keep-clearance beyond the grading radius and it is queryable from `WorldRoadNetwork`.
- [x] Existing Kentridge named-landmark, diagonal-route, and connectivity coverage remains in `KentridgeOrganicLayoutTests`.
- [x] Validate repaired/expanded `KentridgeRoadShoulderRegressionTests` on exact source `b5cac79f1ff4f289d643edeef3019e4c1d75a806`: run `33284733815`, 7/7 passed, Unity peak RSS 5119 MB.
- [ ] Validate segment/chunk/LOD road geometry/material continuity in the built player.

## Validation / cost
- [x] Refresh/merge current `origin/master` immediately before the attempted final request (`2e3574af`, master parent `2b100aa4`).
- [x] Refresh/merge current `origin/master` again after the CI-discovered repairs (`ed0d8711`, current-master parent `47e51f98`).
- [x] Obtain green focused exact-source EditMode regression through `ci-test/fixes/agent-1` only: run `33284733815`, 7/7 passed.
- [x] Static cost audit: one analytic primitive + one explicit placement per bounded piece; definition/footprint budgets are enforced; `Primitive` adds no fields/stride; no road GameObjects or per-frame generation path; coherent edge variation is integer-only sample work.
- [ ] Issue/obtain the combined exact-source PlayMode + built-player request through the same `ci-test/fixes/agent-1` transport using `VoxelEngine.Tests.PlayMode.KentridgePlayableScenePlayTests`, this issue's `scene_issue`, and a 60-second replay.
- [ ] Run/inspect built application/player evidence for `Assets/Scenes/KentridgePlayableSlice.unity`; verify no startup/runtime exceptions.
- [ ] Capture/inspect endpoint-to-endpoint road continuity and player-height traversal with collision/streaming active.
- [ ] Inspect both shoulders on uneven/sloped terrain for natural Grass↔Dirt recovery with no repeated bands, staircase, exposed wall, or hard line.
- [ ] Inspect medium/far views for chunk/LOD seams and floating props.
- [ ] Verify vegetation suppression/recovery and semantic route/influence traceability evidence in the built application.
- [ ] Quantify bounded cost from generated route/definition/primitive counts plus built-player residency/runtime evidence; confirm no storage/vertex-stride or per-frame cost increase.
- [x] Review feature-only diff against `47e51f98`: all changed files are WorldBuilder/voxel road support, regressions, or this assignment metadata; no other assignment, workflow, or feature-branch `.github/test-request.json` change.

## Promotion / closure
- [ ] Complete `issue.json` pending metadata (`status`, `resolutionSummary`, `regressionTest`, `fixCommit`) only after promotion gates pass.
- [ ] Move only this assignment `open` → `pending` in a separate bookkeeping commit.
- [ ] After green exact-SHA targeted CI and built-app validation, set `status=fixed` and `resolvedUtc`, then move only this assignment `pending` → `closed`.
- [ ] Merge current `origin/master` into `fixes/agent-1`, resolve only in-scope conflicts, push feature head, then push that exact head to `origin/master` non-force; if master advances, fetch/merge/retry.
