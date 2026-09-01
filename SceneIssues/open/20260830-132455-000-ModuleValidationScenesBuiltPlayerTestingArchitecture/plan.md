# Plan

## Acceptance model
- Production diffs deterministically select owning modules and all focused module test assemblies.
- Top-level `Assets/Tests/PlayMode` is integration/smoke only; there is no repository-wide EditMode assembly.
- Module tests are discovered structurally under `Tests/`: direct `Tests/*.asmdef` is EditMode, and `Tests/{EditMode,PlayMode}` is explicit platform ownership.
- Shared/API/asmdef contract changes expand through known module dependencies; ordinary Runtime implementation changes stay owning-module scoped plus mandatory Kentridge.
- Unknown production falls back broadly; unowned `Assets/Game/Composition/**` receives Kentridge rather than pretending to be a lower-level module.
- Test-only changes and Unity `.meta` files do not select production validation. Meaningful module-local `Validation/` content selects its module.
- Module player targets are convention-paired `Validation/*.unity` + same-stem `*.player-scenario.json`; no `*.module-validation.json` registration.
- One generic standalone-player runner serves module validation, Kentridge, and SceneIssue replay. Missing/zero/skipped/unexecuted required gates fail.
- Required module tests keep a graphics device available so real Rendering compute regressions execute.
- Water proves visual migration through production `VoxelEngine.Showcase.WaterRenderingShowcase`, a Rendering-owned validation scene/scenario/probe, and standalone-player evidence only.

## Final exact-SHA proof
- Exact feature SHA `8157139c2788d289a10241ca382099611ef3d1f0` was validated only through `ci-test/fixes/agent-8`; request commit `6564ecdefb0abe63d588f0fa796919aaa5e5538c` is directly parented on that SHA and changes only `.github/test-request.json`.
- Run `33498835984` passed end-to-end. Automatic planning selected only `Assets/VoxelEngine/Rendering`, its EditMode assembly, Rendering-owned Water player validation, and mandatory `KentridgePlayableSlice`; fallback paths were empty.
- All 31 planner/runner architecture regressions passed. Rendering ran 253/253 tests green with graphics enabled, including GPU sample-field and vertex-attribute parity at both source steps.
- Water built-player validation passed with four retained standalone frames at 8.3s, 14.3s, 20.3s, and 26.3s. Every retained frame was inspected directly after readiness; water remained coherent with stylized reflection/ripple treatment, and the waterfall visibly advanced between later frames. No pre-readiness, blank, RenderTexture, or PlayMode screenshot was used as visual proof.
- Kentridge built-player validation passed with its normal gameplay/capture scenario and no harness-specific request from the feature agent.
- Automatic validation cost from the durable summary was 248.33s total: Rendering tests 74.91s, Water player 73.02s, Kentridge player 100.41s. The GitHub automatic-validation step was 253s; the whole runner job was 274s. Work stayed limited to one affected module plus mandatory Kentridge, with no unrelated module visual scenes.

## Acceptance audit
1. **Pass** — diff-driven planner deterministically mapped the final production delta to Rendering.
2. **Pass** — focused tests and player targets are structurally discovered from module `Tests/` and paired `Validation/*.unity` + scenario conventions.
3. **Pass** — Water owns `Assets/VoxelEngine/Rendering/Validation/Water/WaterDemo.unity` beneath Rendering.
4. **Pass** — the shared runner built/captured the discovered Water scene without test-name inference or permanent build registration.
5. **Pass** — final visual proof is the built standalone Water artifact; PlayMode/RenderTexture evidence is not used for acceptance.
6. **Pass** — the Rendering production diff automatically ran Rendering focused tests and built-player Kentridge.
7. **Pass** — Rendering additionally ran its module-local Water scene/scenario through the same runner.
8. **Pass** — the request named no test, scene, scenario, screenshot profile, or player-build command; CI inferred all targets.
9. **Pass** — regressions cover API/asmdef dependent expansion and broad-safe fallback for unknown production; ordinary Runtime stays owner-scoped.
10. **Pass** — generic harness feature/test-name/scene-name audit policy was removed; executable camera/timing/capture policy lives in scenarios.
11. **Pass** — scene and scenario remain separate target fields and both Water/Kentridge use the same shared runner.
12. **Pass** — `AGENTS.md`, SceneIssue workflow docs, CI semantics, and validation guidance were updated for the module-author workflow.
13. **Pass** — exact-SHA status, required zero-match/skip/missing scene/scenario/capture failures, and non-executed-gate failures are enforced; final exact run executed all required targets.
14. **Pass** — ordinary Rendering production changes automatically produced `253/253 focused tests -> Water built-player -> Kentridge built-player` on run `33498835984`.
15. **Pass** — no second player harness, module-specific build implementation, or alternate targeted-CI transport was created.
16. **Pass** — focused behavioral regressions remain module-owned while visual acceptance semantics are standalone-player-only.
17. **Pass** — architecture is now code/tests/scenes authored by modules, diff discovery by CI, module tests for contract, module player for visuals, Kentridge for assembled integration.
18. **Pass** — final automatic work measured 248.33s and remained owner-scoped plus Kentridge; no unrelated module visual targets ran.

## Water validation ownership
- `WaterDemo.unity` is a thin consumer of production `WaterRenderingShowcase`.
- Water liquid-publication acceptance support lives under `Assets/VoxelEngine/Rendering/Validation/Water` with a module-local validation asmdef.
- The probe preserves its Unity GUID and reads public read-only `VoxelRenderBridge.SurfaceMetrics`; no parallel renderer/diagnostics authority.
- Independent direct-`Tests/*.asmdef` discovery is proven by the Character-style consumer fixture without modifying Character production.

## Remaining gates
- [x] Reconcile architecture/docs/test ownership and repeated-failure root causes.
- [x] Reconcile current master before exact-head validation.
- [x] Run exact-current-head automatic focused tests, Water built-player validation, and mandatory Kentridge built-player validation using only `ci-test/fixes/agent-8`.
- [x] Inspect every retained Water post-readiness standalone frame and reject pre-readiness evidence; only production-quality proof retained.
- [x] Measure final runtime/cost and review all 18 acceptance criteria.
- [ ] Set issue fixed/resolved and move `open` -> `closed`; then reconcile master again if advanced, revalidate if materially changed, and promote the exact feature head to `origin/master` non-force.
