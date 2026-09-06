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
- [ ] Provide an explicit empty/loading/error state without substitute geometry. Reopened: run `34003328146` initial capture reports READY before the anvil surface is visible; distinguish authored data from published presentation.

## Production-faithful preview
- [x] Instantiate/author selected entries through the existing production decoration/structure/world-object realization path, not a showcase-only renderer. Rendered finish is separately unaccepted below.
- [ ] Preserve production materials, coatings/presentation semantics, geometry backend and world-object behavior across representatives; the diagnostic mode is fixed, but non-voxel and emissive finish remains unaccepted.
- [x] Fix the demonstrated diagnostic material-mode defect with `Color.white` and a real lifecycle regression. Run `34003328146` reports the four Showcase PlayMode cases passed and frame 010 shows material shading. This is not a green overall required run.
- [x] Add a neutral production-compatible preview environment with a floor/contact reference and lighting. Its final presentation quality still requires acceptance.
- [x] Compute bounds and automatically frame representative sizes without per-prop captured coordinates.
- [ ] Prove presenter geometry, floor/support and lighting remain on an independent world-space root in final exact-source built evidence.
- [ ] Prove Merchant Sign's semantic-front visibility and support-behind relationship in final accepted evidence; current visibility is correct but its featureless surface fails finish.
- [ ] Prove elevated floor and underside ceiling three-quarter framing with final built evidence.
- [ ] Correctly handle representative floor, wall, ceiling/hanging, thin-surface, box-assembly, procedural-mesh, voxel-stamp, emissive/light-producing, container, movable and other previewable cases.
- [ ] Ensure selection retirement leaves no stale geometry, colliders, lights, particles, world-object state, subscriptions or presentation resources.
- [ ] Execute the frame-separated repeated-selection regression and prove actual resource stability, not just cleared presenter dictionaries.

## Demonstrated visual defects: run 34003328146
- [ ] Trace and fix Merchant Sign's featureless rectangle through production thin-surface/material semantics; do not fabricate preview-only sign art. Require fresh built evidence at the production-quality bar.
- [ ] Trace and fix plain box-like Door/Trapdoor presentation, including Trapdoor's upright realization, through production kind/geometry/mount routing. Preserve shared APIs and add focused regressions for the proven cause.
- [x] Isolate Trapdoor's upright mount: closed production plan rotation is zero; the neutral size query and realizer select door-style dimensions and wall normal. Correct the baseline to a horizontal hatch and `+Y` floor normal without changing existing open/close behavior or explicit authored world placements. Production proxy art is still unfinished.
- [x] Add six behavioral mount cases through canonical query/authoring/realization/action/planner paths, and update Structures-owned PropShowcaseProductionValidation to render the real hatch and assert horizontal renderer bounds. Added, not yet executed.
- [ ] Execute mount regressions and inspect fresh exact-source standalone hatch evidence; the queued source predates this correction.
- [ ] Trace Forge Hearth's disconnected bars and missing finished emissive presentation through the real production realization; fix the demonstrated cause and verify fresh output.
- [ ] Review initial publication/loading and obtain captures of tiny, large, ceiling and procedural representatives that the ten-second SceneIssue replay did not adequately capture.

## Module-local validation
- [ ] Verify all affected runtime owners' local scenes/scenarios execute successfully; top-level and parent-owned scenes do not substitute for ownership.
- [x] Add SceneRuntime-owned `PropShowcaseMaterialValidation.*` using the real browser; retain unrelated consumers.
- [x] Add Materials-owned `Tests/EditMode/PropMaterialCompositionTests.cs`, its test assembly, and `Validation/PropMaterialCompositionValidation.*`. The 43-second scene invokes the real production browser; tests check canonical scalar material response, cache reuse and rejection of unknown IDs. Execution is pending, not passed.
- [x] Exercise real production enumeration/realization in local scene setup, not reconstructed content.
- [x] Use local player-scenario files only for runtime behavior, captures and assertions; no manual target registration.
- [x] Add focused catalogue/enumeration unit coverage.

## Required-CI teardown blocker
- [x] Isolate failure ordering before retrying: run `34000107687` overlaps PlayMode cleanup; run `34003328146` independently starts its second PlayMode phase while still in play mode and fails SaveModifiedSceneTask. See exact review records.
- [x] Isolate PlayMode module phases and explicit PlayMode filters using existing Unity-wrapper processes, retaining every selected test/player gate (`79b6a2f4`).
- [x] Add/run Python subprocess regressions: prior 20 focused tests passed; original baseline failed 14 assertions/subtests. These are not Unity acceptance.
- [ ] Obtain successful exact-SHA execution including the repaired orchestration and latest feature head.

## Resource-evidence repair
- [x] Replace one-frame stress with three repeated frame-separated production-browser cycles; record startup/switch time, actual components, global native mesh/material counts and separate allocator domains.
- [x] Add nonvisual resource-accounting regressions and require all three cycle records in the owned scenario. Tests are added, not yet run in Unity.
- [ ] Execute resource regressions and review same-endpoint measurements, including unavailable counters. Three process-wide samples are not the device-matrix two-hour world-memory gate.

## Built-player acceptance
- [ ] Build/run the latest exact feature SHA through the assigned CI transport; never replace queued/running work.
- [ ] Capture durable initial left-catalogue/right-preview evidence with a truthful presentation-ready/loading state.
- [ ] Capture tiny/medium/large and every required mount/backend representative.
- [ ] Inspect silhouettes, grounding/contact, material fidelity, framing, clipping, lighting and finish; only production-quality evidence passes. Latest review is prototype/blockout quality, rejected.
- [ ] Exercise repeated navigation without startup/runtime exceptions, stale previews, accumulation or unusable UI states.
- [ ] Measure startup/switching/resource cost against repository budgets; the old 44 switches and peakOwned=1 do not establish memory or frame-time budgets.

## Acceptance checklist
- [ ] Dedicated built scene with left catalogue and right live preview.
- [ ] Every in-scope prop/decoration represented without a drifting identity registry.
- [ ] Clicking any entry renders its corresponding production realization.
- [ ] Switching removes the prior realization cleanly.
- [ ] Framing, grounding, materials and lighting are useful across shapes/sizes/backends.
- [ ] Catalogue-parity and switching regressions pass.
- [ ] Required module-local scenes/scenarios pass through production paths.
- [ ] Durable exact built evidence is reviewed and passes production-quality acceptance.
- [ ] Every required exact-SHA gate passes.
- [ ] Complete `issue.json` resolutionSummary/regressionTest/fixCommit and all required work before open-to-closed bookkeeping.
- [ ] Merge current master, enable final PR auto-merge, pass required `affected`, and verify the closed assignment on master.

## Current state
Request `e83a7fd822dab1c40d59f0f84ccd65937071fd28` / run `34003328146` completed FAILURE at 2026-09-06T02:37:23Z without replacement. It tested `de0aa1fb`, not the later CI isolation/resource revisions. Artifact `9981244713` was downloaded and visually reviewed: material mode is corrected, overall finish is rejected. Request `57ab96ca508e70a4d768aa5ddefc6b7343bb531c` / run `34007356710` was already queued for source `9697d365` on rechecking the remote CI ref; it remains untouched. This revision's trapdoor correction is not in that request. An unreferenced candidate `e23d0cf4` was not submitted. No closure or PR promotion is justified before the mandatory remaining implementation, visual, resource and exact-source gates pass.
