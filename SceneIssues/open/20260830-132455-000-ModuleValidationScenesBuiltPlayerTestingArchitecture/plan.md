# Plan

## Acceptance model
- Production diffs deterministically select owning modules and all focused module test assemblies.
- Top-level `Assets/Tests/PlayMode` is integration/smoke only; there is no repository-wide EditMode assembly.
- Module tests are discovered structurally under `Tests/`: direct `Tests/*.asmdef` is EditMode, and `Tests/{EditMode,PlayMode}` is explicit platform ownership.
- Shared/API/asmdef contract changes expand through known module dependencies; ordinary Runtime implementation changes stay owning-module scoped plus mandatory Kentridge.
- Unknown production falls back broadly; unowned `Assets/Game/Composition/**` is application composition and receives Kentridge rather than pretending to be a lower-level module.
- Test-only changes and Unity `.meta` files do not select production validation. Meaningful module-local `Validation/` content selects its module.
- Module player targets are convention-paired `Validation/*.unity` + same-stem `*.player-scenario.json`; no `*.module-validation.json` registration.
- One generic standalone-player runner serves module validation, Kentridge, and SceneIssue replay. Missing/zero/skipped/unexecuted required gates fail.
- Water proves the visual migration through production `VoxelEngine.Showcase.WaterRenderingShowcase`, a Rendering-owned validation scene/scenario/probe, and standalone-player evidence only.

## Corrective validation history
- `33469098939`: fixed top-level PlayMode becoming synthetic module `Assets`.
- `33469680497` / `33472056643`: isolated migrated test friend-boundary compile failures without widening production APIs; after the second compile symptom the minimal WorldBuilder friend root cause was isolated before another fix.
- `33474565849`: planning/Python/compile passed; WorldBuilder 351 tests = 288 pass / 63 fail. Feature was 216 commits stale, so master was reconciled normally at `0b941fc62c87841bca949df32cf9ee3d6a4ded67` instead of patching stale gameplay.
- `33476275534`: same 63 failures after materially different master reconciliation (367 tests = 304 pass / 63 fail). Required repeated-symptom isolation showed the former repository-wide suite had been moved wholesale beneath WorldBuilder and old scanners/path guards no longer matched module-local test ownership.
- `33479440611`: 20 Python regressions passed; WorldBuilder 359 tests = 316 pass / 43 fail. Planner still selected test-only paths and obsolete manifests, and Water validation-only support lived in unowned production/shared roots. Those ownership defects were corrected; unrelated Kentridge/gameplay failures were not patched.
- `33483342821`: 24 Python regressions passed and fallback paths were empty. Rendering Runtime still expanded through all dependents and the moved Water probe had a concrete `VoxelSurfaceMetrics` namespace compile error. Probe import fixed; dependent expansion restricted to `/Api/` or asmdef contract changes.
- `33483749892`: 25 Python regressions passed and fallback paths were empty, but Unity folder metadata such as `*/Tests.meta` still selected module owners and caused unrelated WorldBuilder execution. Selection now requires real production or meaningful non-meta module Validation content; regressions cover `Tests.meta` and `Runtime.meta` no-op behavior.
- Current master advanced by 53 commits to `e98191876c104ff115a1828b1ce0a6b2d4d4480b`; it was merged normally into this feature via PR #203 at merge commit `1c1dc14b17f09d412d785a91bbf433f5b8e4ffd4`.
- That merge introduced an independent `Assets/Game/Characters/Tests/Game.Characters.Tests.asmdef` consumer. Discovery was generalized so direct module `Tests/*.asmdef` is deterministic EditMode ownership, with a regression proving production changes select it automatically. No Character production or Character SceneIssue files were modified by this architecture correction.

## Water validation ownership
- `WaterDemo.unity` remains a thin consumer of production `WaterRenderingShowcase`.
- Water liquid-publication acceptance support lives under `Assets/VoxelEngine/Rendering/Validation/Water` with a module-local validation asmdef.
- The probe preserves its Unity GUID and reads the existing public read-only `VoxelRenderBridge.SurfaceMetrics`; the redundant shared diagnostics wrapper was removed.

## Remaining gates
- [x] Reconcile architecture/docs/test ownership and repeated-failure root causes.
- [x] Reconcile current master before the next exact-head validation.
- [ ] Run exact-current-head automatic focused tests, Water built-player validation, and mandatory Kentridge built-player validation using only `ci-test/fixes/agent-8`.
- [ ] Inspect every retained Water post-readiness standalone frame and reject pre-readiness evidence.
- [ ] Measure final runtime/cost and review all 18 acceptance criteria.
- [ ] After green exact-SHA proof, set issue fixed/resolved, move `open` -> `closed`, reconcile master again if advanced, revalidate if materially changed, and promote the exact feature head to `origin/master` non-force.
