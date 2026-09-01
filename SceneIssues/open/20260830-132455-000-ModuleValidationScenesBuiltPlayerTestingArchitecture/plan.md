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
- Master introduced five new top-level EditMode regressions during the divergence. They are now migrated by production ownership: Kentridge road composition, terrain grading envelope, and world-road presentation -> `Game.WorldBuilder.Tests.EditMode`; generic road surface detail -> `VoxelEngine.Rendering.Tests.EditMode`; generic continuous terrain-corridor rasterisation -> new `VoxelEngine.Structures.Tests.EditMode`. The temporary legacy copies are deleted. `Assets/Tests/EditMode` now has no directory/test assembly (only its historical folder `.meta`).
- Current feature is based on current master (`behind_by: 0` at the latest compare).

## Blast radius
CI/orchestration; test/module ownership; module-local validation discovery; migration of test assemblies; thin Water validation composition. Master reconciliation incorporated accepted upstream production changes but agent-8 did not rewrite adjacent production systems. New reconciliation work is test-location/assembly ownership only.

## Remaining gates
- [x] Reconcile checklist with module-owned assembly/convention architecture.
- [x] Remove obsolete module-validation manifests and stale SpatialReservations registration residue.
- [x] Exclude top-level PlayMode from production ownership while retaining integration/smoke coverage.
- [x] Correct migrated Rendering/Storage/WorldBuilder test-only friend boundaries.
- [x] Reconcile current master and preserve newly added upstream regressions in module-owned suites.
- [ ] Run exact-current-head automatic module tests, Water built-player, and mandatory Kentridge built-player validation using only `ci-test/fixes/agent-8`.
- [ ] Inspect every retained Water standalone post-readiness frame for production quality and evidence-window pruning.
- [ ] Review all 18 acceptance criteria and update issue metadata only after green exact-SHA proof.
- [ ] Fetch/merge current `origin/master` again after green if it advanced, revalidate if materially changed, then promote the exact feature head to `origin/master` non-force.
