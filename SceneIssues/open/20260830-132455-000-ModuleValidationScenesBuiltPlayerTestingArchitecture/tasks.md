# Tasks

## Completed architecture
- [x] Inventory targeted CI, standalone-player capture, special-case mappings, and validation docs.
- [x] Implement diff-driven module ownership with conservative shared/core expansion and fail-closed unresolved ownership.
- [x] Automatically execute every discovered module-owned EditMode/module-scoped PlayMode assembly and mandatory `KentridgePlayableSlice` for production diffs.
- [x] Fail required zero-match/skipped tests, missing scene/scenario pairs, missing captures, and failed Kentridge integration.
- [x] Refactor module validation, Kentridge, and SceneIssue replay onto one generic standalone-player build/capture path without test-name/scene-name policy.
- [x] Remove `*.module-validation.json` registration semantics and migrate module-local player targets to convention-discovered scene + `*.player-scenario.json` pairs.
- [x] Migrate Water to a thin module-local validation scene using production `VoxelEngine.Showcase.WaterRenderingShowcase`; scenario contains executable behavior only.
- [x] Prove discovery reuse with independent non-Water module/test fixtures.
- [x] Preserve focused regressions while removing PlayMode-only visual-acceptance semantics.
- [x] Update `AGENTS.md`, SceneIssue workflow, CI semantics, and validation documentation.
- [x] Remove repository-wide/top-level EditMode test assembly; migrate tests into lower-level owning module assemblies. Top-level PlayMode remains integration/smoke only and does not own production paths.
- [x] Add planner/runner regressions for automatic module discovery, new assembly discovery without registration, shared dependency expansion, top-level PlayMode exclusion, convention player pairing, missing/orphan pairs, manifest rejection, zero/skipped test failure, and generic evidence windows.

## Corrective validation history
- [x] Run `33469098939`: isolate/fix top-level PlayMode synthetic `Assets` ownership ambiguity and add regression before retry.
- [x] Run `33469680497`: isolate/fix migrated Rendering/Storage/WorldBuilder internal-test friend boundaries without widening production APIs.
- [x] Run `33472056643`: after a second compile failure, isolate minimal WorldBuilder friend-assembly root cause before another fix; migrate legacy `VoxelEngine.Tests.EditMode` friend to `Game.WorldBuilder.Tests.EditMode`.
- [x] Run `33474565849`: planning and all 20 Python regressions passed; compilation clean; first automatic suite passed; `Game.WorldBuilder.Tests.EditMode` ran 351 tests with 288 pass / 63 fail. Failure spread across 34 files established stale branch/master divergence rather than another access/compiler symptom.
- [x] Reconcile current master instead of rewriting stale tests/adjacent production. Feature was 216 commits behind at merge base `142b1134bd9d6a9eb1d60e55a296afaf6d9e7b3e`; two delete-vs-modify moved-test conflicts were resolved by carrying master versions into migrated locations and temporarily restoring legacy paths. Master merged normally at `0b941fc62c87841bca949df32cf9ee3d6a4ded67` through PR #201; no force/synthetic merge.
- [x] Preserve master’s five newly added top-level EditMode regressions while maintaining module ownership: Kentridge road composition, terrain grading envelope, world-road presentation -> WorldBuilder; road surface detail -> Rendering; continuous terrain corridor rasterisation -> new Structures EditMode assembly. Delete temporary/top-level copies. `Assets/Tests/EditMode` has no test directory/assembly after cleanup.
- [x] Confirm latest feature compare against master is `behind_by: 0` before exact-head validation.

## Final gates
- [ ] Run exact-current-head automatic module tests, Water built-player validation, and mandatory Kentridge built-player validation using only `ci-test/fixes/agent-8`.
- [ ] Inspect every retained Water post-readiness standalone-player frame; reject pre-readiness evidence and verify production-quality canonical Water reuse.
- [ ] Confirm final automatic validation runtime/cost remains practical; prior full automatic path measured 179.82s but final architecture needs exact-head measurement.
- [ ] Review all 18 acceptance criteria against exact-head results. Criteria 1-13 and 15-17 have source/regression support; criterion 14 and final proof for 5/6/7/13 require canonical Water -> Kentridge standalone execution; criterion 18 requires final runtime measurement.
- [ ] After green exact-SHA proof, update `issue.json` status/resolution fields, move the assignment directory directly `open` -> `closed`, then fetch/merge current master again if it advanced. Revalidate if that changes the feature materially.
- [ ] Push the exact final feature head to `origin/master` non-force; if master advances, fetch/merge/retry. Do not promote the CI transport request commit.
