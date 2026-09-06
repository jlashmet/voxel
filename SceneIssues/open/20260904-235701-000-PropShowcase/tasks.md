# PropShowcase tasks

## Discovery and ownership
- [x] Fetch current `origin/master`, inspect the relevant production catalogues/presets/realization code, and update `plan.md` with the actual affected modules and current feature SHA.
- [x] Inventory every independently previewable production prop/decoration source, including base decoration content, registered decoration expansions, reusable room/furniture/lighting/storage/dining/martial/textile presets, and production world-object props that belong in this showcase.
- [x] Define the exact in-scope entry set and document intentional exclusions/aliases/variants so "all available props/decorations" has a deterministic meaning.
- [x] Verify whether the existing production APIs can enumerate the complete set without duplicated IDs or switch knowledge in the scene.
- [x] If enumeration is incomplete, add the narrowest read-only semantic enumeration/query boundary to the owning production module; do not create a showcase-only content registry.
- [x] Identify every affected module root and its existing module-local validation scene(s); add required validation-scene work below for any player-visible/runtime module that lacks a suitable focused surface.
- [x] Add the missing reusable production consumer for `DecorationProceduralMeshRequest`; it must live in Structures, preserve canonical request/material semantics, and be usable by validation/shipped consumers rather than existing only for `PropShowcase`.

## Catalogue browser model
- [x] Implement a deterministic showcase-facing read model/adapter derived from canonical production sources, with stable identity, friendly display label, category/grouping metadata where available, and enough semantic information to invoke the real production realization.
- [x] Ensure every in-scope canonical entry appears exactly once unless an intentionally separate variant is documented.
- [x] Add focused regression coverage proving catalogue parity/completeness and detecting duplicate or orphaned showcase entries.
- [x] Prove that adding a representative canonical entry through the supported production registration path does not require adding a second duplicated identity constant/list to `PropShowcase`.

## PropShowcase scene and UI
- [x] Create `Assets/Scenes/PropShowcase.unity` as a dedicated integration/showcase scene and register it through the repository's normal scene/build path.
- [x] Build a readable left-side panel that can handle hundreds of entries, supports scrolling, shows each friendly label, and clearly marks the current selection.
- [x] Keep the main right-side area dedicated to the selected prop/decoration preview rather than rendering the whole catalogue simultaneously.
- [x] Make clicking an entry replace the active preview immediately and deterministically.
- [x] Provide an explicit empty/loading/error state that is diagnostic without substituting placeholder geometry for a production prop.

## Production-faithful preview
- [x] Instantiate/author the selected entry through the same production decoration/structure/world-object realization path used by shipped content; do not use `GameObject.CreatePrimitive`, bespoke preview meshes, ad-hoc materials/shaders, or fake substitute props where a production realization exists.
- [ ] Preserve the selected content's production materials, coatings/presentation semantics, geometry backend, and applicable world-object presentation behavior. Reopened: exact run `34000107687` shows normal-coverage colours instead of voxel materials.
- [ ] Fix the demonstrated material-mode defect by selecting production (`Color.white`) surface shading in the showcase environment, add a behavioural regression through the real enable/selection lifecycle, and verify fresh built-player material output without changing the production shader or catalogue. Implementation exists at source `de0aa1fb`; exact validation is outstanding.
- [x] Add a neutral but production-compatible preview environment with stable floor/contact reference and lighting that makes material and silhouette differences readable.
- [x] Compute preview bounds from the realized content and automatically frame/position the camera so representative tiny, medium, and large entries are visible without hand-authored per-prop coordinates.
- [ ] Keep presenter-owned geometry, floor/support references, and preview lighting on a world-space presentation root independent of the framing camera transform; prove the final framing/grounding relationship in built-player evidence.
- [ ] Fix the demonstrated wall-mounted thin-surface visibility defect: `Merchant Sign` must be visibly rendered from its semantic front with the support surface behind it, and fresh exact-SHA built-player evidence must prove the relationship.
- [ ] Fix the demonstrated vertical-mount camera defect: floor-mounted props must use a useful elevated three-quarter view instead of looking down the `+Y` mount normal, and ceiling-mounted props must use a useful underside three-quarter view instead of looking straight up the `-Y` mount normal; prove both with fresh exact-SHA built-player evidence.
- [ ] Correctly handle representative floor-mounted, wall-mounted, ceiling/hanging, thin-surface, box-assembly, procedural-mesh, voxel-stamp, emissive/light-producing, container, movable, and other independently previewable production cases.
- [ ] Ensure switching entries fully disposes/recycles the prior preview realization and leaves no stale geometry, colliders, lights, particle emitters, world-object state, subscriptions, or presentation resources.
- [ ] Add a repeated-selection stress regression that cycles through a representative set and proves stable active-object/resource counts and no exceptions. Reopened: the old one-frame loop only checked presenter dictionaries. Frame-separated cycles and actual resource probes are now implemented; Unity execution is pending.

