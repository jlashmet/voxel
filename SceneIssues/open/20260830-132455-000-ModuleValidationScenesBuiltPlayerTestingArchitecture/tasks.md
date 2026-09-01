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
- [x] Add planner/runner regressions for automatic module discovery, new assembly discovery without registration, contract dependency expansion, top-level PlayMode exclusion, convention player pairing, missing/orphan pairs, manifest rejection, zero/skipped test failure, generic evidence windows, metadata-only no-op changes, and direct module `Tests/*.asmdef` EditMode discovery.

## Corrective validation history
- [x] Run `33469098939`: isolate/fix top-level PlayMode synthetic `Assets` ownership ambiguity and add regression before retry.
- [x] Run `33469680497`: isolate/fix migrated Rendering/Storage/WorldBuilder internal-test friend boundaries without widening production APIs.
- [x] Run `33472056643`: after a second compile failure, isolate minimal WorldBuilder friend-assembly root cause before another fix.
- [x] Run `33474565849`: planning and all 20 Python regressions passed; compilation clean; first automatic suite passed; WorldBuilder ran 351 tests with 288 pass / 63 fail. Failure spread established stale branch/master divergence rather than another access/compiler symptom.
- [x] Reconcile current master instead of rewriting stale tests/adjacent production. Master merged normally at `0b941fc62c87841bca949df32cf9ee3d6a4ded67`; no force/synthetic merge. Preserve new upstream regressions in module-owned suites.
- [x] Run `33476275534` on reconciled feature `41279d83c8dbea6ed8aa0a7b422cbdcac1e07cdf`: planning, 20 Python regressions, compilation, and first module suite passed; WorldBuilder ran 367 tests with 304 pass / 63 fail, reproducing the same failure count after a materially different fix.
- [x] Before another behavioral fix, isolate the repeated-symptom root cause: the moved WorldBuilder suite still contained broad former repository-wide tests; relocated tests confused old production scanners; several guards referenced old package/deleted Showcase targets. Correct migration-only scanner/path/deleted-target defects without modifying unrelated production behavior.
- [x] Run correctly parented exact request `33479440611` on `fa86f8dbd6db0966e66f47673c6fe90bbe6f7e1a`: planning and all 20 Python regressions passed; automatic execution still selected WorldBuilder, where 359 tests produced 316 pass / 43 fail. Do not patch those unrelated Kentridge/architecture failures.
- [x] Isolate the remaining planning defect from `33479440611`: `plan()` selected modules for changed `/Tests/` paths despite those paths being non-production; deleted obsolete manifests were treated as production fallback; validation-only Water support lived in unowned production/shared roots.
- [x] Add regressions and fix ownership semantics: test-only paths do not claim module production validation; deleted `*.module-validation.json` paths are non-production; validation-only asmdefs do not enter runtime dependency ownership; unowned top-level `Assets/Game/Composition/**` changes receive Kentridge integration without broad lower-level fallback; unknown non-composition production still uses broad safe fallback.
- [x] Remove the WorldBuilder production friend delta by retaining existing `VoxelEngine.Tests.EditMode` assembly identity at the module-local WorldBuilder test path; reuse the established friend rather than widening production.
- [x] Move Water liquid-publication validation support under `Assets/VoxelEngine/Rendering/Validation/Water`: preserve the probe Unity GUID, add a module-local validation asmdef, observe existing public read-only `VoxelRenderBridge.SurfaceMetrics` directly, remove redundant shared `RenderingSurfaceDiagnostics`, and remove the obsolete Rendering friend for the retired `Game.WorldBuilder.Tests.EditMode` identity.
- [x] Run exact request `33483342821` on `cd9023674cf659b384a7dac5ba03423bef797c63`: all 24 Python architecture regressions passed and fallback paths were empty, but Rendering Runtime changes still expanded transitively to unrelated modules; compilation then failed before tests because the moved Water probe lacked the `SurfaceExtraction` namespace for `VoxelSurfaceMetrics`.
- [x] Fix only those demonstrated causes: import the real public `VoxelSurfaceMetrics` namespace; expand known dependents only for module contract-surface changes (`/Api/` or asmdef) while ordinary Runtime implementation changes execute the owning module plus mandatory Kentridge. Add regression proving Runtime stays local and API still expands dependents.
- [x] Run exact request `33483749892` on `47a43b9539af487cae934478acce1ae48530e3ac`: all 25 Python regressions passed and fallback paths were empty, but the plan still selected WorldBuilder and many unrelated modules because changed Unity folder metadata (`*/Tests.meta`, etc.) had module owners and the old selection condition admitted any non-test-path owner. Unity then ran WorldBuilder first and failed before Water/Kentridge.
- [x] Fix the demonstrated metadata-selection defect: an owned path now selects module validation only when it is real production or meaningful non-meta module `Validation/` content. Add regressions proving `Tests.meta` and `Runtime.meta` are no-op changes while validation scenes still select their module.
- [x] Reconcile the 53-commit current master (`e98191876c104ff115a1828b1ce0a6b2d4d4480b`) normally into `fixes/agent-8` via PR #203 at merge commit `1c1dc14b17f09d412d785a91bbf433f5b8e4ffd4`; no force/synthetic merge.
- [x] Use newly accepted `Assets/Game/Characters/Tests/Game.Characters.Tests.asmdef` as an independent reuse consumer. Generalize module discovery so direct `Tests/*.asmdef` is deterministic EditMode ownership alongside explicit `Tests/{EditMode,PlayMode}`; add regression proving Character-style layout is auto-discovered without module registration. Do not modify Character production or its SceneIssue.

## Final gates
- [ ] Run exact-current-head automatic module tests, Water built-player validation, and mandatory Kentridge built-player validation using only `ci-test/fixes/agent-8`.
- [ ] Inspect every retained Water post-readiness standalone-player frame; reject pre-readiness evidence and verify production-quality canonical Water reuse.
- [ ] Confirm final automatic validation runtime/cost remains practical; prior full automatic path measured 179.82s but final architecture needs exact-head measurement.
- [ ] Review all 18 acceptance criteria against exact-head results. Criteria 1-13 and 15-17 have source/regression support; criterion 14 and final proof for 5/6/7/13 require canonical Water -> Kentridge standalone execution; criterion 18 requires final runtime measurement.
- [ ] After green exact-SHA proof, update `issue.json` status/resolution fields, move the assignment directory directly `open` -> `closed`, then fetch/merge current master again if it advanced. Revalidate if that changes the feature materially.
- [ ] Push the exact final feature head to `origin/master` non-force; if master advances, fetch/merge/retry. Do not promote the CI transport request commit.
