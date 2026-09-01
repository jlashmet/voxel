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

## Material validation results
- Planner/runner regressions are 29/29 green and exact plans select only Rendering EditMode + Water player + mandatory Kentridge with no fallback.
- Earlier broad WorldBuilder execution was traced to migrated repository-wide tests, test/meta path ownership, and Runtime dependency over-expansion; those ownership defects were fixed without patching unrelated gameplay.
- `33485149715`: Rendering 229/254; 22 GPU tests were hidden by `-nographics`, and three Water tests exposed an unrelated per-voxel tessellation branch delta. Required module tests now retain graphics; Water production was reverted to master.
- `33487852636`: Rendering 248/254. Four Water fixtures assumed obsolete pre-spray packed-material/vertex-count semantics; canonical material/face/seam assertions were retained while supplemental spray is ignored, and the reverted flat-top experiment test was removed.
- `33493012028` on `7ad62713889c099c951da078c35c3d6fc60ac6fd`: planner + 29 regressions green; Rendering improved to 251/253 and all Water failures were gone. Both remaining failures were `_SampleSurface` parity only: density/material/boundary matched exactly.
- Repeated-symptom minimal isolation corrected the prior diagnosis: mismatch counts 1183=`13*13*7` and 1014=`13*13*6` are the solid-centre sample layers. CPU `TransvoxelDensityJob.Execute` adds transient `AuthoritativeSolidBit = 1<<26` from authoritative centre occupancy because presentation material may extend onto nearby air-centred samples; the GPU port omitted that bit. Fix `3a050468a44fd2cc9236f09bfd92010d55910c1d` ports the same occupancy bit on every GPU `SampleField` exit and removes the falsified air-canonicalization attempt.
- Master reconciliation history is normal/non-force: PR #203 -> `1c1dc14b17f09d412d785a91bbf433f5b8e4ffd4`, PR #204 -> `52b077e351286a15aa20787e3c10fe9a85d38a9f`, and current master `b274014ae201153c816c981a1092ad8b0d0a7539` -> PR #205 merge `9987006dbecee1861e3bc5c114530ef08eee9a40` before the next exact gate.

## Water validation ownership
- `WaterDemo.unity` is a thin consumer of production `WaterRenderingShowcase`.
- Water liquid-publication acceptance support lives under `Assets/VoxelEngine/Rendering/Validation/Water` with a module-local validation asmdef.
- The probe preserves its Unity GUID and reads public read-only `VoxelRenderBridge.SurfaceMetrics`; no parallel renderer/diagnostics authority.
- Independent direct-`Tests/*.asmdef` discovery is proven by the Character-style consumer fixture without modifying Character production.

## Remaining gates
- [x] Reconcile architecture/docs/test ownership and repeated-failure root causes.
- [x] Reconcile current master before the next exact-head validation.
- [ ] Run exact-current-head automatic focused tests, Water built-player validation, and mandatory Kentridge built-player validation using only `ci-test/fixes/agent-8`.
- [ ] Inspect every retained Water post-readiness standalone frame and reject pre-readiness evidence; only production-quality passes.
- [ ] Measure final runtime/cost and review all 18 acceptance criteria.
- [ ] After green exact-SHA proof, set issue fixed/resolved, move `open` -> `closed`, reconcile master again if advanced, revalidate if materially changed, and promote the exact feature head to `origin/master` non-force.
