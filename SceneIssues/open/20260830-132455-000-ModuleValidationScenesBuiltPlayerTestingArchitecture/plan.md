# Plan

## Acceptance
- Production diffs deterministically select owning modules; **all EditMode and module-scoped PlayMode tests owned by each affected lower-level module run automatically**. Agents do not register individual tests or maintain per-test filters.
- **There is no repository-wide/top-level EditMode test assembly. EditMode tests live with the lower-level module they validate** (for example Rendering, Structures, Spatial). A production module change runs that module's complete EditMode suite.
- Only genuinely high-level integration/smoke PlayMode coverage belongs at the top level and does **not** define production-module ownership. `KentridgePlayableSlice` remains the mandatory top-level built-player integration gate for production diffs.
- Module ownership comes from repository/module structure and Unity assembly boundaries. Shared/core changes expand through production assembly dependencies where practical, with conservative fallback when inference is unavailable.
- **There is no `*.module-validation.json` registration layer.** Module ownership, test ownership, and module-local player targets are convention/structure driven.
- Player-visible modules own paired scene/scenario targets under module-local `Validation/`; scenario JSON contains executable behavior only.
- Water proves migration/reuse through the canonical production `WaterRenderingShowcase` and production-quality standalone-player evidence.
- Missing/zero-match/skipped/failed required targets fail validation; routine targeted cost remains practical.

## Selected architecture / implemented state
- Lower-level module roots and complete module-local EditMode/PlayMode test assemblies are discovered structurally from `Assets/**/Tests/{EditMode,PlayMode}` and Unity `.asmdef` ownership; repository-wide `Assets/Tests/PlayMode` is retained solely as high-level integration/smoke and excluded from production ownership.
- Runtime `.asmdef` name/GUID references expand shared/core changes through dependent modules; unknown production ownership broadens conservatively.
- Reintroduced top-level `Assets/Tests/EditMode` assemblies and `*.module-validation.json` registrations fail architecture validation.
- Player targets are convention-discovered under `<Module>/Validation`; every scene requires an adjacent same-stem `*.player-scenario.json`, and orphan/missing pairs fail closed.
- `KentridgePlayableSlice` is repository CI policy for production changes, never module registration.
- Targeted CI uses the exact feature SHA as the request parent; an explicit test filter is optional diagnostics only. Automatic validation runs all affected assemblies, player targets, Kentridge, and repository Python validation regressions.
- Water validation is a thin module-local scene consuming the canonical production `VoxelEngine.Showcase.WaterRenderingShowcase` plus the existing readiness probe; no parallel Water composition is owned here.
- Contributor and SceneIssue workflow docs now describe the manifest-free convention.

