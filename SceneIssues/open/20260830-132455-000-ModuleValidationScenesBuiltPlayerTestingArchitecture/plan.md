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
- Runtime `.asmdef` dependencies expand affected modules; unresolved production paths use safe fallback.
- Module player scenes/scenarios are convention-discovered beneath module `Validation/`; the generic standalone-player runner has no Water/Kentridge/test-name-specific selection policy.
- Required module tests execute by assembly; required skipped/zero-match cases fail.
- Water `WaterDemo.unity` is a thin consumer of production `VoxelEngine.Showcase.WaterRenderingShowcase` plus the generic readiness probe.

## Validation history / corrections
- `33469098939`: isolated/fixed top-level PlayMode falsely becoming synthetic production module `Assets`.
- `33469680497`: isolated/fixed migrated Rendering/Storage/WorldBuilder test friend access without widening production APIs.
- `33472056643`: after the second compile failure, isolated minimal WorldBuilder friend-assembly root cause before another fix; added migrated `Game.WorldBuilder.Tests.EditMode` friend only.
- `33474565849` on `805ecc15c2091bf4e6ef1ef85df37d2c1b01120d`: planning + 20 Python regressions passed, compilation was clean, first module suite passed; `Game.WorldBuilder.Tests.EditMode` ran 351 tests (288 pass / 63 fail). Failure distribution proved stale branch/master divergence rather than another compiler/friend-access symptom.
- Feature was 216 commits behind master at merge base `142b1134bd9d6a9eb1d60e55a296afaf6d9e7b3e`. Master had changed the same former top-level tests and WorldBuilder/Structures contracts that this feature migrated.
- Reconciliation conflicts were limited to moved-test delete/modify cases. The two master-modified tests were carried into their migrated WorldBuilder locations; their legacy paths were temporarily restored only to allow a normal merge. Master then merged normally into the feature at `0b941fc62c87841bca949df32cf9ee3d6a4ded67` (PR #201), with no force/synthetic merge.
- Master introduced five new top-level EditMode regressions during the divergence. They were migrated by production ownership: Kentridge road composition, terrain grading envelope, and world-road presentation -> WorldBuilder; generic road surface detail -> Rendering; generic continuous terrain-corridor rasterisation -> Structures. Temporary legacy copies were deleted.
- Exact request `33476275534` on reconciled feature `41279d83c8dbea6ed8aa0a7b422cbdcac1e07cdf` again passed planning, all 20 Python regressions, compilation, and the first module suite, then `Game.WorldBuilder.Tests.EditMode` ran 367 tests (304 pass / 63 fail). Because the same 63-failure symptom survived a materially different master reconciliation, no third behavioral fix was attempted until a minimal repro/root cause was isolated.
- Minimal root cause: the migration had renamed much of the former repository-wide `VoxelEngine.Tests.EditMode` wholesale into `Game.WorldBuilder.Tests.EditMode`; this left unrelated Constitution, repository architecture, Showcase, Rendering/Storage, and Kentridge legacy guards under one WorldBuilder-owned assembly. Moving those tests beneath `Assets/Game/.../Tests` also made old repository scanners incorrectly classify test source/test asmdefs as production, and several tests retained package/scene paths for production assets that had since moved or been deleted.
- Corrections are migration-boundary only: production scanners now exclude module-local `/Tests/` and `/Editor/`; WorldGen/Kentridge ownership guards point at current `Assets/Game/WorldBuilder/Generation`; current Showcase boundary guards validate the `Assets/Game/Composition/Showcase` ownership and scene-asset shell; deleted WorldbuildingGallery and removed Showcase traversal implementation regressions were removed rather than recreating obsolete behavior or modifying their foreign SceneIssues. No adjacent production implementation was changed for these failures.

## Blast radius
CI/orchestration; test/module ownership; module-local validation discovery; migration of test assemblies; thin Water validation composition. Master reconciliation incorporated accepted upstream production changes but agent-8 did not rewrite adjacent production systems. Repeated-failure corrections are limited to test ownership/scanner/path semantics and removal of regressions whose production targets no longer exist.

## Remaining gates
- [x] Reconcile checklist with module-owned assembly/convention architecture.
- [x] Remove obsolete module-validation manifests and stale SpatialReservations registration residue.
- [x] Exclude top-level PlayMode from production ownership while retaining integration/smoke coverage.
- [x] Correct migrated Rendering/Storage/WorldBuilder test-only friend boundaries.
- [x] Reconcile current master and preserve newly added upstream regressions in module-owned suites.
- [x] After `33476275534` reproduced the same 63-failure symptom after master reconciliation, isolate the minimal test-migration root cause before another fix and correct only demonstrated scanner/path/deleted-regression defects.
- [ ] Run exact-current-head automatic module tests, Water built-player, and mandatory Kentridge built-player validation using only `ci-test/fixes/agent-8`.
- [ ] Inspect every retained Water standalone post-readiness frame for production quality and evidence-window pruning.
- [ ] Review all 18 acceptance criteria and update issue metadata only after green exact-SHA proof.
- [ ] Fetch/merge current `origin/master` again after green if it advanced, revalidate if materially changed, then promote the exact feature head to `origin/master` non-force.
