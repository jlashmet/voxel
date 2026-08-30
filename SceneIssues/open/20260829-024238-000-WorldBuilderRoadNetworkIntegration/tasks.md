# Tasks

## Investigation
- [x] Trace production owners for macro connectivity, Kentridge routes/streets, voxel terrain/surface generation, ecology, and streaming/LOD.
- [x] Inspect historical strip-road behavior at `336cb6e63e19bc6039f3f89bb4d2056e2d0efb60` and slope-material safety at `8cd28a5ea7133a4012a17112375f70384bee79ec`.
- [x] Record the representation-gap discriminator and selected shared-road design in `plan.md`.
- [x] Diagnose the pre-merge targeted CI compile failure and repair the missing `MountingForce.WorldGen` test import without changing product behavior.
- [x] Audit terrain/crossing flag authority: current production `TerrainQuery`, `PlannedRoute`, and `TopDownWorldRouteSpec` expose no water/reserved/barrier field, so adapters must not fabricate flags; generic resolver policy remains ready for a future authoritative owner.
- [x] Classify targeted CI `33281599556`: product/test-contract failures, not runner noise; 7 tests executed, 2 failed.
- [x] Resolve the non-monotonic shoulder sample (`19 -> 26`): adjacent samples were independently hashing edge width; semantic and physical paths now sample coherent 64dm world-space variation at the nearest centerline point.
- [x] Resolve authored vegetation point `(900,455)` collision: master already had fixed preferred anchors against an organic seeded building layout; authored non-residential anchors now use deterministic bounded clearance search while preserving species/zone intent.
- [x] Identify the sanctioned built-player gate: same `tests-single.yml` transport, `PlayMode` + focused test + `scene_issue` + `replay_seconds`; no extra CI branch/workflow required.
- [x] Classify combined full-app run `33285741354`: built player compiled, launched for 60s, captured 4 screenshots, and exited 0, but the PlayMode acceptance exposed an in-scope `FixedString64Bytes` truncation for long macro-road definition name `world-road-macro:overworld-moordell->overworld-to-rossdam-s0p0` in `WorldRoadNetworkVoxelCatalogue.Build`.
- [x] Inspect green combined run `33286511375` and its real-player artifact instead of treating workflow success as closure evidence: PlayMode regression passed 1/1 and the player ran 60s with zero harness assertions/exceptions, but the captured Dirt→Grass road edge shows a regular voxel/checker staircase and the four frames do not prove endpoint-to-endpoint continuity or both shoulders on uneven/sloped terrain. This is an acceptance failure, not a bookkeeping blocker.
- [x] Classify final blend request `33294139897`: product compile failure before tests/player build, `VoxelCell.cs(151,37)` ambiguous `Math.Clamp(byte, byte, byte)` vs `Math.Clamp(int, int, int)` in the new packed blend helper; runner admission/resources were healthy.
- [x] Inspect repaired combined run `33296050037`: PlayMode blend regressions passed 2/2 and the real Kentridge player ran 60s with zero harness assertions/exceptions, but screenshots are dominated by loading/opening/interior/building-wall views. They do not prove endpoint continuity, both shoulders on uneven/sloped terrain, medium/far seam behavior, or vegetation recovery. Treat as validation-harness acceptance failure, not closure evidence.

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
- [x] Bound generated road definition names to `FixedString64Bytes.UTF8MaxLengthInBytes` while preserving the segment/piece suffix and deterministic route traceability.
- [x] Remove the regular checker/stair-step Dirt→Grass authoring path while preserving the shared 0..31 road influence, continuous shoulder recovery, slope/exposed-top correctness, destructibility, and bounded streaming representation; fractional shoulders now retain local terrain as authoritative material and carry road material as continuous presentation metadata.
- [x] Encode the generic two-material surface blend inside the existing packed surface representation without increasing persisted voxel size or `SmoothSurfaceVertex` stride; the shared `SmoothSurface` path interpolates authored scalar coverage and blends full primary/secondary material response.
- [x] Ensure two-material presentation metadata is presentation-only and contributes zero coating density/displacement on both CPU and GPU density paths.
- [x] Preserve ordinary non-blend coating/style/detail behavior, including existing coating displacement semantics.
- [x] Mask the blend marker back to the underlying reconstruction style before shared CPU/GPU style lookup and faceted classification so blend metadata cannot route geometry through a different reconstruction path.
- [x] Repair the CI-discovered `MaterialBlend` coverage clamp overload ambiguity without changing packed semantics or runtime behavior (`3c586c51b472f6c34461cfe939e8eca1051801a5`).
- [x] Improve the generic capture-less `KentridgePlayableSlice` evidence profile so a 60-second SceneIssue replay exits the opening sooner, exercises player-height road traversal, and captures an elevated survey view without adding a new capture or CI transport.

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
- [x] Add a production-catalogue regression using the observed long macro route ID; it lowers through `WorldRoadNetworkVoxelCatalogue.Build`, asserts fixed-string capacity, and preserves the `-sNpM` suffix. It lives in PlayMode so the final combined request can prove this repair and then launch the real player.
- [x] Add PlayMode production-boundary coverage that samples an actual fractional `TerrainCorridorRasteriser` shoulder and round-trips that exact 0..31 value through the packed two-material surface contract without storage or vertex-stride growth.
- [x] Add PlayMode shared-surface coverage that invokes the production CPU density coating-displacement path: a blend whose secondary material byte equals Snow produces zero displacement while ordinary Snow retains positive displacement; shared style lookup also resolves the marker back to Smooth.
- [ ] Validate segment/chunk/LOD road geometry/material continuity in the built player.

