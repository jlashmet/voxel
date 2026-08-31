# Plan

## Acceptance
- Production diffs deterministically select owning modules; **all EditMode and module-scoped PlayMode tests owned by each affected lower-level module run automatically**. Agents do not register individual tests or maintain per-test filters.
- **There is no repository-wide/top-level EditMode test assembly. EditMode tests live with the lower-level module they validate** (for example Rendering, Structures, Spatial). A production module change runs that module's complete EditMode suite.
- Only genuinely high-level integration/smoke PlayMode coverage belongs at the top level. `KentridgePlayableSlice` remains the mandatory top-level built-player integration gate for production diffs.
- Module ownership comes from repository/module structure and Unity assembly boundaries. Shared/core changes expand through the production assembly dependency graph where practical, with conservative fallback only when ownership/dependency inference is unavailable.
- **There is no `*.module-validation.json` registration layer in the target architecture.** Module ownership, test ownership, and module-local player validation target discovery are convention/structure driven.
- Module-local built-player validation is discovered from the owning module's `Validation/` area. A validation target owns its scene plus a separate `*.player-scenario.json` when scenario behavior is required. The scenario JSON is executable validation content (timing/captures/assertions), not module/test registration metadata.
- Water proves migration/reuse with production rendering and production-quality standalone-player evidence.
- Missing/zero-match/skipped/failed required targets fail validation; routine targeted cost remains practical.

## Final architecture decision
The repository should not keep a reduced module manifest after removing explicit test filters. The entire `*.module-validation.json` concept is unnecessary because every registration concern it carried can be inferred from structure or replaced by a narrower piece of executable scenario content.

### 1. Module ownership is structural
A production diff identifies the owning lower-level Unity module from the changed path, module root, and production `.asmdef` boundaries. CI does not require a manifest containing `productionPaths` or `sharedPaths` for normal module ownership.

Shared/core production changes expand through production `.asmdef` dependency relationships where practical. If ownership/dependency inference is genuinely unresolved, validation remains conservative/fail-closed rather than silently narrowing the affected set.

### 2. Tests are owned by module-local test assemblies
Every lower-level module owns its own EditMode test assembly and any module-scoped PlayMode test assembly. CI discovers and runs the complete test assemblies owned by each affected module.

Adding a test beneath the module requires no planner change, manifest edit, class-name registration, or method filter. Test selection is assembly/module selection, never individual-test registration.

Repository-wide/top-level EditMode assemblies are eliminated. Existing tests in the broad `VoxelEngine.Tests.EditMode` assembly are migrated to the lower-level module whose production responsibility they validate. Only genuine assembled-game/integration/smoke PlayMode coverage remains top-level.

### 3. Module-local player validation is convention discovered
Player-visible modules may own one or more validation targets under their module-local `Validation/` area. A target is discovered from repository convention rather than a module manifest.

Target shape is conceptually:

```text
<Module>/
  Api/
  Runtime/
  Tests/
    EditMode/
    PlayMode/
  Validation/
    <Target>/
      <Target>.unity
      <Target>.player-scenario.json
```

Equivalent flat naming inside `Validation/` is acceptable during migration if discovery remains deterministic, but the important rule is that the scene/scenario pairing is locally discoverable without `*.module-validation.json`.

The validation scene exercises the real production implementation. The separate `*.player-scenario.json` may define run duration, capture cadence, evidence windows, movement/timeline actions, and required/forbidden runtime assertions. This file remains because those values are actual validation behavior that cannot always be inferred from repository structure. It must not contain production ownership mappings, test filters, module dependency declarations, or other registration bookkeeping.

### 4. Kentridge is repository CI policy
`KentridgePlayableSlice` is attached automatically to production changes as the canonical built-player assembled-game integration gate. Modules and feature agents never register or select it.

The normal validation flow therefore becomes:

```text
production diff
  -> infer affected module(s)
  -> run each affected module's complete EditMode/module-scoped PlayMode assemblies
  -> discover and run each affected player-visible module's local Validation target(s)
  -> run built-player KentridgePlayableSlice
```

### 5. Delete the module manifest concept
Remove `*.module-validation.json` files, their schema/parser, planner semantics, manifest-specific regression fixtures, and documentation that tells agents/modules to maintain them.

The current Water manifest demonstrates why it should disappear:
- `productionPaths` / `sharedPaths` duplicate structural ownership that CI should derive from module/assembly boundaries.
- `tests` duplicates Unity test ownership and creates stale-registration/zero-match failure modes.
- `playerValidation.scene` / `playerValidation.scenario` is a pairing that can be discovered from the module-local `Validation/` convention.

