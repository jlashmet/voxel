# Plan

## Acceptance
- Production diffs deterministically select owning modules; **all EditMode and module-scoped PlayMode tests owned by each affected lower-level module run automatically**. Agents do not register individual tests or maintain per-test filters.
- **There is no repository-wide/top-level EditMode test assembly. EditMode tests live with the lower-level module they validate** (for example Rendering, Structures, Spatial). A production module change runs that module's complete EditMode suite.
- Only genuinely high-level integration/smoke PlayMode coverage belongs at the top level. `KentridgePlayableSlice` remains the mandatory top-level built-player integration gate for production diffs.
- Module ownership comes from repository/module structure and Unity assembly boundaries. Shared/core changes expand through production assembly dependencies where practical, with conservative fallback when inference is unavailable.
- **There is no `*.module-validation.json` registration layer.** Module ownership, test ownership, and module-local player targets are convention/structure driven.
- Player-visible modules own paired scene/scenario targets under module-local `Validation/`; scenario JSON contains executable behavior only.
- Water proves migration/reuse through the canonical production `WaterRenderingShowcase` and production-quality standalone-player evidence.
- Missing/zero-match/skipped/failed required targets fail validation; routine targeted cost remains practical.

## Selected architecture / implemented state
- Module roots and complete module-local EditMode/PlayMode test assemblies are discovered structurally from `Assets/**/Tests/{EditMode,PlayMode}` and Unity `.asmdef` ownership.
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
- Exact request for feature `1c82800896e40f34a1a4550865a83bd975a44428`, run `33463299020`, failed **before Unity** in automatic planning: `Assets/Game/Scenes/interactables-secrets.module-validation.json` remained from the old registration mechanism. This was an architecture migration defect, not infrastructure or rendering.
- Live-tree inventory then found the remaining obsolete registrations; Structures' manifest had already disappeared on the live branch, the remaining interactables manifest was deleted, and feature tree `b6344668d8b904e488f22cdc66eb15702e376874` was clean at that point.
- The same audit corrected Water discovery/location to the documented `Rendering/Validation/Water` convention and wired generic `tools/tests/test_*.py` discovery into targeted CI.
- Exact request run `33464201573` for feature `7d88992a6a296c4dcb743c6ad219ec7ebfec77a3` failed before Unity at the intended repository-wide architecture guard because `Assets/Game/WorldBuilder/Generation/Validation/SpatialReservations/spatial-reservations.module-validation.json` had subsequently been introduced by the separate SpatialReservations assignment. The same file is currently present on both `master` and `fixes/agent-7`.
- This is an external prerequisite/blocker, not a reason to weaken acceptance: this issue explicitly requires the registration concept to be absent repository-wide, and its regression must continue to fail when one is reintroduced. Agent-8 will not modify the SpatialReservations assignment. No replacement targeted CI request should be sent until that external file is removed upstream.

## Blast radius
CI/orchestration; module/test-assembly discovery; removal of module-registration manifests/docs; migration of broad EditMode ownership; convention-based module-local player target discovery; validation tests/assets; thin Water validation composition. No new simulation/collision policy or adjacent-system refactor.

## Remaining gates
- [x] Reconcile checklist items with the implemented module-owned assembly migration and convention regressions.
- [ ] External prerequisite: SpatialReservations must remove its obsolete `*.module-validation.json` registration without changing this issue's acceptance.
- [ ] Run exact-current-head automatic module tests, Water built-player, and mandatory Kentridge built-player validation using only `ci-test/fixes/agent-8` after the blocker is gone.
- [ ] Inspect every retained Water standalone post-readiness frame and verify production quality/evidence-window pruning.
- [ ] Review all 18 acceptance criteria; complete metadata/closure only after green exact-SHA proof.
- [ ] Merge current `origin/master`, revalidate if the exact feature head changes materially, then promote that exact head to `origin/master` non-force.