## Validation / cost
- [x] Refresh/merge current `origin/master` immediately before the attempted final request (`2e3574af`, master parent `2b100aa4`).
- [x] Refresh/merge current `origin/master` after the CI-discovered shoulder/vegetation repairs (`ed0d8711`, master parent `47e51f98`).
- [x] Refresh/merge current `origin/master` after the full-player name repair: feature merge `cd835ce4`, master `d4b31a70`.
- [x] Obtain green focused exact-source EditMode regression through `ci-test/fixes/agent-1` only: run `33284733815`, 7/7 passed.
- [x] Static cost audit: one analytic primitive + one explicit placement per bounded piece; definition/footprint budgets are enforced; `Primitive` adds no fields/stride; no road GameObjects or per-frame generation path; coherent edge variation is integer-only sample work; name repair adds no geometry/residency cost. The presentation repair reuses persisted style/coating/detail bits and the existing 32-byte `SmoothSurfaceVertex`; marked fragments add one bounded second material evaluation only.
- [ ] Obtain a green combined exact-source PlayMode + built-player request through the same `ci-test/fixes/agent-1` transport with visually sufficient road evidence. Run `33286511375` was green but rejected for the checker/stair-step shoulder. Run `33294139897` failed compilation before either gate. Run `33296050037` is green (2/2 PlayMode; real-player capture/upload/status all green) but its camera evidence is insufficient for the issue's required road views.
- [ ] Run/inspect final repaired built application/player evidence for `Assets/Scenes/KentridgePlayableSlice.unity`; verify no startup/runtime exceptions.
- [ ] Capture/inspect endpoint-to-endpoint road continuity and player-height traversal with collision/streaming active.
- [ ] Inspect both shoulders on uneven/sloped terrain for natural Grass↔Dirt recovery with no repeated bands, staircase, exposed wall, or hard line.
- [ ] Inspect medium/far views for chunk/LOD seams and floating props.
- [ ] Verify vegetation suppression/recovery and semantic route/influence traceability evidence in the built application.
- [ ] Quantify bounded cost from generated route/definition/primitive counts plus built-player residency/runtime evidence; confirm no storage/vertex-stride or per-frame cost increase.
- [x] Review feature product/test scope: changed implementation/test files are WorldBuilder/voxel road support or road regressions; assignment metadata is in-scope. Preserve the coordinator's `SceneIssues/README.md` CI clarification and merged queue-state changes from master rather than rewriting them.

## Promotion / closure
- [ ] Complete `issue.json` pending metadata (`status`, `resolutionSummary`, `regressionTest`, `fixCommit`) only after promotion gates pass.
- [ ] Move only this assignment `open` → `pending` in a separate bookkeeping commit.
- [ ] After green exact-SHA targeted CI and built-app validation, set `status=fixed` and `resolvedUtc`, then move only this assignment `pending` → `closed`.
- [ ] Merge current `origin/master` into `fixes/agent-1`, resolve only in-scope conflicts, push feature head, then push that exact head to `origin/master` non-force; if master advances, fetch/merge/retry.