After migration, the remaining Water `*.player-scenario.json` is intentionally retained only as scenario behavior.

### 6. Required architectural regressions
- Adding a new test to a module-local EditMode/PlayMode assembly is automatically included without changing planner code or metadata.
- A production module diff selects all of that module's test assemblies, not a hand-maintained subset.
- Reintroducing a repository-wide/top-level EditMode assembly fails architecture validation.
- Reintroducing `*.module-validation.json` registration semantics is rejected or ignored by the planner; no required validation may depend on it.
- A module-local validation scene/scenario target is discovered from convention alone.
- Shared/core production changes expand through the production assembly dependency graph where practical and otherwise retain conservative fail-closed behavior.
- Missing/ambiguous scene-scenario pairing fails rather than silently skipping player validation.

## Implemented before architecture correction
- The earlier implementation used repository-driven `*.module-validation.json`, diff ownership/shared-core expansion, separate player scenarios, mandatory Kentridge integration, generic readiness/evidence windows, and an independent Structures reuse fixture. The manifest-driven portion is now explicitly superseded by the final architecture above.
- Exact run `33375145205` proved automatic Water focused tests -> Water player -> Kentridge player in 179.82s (~10.3% over the prior 163s path).
- Required Water correctness defects were isolated with behavioral regressions: shared-arena addressing, upward topology, greedy planar merging blocking waves, presentation-only displacement, and bulk terrain authoring budget.
- Runs `33385476451`, `33388147850`, and `33390924383` passed automated module/player gates, but the first two retained Water tableaus failed direct production-quality review.
- Ownership/fallback audit confirmed unknown production paths cannot degrade to integration-only validation under the earlier planner; the corrected planner must preserve that fail-closed property without manifest registration.
- Post-master exact run `33431392723` proved the requested PlayMode regression and automatic planning but failed closed when the then-explicit Water EditMode filter matched zero tests. That failure is direct evidence for removing individual-test registration. Root-cause comparison found the master merge had also dropped both the flat-water top-tessellation regression and its narrow production invariant; both were restored while retaining the newer canonical Water material/topology/spray behavior.
- Run `33432210469` passed the prior exact-head architecture, after which direct artifact review exposed a generic evidence-window filename parsing defect; the current branch contains the generic parser fix and regression, which still requires exact-head validation after the architecture correction.

## Reuse boundary / resolved prerequisite
- After two materially different scene-level corrections, root-cause review found Agent-8 was duplicating Water showcase composition policy. Agent-9 owns the canonical production `WaterRenderingShowcase`; its scene is a thin shell around shared `VoxelEngine.Showcase.WaterRenderingShowcase` composition and has semantic production-path regressions.
- Agent-9's canonical Water work landed on `master` in close commit `0de38ba704be999c13c9c9aa59237efa65405144`, clearing the external prerequisite without copying/cherry-picking another assignment.
- The module-local Water scene already consumes the canonical `WaterRenderingShowcase` component. Integration therefore stays at the semantic boundary: own the production composition rather than cloning showcase policy. No third bespoke tableau/camera/shader tweak is permitted or needed.

## Blast radius
CI/orchestration; module and test-assembly discovery; removal of module-validation manifest parsing/fixtures/docs; migration of existing broad EditMode tests into owning lower-level test assemblies; convention-based module-local player target discovery; validation assets/tests/docs; and the Water validation adapter/scenario. No new simulation/collision policy or adjacent-system refactor.

## Remaining gates
- [ ] Eliminate repository-wide/top-level EditMode test assembly ownership and migrate its tests into lower-level module-owned EditMode assemblies.
- [ ] Delete `*.module-validation.json` and remove manifest schema/parser/planner/test-registration semantics.
- [ ] Replace explicit test registration with convention/assembly-driven discovery that runs every affected module EditMode/module-scoped PlayMode assembly.
- [ ] Discover module-local built-player scene/scenario targets from the `Validation/` convention with no module manifest.
- [ ] Prove a new module test is automatically included without metadata/planner registration and prove shared/core dependency expansion remains conservative.
- [ ] Prove architecture validation rejects a reintroduced top-level EditMode test assembly.
- [ ] Prove module-local player validation is discovered from scene/scenario convention alone and fails closed on ambiguous/missing pairing.
- [ ] Run exact-head module tests, Water built-player, and mandatory Kentridge built-player validation.
- [ ] Inspect every retained Water standalone frame and verify production quality/evidence-window pruning.
- [ ] Review all 18 criteria; then complete metadata, move open -> closed, merge current master, and promote non-force.