## Material results
- Earlier manifest-driven runs proved the automatic Water -> Kentridge player path and isolated/fixed Water correctness/evidence issues; that registration architecture is superseded.
- Agent-9's canonical Water implementation landed on master in `0de38ba704be999c13c9c9aa59237efa65405144`, clearing the reuse prerequisite.
- Run `33463299020` failed before Unity because a residual interactables manifest remained; that obsolete registration was removed.
- Run `33464201573` then failed before Unity because the already-completed SpatialReservations assignment had merged a legacy `spatial-reservations.module-validation.json`; audit proved the SceneIssue was already closed and the file was stale residue, so agent-8 removed only that obsolete registration.
- Exact request run `33469098939` for feature `53d2fa1029468984d6cfb76046d4968d0aca7ae3` exposed a second planner defect before Unity: `Assets/Tests/PlayMode/VoxelEngine.Tests.PlayMode.asmdef` created synthetic module root `Assets`, so every lower-level runtime assembly had two owners (first reported `Game.Composition.CharacterEquipment.Editor`). The root cause is now isolated and covered by a regression: top-level PlayMode remains available as integration/smoke but is excluded from lower-level production ownership. Planner fix landed at `d19241e8494ce68f9b34ce57f0b78c8559d1b7d7`.
- Exact request run `33469680497` for feature `af0bbf774f65a4cf0d610802aa9aa9938a32f781` passed planning and repository Python/planner regressions, then failed Unity compilation because migrated module-owned EditMode assemblies no longer matched Rendering's legacy `InternalsVisibleTo("VoxelEngine.Tests.EditMode")` declaration. The failure was limited to legitimate internal Rendering test consumers in `VoxelEngine.Rendering.Tests.EditMode`, `VoxelEngine.Storage.Tests.EditMode`, and `Game.WorldBuilder.Tests.EditMode`; the test-only friend boundary was corrected without widening production APIs.
- Exact request run `33472056643` for feature `8461063f9fa233190f8d16308fff9be3b1b83b5d` again passed planning and all 20 repository Python regressions, then failed Unity compilation on WorldBuilder builder/ref members used by the migrated WorldBuilder suite. Minimal repro/root-cause audit showed those members are intentionally `internal` and `Game.WorldBuilder.Api/AssemblyInfo.cs` still friended only the deleted monolithic `VoxelEngine.Tests.EditMode`; `Game.WorldBuilder.Tests.EditMode` is now added as the replacement test friend. No production API was widened and the test call sites remain unchanged.
- Exact request run `33474565849` for feature `805ecc15c2091bf4e6ef1ef85df37d2c1b01120d` passed planning, all 20 repository Python regressions, and the first automatic module suite, then executed `Game.WorldBuilder.Tests.EditMode`: 351 tests ran, 288 passed, 63 failed across 34 source files. These are no longer compile failures; failures span stale showcase paths, Kentridge geometry/reservations, constitution checks, game/worldgen assembly boundaries, and other contracts changed on master after this feature diverged.
- Branch-drift audit shows `fixes/agent-8` is 216 commits behind current master with merge base `142b1134bd9d6a9eb1d60e55a296afaf6d9e7b3e`. The feature migration renamed much of the former top-level EditMode suite into `Assets/Game/WorldBuilder/Tests/EditMode`, while master has continued modifying those same tests and underlying WorldBuilder/Structures contracts. A normal master -> feature reconciliation attempt (temporary PR #199) reported merge conflicts and was closed unmerged; no force/synthetic merge was used. This is now the blocker for obtaining meaningful final exact-head module/player evidence without duplicating accepted upstream fixes.
- Static audit confirms current `WaterDemo.unity` is a thin production consumer and `WaterDemo.player-scenario.json` contains only executable timing/capture/assertion behavior.

## Blast radius
CI/orchestration; module/test-assembly discovery; removal of module-registration manifests/docs; migration of broad EditMode ownership; convention-based module-local player target discovery; validation tests/assets; thin Water validation composition. Corrective work is limited to preserving intentional test-only friend access after assembly migration. No new simulation/collision policy or adjacent-system refactor.

## Blockers
- Current master reconciliation is required before final validation because exact-head run `33474565849` is exercising stale pre-master WorldBuilder/Kentridge contracts. `fixes/agent-8` is 216 commits behind master and GitHub reports merge conflicts for a normal master -> feature merge. Do not rewrite the 63 failing tests or adjacent production systems to the stale branch state; resolve only the feature/master overlaps when a normal merge-capable workspace is available, then re-run exact-head validation through `ci-test/fixes/agent-8`.

## Remaining gates
- [x] Reconcile checklist items with the implemented module-owned assembly migration and convention regressions.
- [x] Remove the stale SpatialReservations `*.module-validation.json` residue after verifying its owning SceneIssue was already closed.
- [x] Isolate/fix top-level PlayMode falsely claiming production ownership; preserve it as high-level smoke/integration only.
- [x] Correct Rendering internal-test access for the three migrated EditMode assemblies exposed by exact-head run `33469680497`.
- [x] After the second exact-head Unity compile failure, isolate the minimal WorldBuilder repro/root cause and migrate its stale monolithic test friend to `Game.WorldBuilder.Tests.EditMode`.
- [ ] Reconcile current `origin/master` into `fixes/agent-8` without discarding either accepted upstream fixes or assignment-owned validation architecture; currently blocked by real merge conflicts and unavailable local git networking.
- [ ] Run exact-current-head automatic module tests, Water built-player, and mandatory Kentridge built-player validation using only `ci-test/fixes/agent-8`.
- [ ] Inspect every retained Water standalone post-readiness frame and verify production quality/evidence-window pruning.
- [ ] Review all 18 acceptance criteria; complete metadata/closure only after green exact-SHA proof.
- [ ] Fetch/merge current `origin/master` again after green if it advanced, then promote that exact head to `origin/master` non-force.