## Module-local validation
- [ ] For each affected player-visible/runtime module, create or update a focused scene under that module's own `<Module>/Validation/` directory; do not count top-level `PropShowcase` as the module-local validation surface. Verify every affected owner, including material composition.
- [x] Add a focused PropShowcase production-consumer scene and scenario under `Assets/Game/Composition/Showcase/SceneRuntime/Validation/`, covering production material mode and representative selections; retain existing unrelated validation consumers. Added `PropShowcaseMaterialValidation.*` at `de0aa1fb`.
- [x] Exercise the real production catalogue enumeration and realization path in those module-local validation scenes.
- [x] Add module-local `*.player-scenario.json` only where runtime selection/capture/assertion behavior is needed; do not add manual registration metadata.
- [x] Add focused EditMode/unit coverage for pure catalogue/enumeration invariants where appropriate.

## Required-CI teardown blocker
- [x] Inspect the failed artifact and isolate the failure ordering before another retry. Run `34000107687` starts the next phase before the preceding PlayMode `IPostBuildCleanup`/scene restoration; product case results had passed.
- [x] Isolate PlayMode module phases and explicitly requested PlayMode tests in fresh existing Unity-wrapper processes, retaining persistent EditMode batching and every required selected test/player gate. Implemented at `79b6a2f4261185680ecbeceff7797f71992d35ab`.
- [x] Add and run subprocess behavioural regressions for two PlayMode phases followed by a focused request; reject zero-match, skipped, failed and missing test results without a hard-coded requested-test assembly. All 20 focused Python tests pass; baseline fails 14 assertions/subtests. See `ci-teardown-repro.md`.
- [ ] Obtain successful exact-SHA execution after the orchestration repair; local Python regressions do not substitute for Unity or built-player evidence.

## Resource-evidence repair
- [x] Replace the one-frame stress burst with three repeated, frame-separated cycles through the real browser; wait for endpoint cleanup and record startup/selection cost, actual owned components, global native mesh/material counts and separate allocator totals. Do not manufacture a memory pass from presenter counts.
- [x] Add focused nonvisual regressions for inactive/deferred-owned objects, unparented native mesh accounting, and a missing owner; require all three cycle records in the module-local scenario. These tests are added, not yet executed in Unity.
- [ ] Execute the new resource regressions and exact built-player scenario; review same-endpoint measurements and any unavailable profiler counters before accepting lifecycle or cost. Short process-wide probes do not establish the device-matrix two-hour world-memory gate.

## Built-player acceptance
- [ ] Build/run the exact feature SHA through the required targeted-CI transport and do not replace a queued/running request.
- [ ] Capture durable built-player evidence of the initial `PropShowcase` view showing the left catalogue and right preview layout.
- [ ] Capture representative selections spanning small/medium/large props and at least the major realization/mount categories used by the catalogue.
- [ ] Visually inspect the built-player evidence for readable silhouettes, grounding/contact, material fidelity, framing, clipping, lighting, and absence of placeholder/blockout presentation; only production-quality evidence passes.
- [ ] Exercise repeated navigation through many entries in the built player and verify there are no startup/runtime exceptions, stale previews, accumulating objects, or unusable UI states.
- [ ] Measure relevant startup/switching/resource cost and confirm the showcase does not introduce an unreasonable runtime or memory-growth regression. Prior replay reports 44 switches and peakOwned=1, but presenter counts and the empty `fps.txt` do not establish memory or frame-time budgets.

## Acceptance checklist
- [ ] `PropShowcase` is a dedicated built scene with a left catalogue panel and right live preview.
- [ ] Every in-scope production prop/decoration is represented by the catalogue browser without a drifting duplicate identity list.
- [ ] Clicking any listed entry renders the corresponding production realization in the right panel.
- [ ] Switching selection removes the previous preview cleanly.
- [ ] Preview framing, grounding, materials, and lighting are useful across representative content shapes/sizes/backends.
- [ ] Automated catalogue-parity and switching regressions pass.
- [ ] Required module-local validation scenes and scenarios pass through production paths.
- [ ] Durable exact built-player evidence has been visually reviewed at the repository's production-quality bar.
- [ ] All required exact-SHA targeted CI gates pass.
- [ ] `issue.json` has final `resolutionSummary`, `regressionTest`, and `fixCommit`; every required checkbox is complete before moving `open/` to `closed/`.
- [ ] Current `origin/master` is merged into the feature branch before final promotion; PR auto-merge is enabled and the required `affected` gate passes until the SceneIssue is visible closed on master.

## Current blockers
Request `e83a7fd822dab1c40d59f0f84ccd65937071fd28` / run `34003328146` remains queued for exact source `de0aa1fb4221b06f8f63e6f22fc26ffba77defc8`; leave it untouched. The preceding run's automatic module validation failed during Test Runner teardown, and its diagnostic-normal material output failed visual acceptance. The independent CI isolation repair and later resource-evidence work are not part of the queued source and need fresh exact-SHA CI only after the existing request completes. All Unity/player, visual, resource and final promotion gates remain mandatory.
