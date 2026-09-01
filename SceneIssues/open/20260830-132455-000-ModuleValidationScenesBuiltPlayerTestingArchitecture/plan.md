# Plan

## Acceptance
- Production diffs deterministically select owning modules; all EditMode and module-scoped PlayMode tests owned by each affected lower-level module run automatically.
- There is no repository-wide/top-level EditMode test assembly. EditMode tests live with the lower-level module they validate; only genuine high-level smoke/integration PlayMode remains top-level.
- Module ownership comes from repository/module structure and Unity assembly boundaries; shared/core changes expand through dependencies or conservative fallback.
- There is no `*.module-validation.json` registration layer. Module-local player targets are paired scene + `*.player-scenario.json` by convention.
- `KentridgePlayableSlice` is the mandatory built-player integration gate for production diffs.
- Water proves migration/reuse through the canonical production `WaterRenderingShowcase` and standalone-player evidence.
- Missing, zero-match, skipped, failed, or unexecuted required targets fail validation.

## Implemented architecture
- Module-local EditMode/PlayMode assemblies are structurally discovered from `Assets/**/Tests/{EditMode,PlayMode}` and `.asmdef` ownership.
- Top-level `Assets/Tests/PlayMode` is integration/smoke only and excluded from lower-level module ownership.
- Runtime `.asmdef` dependencies expand affected modules; unresolved production paths use safe fallback. Validation-only asmdefs are excluded from the runtime dependency graph.
- Test-only changes under `/Tests/` do not claim production ownership or cause module/player execution by themselves.
- Unowned top-level `Assets/Game/Composition/**` production changes are application-composition changes and receive the mandatory Kentridge integration gate rather than broad lower-level module fallback; composition subtrees with their own module tests still resolve normally.
- Module player scenes/scenarios are convention-discovered beneath module `Validation/`; the generic standalone-player runner has no Water/Kentridge/test-name-specific selection policy.
- Required module tests execute by assembly; required skipped/zero-match cases fail.
- Water `WaterDemo.unity` is a thin consumer of production `VoxelEngine.Showcase.WaterRenderingShowcase`; its stronger liquid-publication probe is module-owned under Rendering `Validation/Water` and observes the production `VoxelRenderBridge.SurfaceMetrics` diagnostic directly.

## Validation history / corrections
- `33469098939`: isolated/fixed top-level PlayMode falsely becoming synthetic production module `Assets`.
- `33469680497`: isolated/fixed migrated Rendering/Storage/WorldBuilder test friend access without widening production APIs.
- `33472056643`: after the second compile failure, isolated minimal WorldBuilder friend-assembly root cause before another fix.
- `33474565849` on `805ecc15c2091bf4e6ef1ef85df37d2c1b01120d`: planning + 20 Python regressions passed, compilation was clean, first module suite passed; `Game.WorldBuilder.Tests.EditMode` ran 351 tests (288 pass / 63 fail). Failure distribution proved stale branch/master divergence rather than another compiler/friend-access symptom.
- Feature was 216 commits behind master at merge base `142b1134bd9d6a9eb1d60e55a296afaf6d9e7b3e`. Master reconciled normally into the feature at `0b941fc62c87841bca949df32cf9ee3d6a4ded67` (PR #201), with no force/synthetic merge. Newly added upstream regressions were preserved in module-owned locations.
- Exact request `33476275534` on reconciled feature `41279d83c8dbea6ed8aa0a7b422cbdcac1e07cdf` again passed planning, all 20 Python regressions, compilation, and the first module suite, then `Game.WorldBuilder.Tests.EditMode` ran 367 tests (304 pass / 63 fail). Because the same 63-failure symptom survived a materially different master reconciliation, no third behavioral fix was attempted until a minimal repro/root cause was isolated.
- Minimal repeated-symptom root cause: much of the former repository-wide `VoxelEngine.Tests.EditMode` had been moved wholesale beneath WorldBuilder and renamed, leaving unrelated repository/Kentridge/Showcase checks under one WorldBuilder-owned assembly. Old filesystem scanners also treated relocated `/Tests/` files/asmdefs as production. Scanner/path/deleted-feature regressions were corrected without changing unrelated gameplay.
- Correctly parented exact run `33479440611` on feature `fa86f8dbd6db0966e66f47673c6fe90bbe6f7e1a` passed automatic planning and all 20 Python regressions, then ran the WorldBuilder suite and failed with 359 tests / 316 pass / 43 fail. The planner output exposed the remaining architecture defect: module selection still occurred for changed `/Tests/` paths even though `is_production()` rejected them, obsolete deleted `*.module-validation.json` paths were treated as production fallback, and validation-only Water support lived in unowned production roots. The 43 failures were unrelated pre-existing Kentridge/architecture assertions, so no gameplay fixes were attempted.
- Planner correction after `33479440611`: test paths no longer select module work; deleted obsolete module-validation manifest paths are non-production; validation asmdefs do not enter runtime dependency ownership; unowned top-level Game Composition changes are integration-only rather than broad lower-level fallback. New focused Python regressions cover each rule.
- WorldBuilder test migration no longer creates a production friend delta: the module-local suite retains its existing `VoxelEngine.Tests.EditMode` assembly identity at the new module-local path, so the already-existing production friend is reused and `Game.WorldBuilder.Api/AssemblyInfo.cs` matches master again.
- Water validation-only support moved from production/shared roots into `Assets/VoxelEngine/Rendering/Validation/Water`: the scene keeps its probe binding through the preserved Unity GUID, a module-local validation asmdef references Rendering Runtime, and the probe reads existing public read-only `VoxelRenderBridge.SurfaceMetrics` directly. The redundant shared `RenderingSurfaceDiagnostics` wrapper was removed. Rendering/Storage module-test friend boundaries remain test-only support; the obsolete `Game.WorldBuilder.Tests.EditMode` Rendering friend was removed.

## Blast radius
CI/orchestration; test/module ownership; module-local validation discovery; migration of test assemblies; thin Water validation composition. Master reconciliation incorporated accepted upstream production changes but agent-8 did not rewrite adjacent production systems. Repeated-failure corrections are limited to ownership/planning/scanner/path semantics and validation-support placement; unrelated Kentridge/gameplay failures are not being patched to make this architecture issue green.

## Remaining gates
- [x] Reconcile checklist with module-owned assembly/convention architecture.
- [x] Remove obsolete module-validation manifests and stale SpatialReservations registration residue.
- [x] Exclude top-level PlayMode from production ownership while retaining integration/smoke coverage.
- [x] Correct migrated Rendering/Storage/WorldBuilder test-only friend boundaries.
- [x] Reconcile current master and preserve newly added upstream regressions in module-owned suites.
- [x] After `33476275534` reproduced the same 63-failure symptom after master reconciliation, isolate the minimal test-migration root cause before another fix and correct only demonstrated scanner/path/deleted-regression defects.
- [x] After `33479440611` still selected WorldBuilder, isolate the remaining planner ownership root cause and correct test-only path selection, obsolete-manifest classification, validation dependency ownership, application-composition fallback semantics, and Water validation-support placement without patching unrelated gameplay.
- [ ] Run exact-current-head automatic module tests, Water built-player, and mandatory Kentridge built-player validation using only `ci-test/fixes/agent-8`.
- [ ] Inspect every retained Water standalone post-readiness frame for production quality and evidence-window pruning.
- [ ] Review all 18 acceptance criteria and update issue metadata only after green exact-SHA proof.
- [ ] Fetch/merge current `origin/master` again after green if it advanced, revalidate if materially changed, then promote the exact feature head to `origin/master` non-force.
