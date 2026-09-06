# PropShowcase tasks

## Discovery and ownership
- [x] Fetch current `origin/master`, inspect production catalogues/presets/realization code, and identify affected modules.
- [x] Inventory every independently previewable production source and define the deterministic 529-entry scope/exclusions.
- [x] Expose the narrowest read-only enumeration/query boundaries needed; keep content identity out of the showcase.
- [x] Add the missing reusable production consumer for procedural-decoration requests.
- [x] Identify module-local validation ownership for Structures, SceneRuntime, and Materials.

## Catalogue browser model
- [x] Implement a deterministic showcase read model derived from canonical sources with stable identity/display metadata.
- [x] Represent every in-scope canonical entry exactly once and regression-test parity/duplicates/orphans.
- [x] Prove representative production registration does not require a second showcase identity list.

## PropShowcase scene and UI
- [x] Create/register dedicated `Assets/Scenes/PropShowcase.unity`.
- [x] Provide a readable scrollable left catalogue and dedicated right live preview; selection replaces prior content.
- [x] Provide explicit diagnostic error state without substitute geometry.
- [x] Implement truthful voxel publication state: voxel-backed selections remain `LOADING` until production surface publication is complete, then become `READY`. Behavioral and player validation are pending.

## Production-faithful preview
- [x] Instantiate/author selected entries through existing production realization/presentation paths.
- [x] Fix diagnostic normal shading by selecting production `Color.white`; run `34003328146` visually confirmed material shading.
- [x] Keep preview floor/support/lights and presenter geometry on a world-space root independent of camera framing; final built proof remains pending.
- [x] Compute bounds-based framing with semantic-front wall views plus floor/ceiling three-quarter views.
- [x] Add frame-separated repeated-selection/resource instrumentation instead of one-frame presenter-count proof.
- [ ] Preserve production materials/coatings/backends/world-object behavior across all representative captures; final evidence is pending.
- [ ] Prove selection retirement leaves no stale geometry/colliders/lights/particles/world-object state/resources in exact player execution.

## Demonstrated visual defects
- [ ] Merchant Sign: shared production thin-surface presenter now adds raised painting-family frame/emblem detail at `2b7e30e1`; require fresh exact built evidence showing finished semantic-front presentation rather than a flat rectangle.
- [ ] Door/Trapdoor: Trapdoor mount is horizontal (`849875e3`) and shared `UnityWorldObjectPresentationSink` now uses detailed generated Door/SecretDoor/Trapdoor panel geometry (`2b7e30e1`); require fresh built evidence and preserve open/close semantics.
- [ ] Forge Hearth: shared `DecorationEffectPresenter` now consumes existing semantic light/particle hooks (`2b7e30e1`); inspect fresh output. If canonical voxel hearth geometry remains disconnected/blockout quality, fix that demonstrated production-authoring cause before acceptance.
- [ ] Initial publication: exact evidence must show truthful `LOADING` before voxel surface publication and a later `READY` transition with visible geometry.
- [ ] Capture/review tiny, medium, large, wall, floor, ceiling/hanging, thin, box, procedural, voxel-stamp, emissive, container/movable and interactive representatives at production-quality bar.

## Module-local validation
- [x] Structures owns `Validation/PropShowcaseProductionValidation.*`; updated to exercise framed thin detail, semantic effects, detailed Door/Trapdoor production proxies and floor mount.
- [x] SceneRuntime owns `Validation/PropShowcaseMaterialValidation.*`; scenario requires readiness, representative selections and all three resource cycles.
- [x] Materials owns `Tests/EditMode/PropMaterialCompositionTests.cs` plus `Validation/PropMaterialCompositionValidation.*` through the real browser/material adapter.
- [x] Add focused catalogue, mount, presentation-quality, readiness, material and resource regressions without manual CI registration.
- [ ] Execute every affected module's tests/scenes successfully through exact-SHA CI.

## CI failures and repairs
- [x] Run `34000107687`: isolate persistent PlayMode temporary-scene teardown overlap.
- [x] Implement per-PlayMode-process isolation (`79b6a2f4`) and pass 20 focused Python orchestration tests; baseline fails the new isolation assertions.
- [x] Run `34003328146`: inspect completed failure; 4 Showcase PlayMode cases passed, required overall run failed teardown, standalone replay passed but visual quality rejected.
- [x] Run `34007356710`: isolate compile-only failures (`NonParallelizable` unavailable; production SceneRuntime referenced validation-only stress helper). Repair at `2154840b`; no test/player evidence from that run is accepted.
- [x] Run `34011392051`: inspect timeout artifact. `Assets/Scenes/PropShowcase.unity` incorrectly triggered broad unknown-path fallback, selecting 48 modules / 52 tests / 23 players; persistent tests passed before the 20-minute limit.
- [x] Treat top-level `Assets/Scenes/*.unity` as integration-only for module ownership while retaining one canonical Kentridge gate for production diffs; preserve broad fallback for genuinely unknown production paths. Add focused planner regressions.
- [x] Prevent same-module validation scenes from overwriting one another's exact artifact logs/screenshots by keying player output to module plus scene; add focused runner regressions.
- [ ] Obtain successful exact-SHA targeted CI for the current implementation and inspect its artifact rather than inferring success from individual phases.

## Resource/cost evidence
- [x] Repeat the same sampled selection set across three frame-separated cycles and record startup/switch timing, actual owned components, global meshes/materials, allocator totals and resident geometry.
- [x] Add regressions for deferred/inactive object retirement, native mesh accounting and missing owner detection.
- [ ] Execute and review same-endpoint resource measurements. Do not call the short process-wide probe the device-matrix two-hour world-memory test.
- [ ] Confirm showcase startup/switch/resource cost is reasonable and no selection accumulation is demonstrated.

## Built-player acceptance
- [ ] Run the latest exact feature SHA through `ci-test/fixes/agent-9`; never replace queued/running work.
- [ ] Capture durable initial left-catalogue/right-preview evidence plus all required representatives.
- [ ] Directly inspect silhouettes, construction detail, grounding/contact, materials, clipping, lighting, repetition and placeholder/blockout appearance; only `production-quality` passes.
- [ ] Exercise repeated navigation without startup/runtime exceptions, stale previews or unusable UI.

## Acceptance checklist
- [ ] Dedicated built scene has usable left catalogue and right live production preview.
- [ ] All 529 in-scope entries are represented without a drifting identity registry.
- [ ] Selecting any listed entry renders its corresponding production realization.
- [ ] Switching removes prior realization/resources cleanly.
- [ ] Framing, grounding, materials, lighting and construction are useful across representative shapes/sizes/backends.
- [ ] Catalogue-parity, presentation, switching/readiness and resource regressions pass.
- [ ] Required module-local scenes/scenarios pass through production paths.
- [ ] Durable exact built evidence passes production-quality visual review.
- [ ] Every required exact-SHA targeted CI gate passes.
- [ ] Complete `issue.json` `resolutionSummary`, `regressionTest`, `fixCommit`; every required checkbox is complete before open→closed.
- [ ] Merge current `origin/master` into `fixes/agent-9`, open/update final PR, enable auto-merge, pass required `affected`, and verify closed assignment on master.

## Current state
Run `34011392051` is a failed timeout, not a product pass. Its artifact proved persistent tests passed but broad planner fallback exhausted the job on unrelated players. The planner and artifact-isolation repairs plus focused Python regressions are committed. Fresh exact-source CI is required before any visual/resource acceptance, closure or PR promotion.
