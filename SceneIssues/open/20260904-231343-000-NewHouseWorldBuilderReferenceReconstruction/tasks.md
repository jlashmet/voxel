# New House WorldBuilder Tasks

## 0. Required ownership and validation path (discovered)
- [x] Runtime/player-visible ownership is `Assets/Game/WorldBuilder`; reuse the existing Structures/Voxel APIs without modifying those runtime modules unless a demonstrated acceptance blocker requires it.
- [x] Module-local validation path is `Assets/Game/WorldBuilder/Validation/NewHouseReferenceReconstruction/NewHouseReferenceReconstruction.unity`, exercising the real production WorldBuilder composition/catalogue path and producing built-player/reference-render evidence.
- [x] Preserve every imported generic checklist item while reconciling it to the supplied reference: garage/driveway-specific items that are absent from the supplied house reference are completed explicitly as `N/A — absent from supplied reference`, rather than by adding non-reference geometry.
- [x] Discovered regression: adding stable house material IDs requires extending `GameMaterialOwnershipTests` frozen-ID/count assertions; exact-SHA run `33944812062` exposed this and the feature branch now covers IDs 23-28 / count 29.
- [x] Discovered validation blocker from exact-SHA run `33945621865` was the unrelated URP `DebugManager` legacy-Input PlayMode path in `TypedStructuralSocketCompositionSceneTests`; later exact-SHA run `33951274739` passed the repository-derived automatic module validation, so this is not an outstanding feature-product failure.
- [x] Discovered visual defect from exact-SHA run `33951274739`: the focused house validation called `ShowcaseWorld.StepStreaming` every frame, admitting unrelated showcase castle construction and evicting/rebuilding the fixed evidence footprint as audit camera positions changed. The validation now retains its deterministically preloaded production storage snapshot and the player scenario forbids `Castle authoring exceeded`.
- [x] Re-run exact-SHA targeted CI after validation-residency isolation: run `33952976056` passed automatic module validation and standalone replay and no longer logged the castle failure, but direct inspection still showed terrain only rather than the authored house.
- [x] Discovered material-surface blocker from run `33952976056`: the validation used the legacy four-argument `ShowcaseWorld` constructor, whose private world palette registers only historical showcase materials and omits stable house IDs 23-28. Raw house voxels therefore passed authoring/readback while the renderer-bound world palette could not classify their surfaces. The validation now uses the production game-material constructor with `GameMaterialComposition.SimulationDefinitions()` / `ShowcaseMaterials`; unrelated settlement catalogue content is suppressed for this focused proof.
- [x] Re-run exact-SHA targeted CI after complete game-material world binding: the automated gates passed, but built-player evidence still showed terrain rather than the authored cottage; the application composition root was then corrected to publish the completed bounded Structures authoring phase before renderer binding.
- [x] Re-run exact-SHA targeted CI after the publication-boundary correction: run `33954740928` passed automatic module validation and standalone replay, but direct hero/audit inspection still showed fractured terrain and no recognizable reference house.
- [x] Repeated-symptom root-cause gate: exact-SHA diagnostic run `33960811414` isolated the missing house to the default GPU surface path even though the focused world reported complete published coverage.
- [x] GPU-prerequisite hypothesis resolved: repository inspection proved `VOXEL_DISABLE_GPU_CUTOVER=1` is the production CPU emergency/A-B path already used by module-player validation, not a test-only substitute. The focused scene sets it before rendering and exact runs now produce stable house geometry; this feature does not depend on the separate GPU-restoration SceneIssue.
- [x] Direct built-player comparison after geometry recovery falsified the earlier oversized/arched interpretation; the house was reduced to the compact reference proportions with rectangular entry/lower windows, four-leaf upper bank, smaller high gable window, chimney/finials, flowers/ivy, and wider portrait framing.
- [x] Exact run `33994976147` exposed wrong supplied texture-role ordering; the additional layers were remapped by visual role, and Rendering `TextureLayers` proof was scoped to its own readiness assertion rather than unrelated water readiness.
- [x] Exact runs `33996415142` and `33998165969` passed all automated gates but direct inspection showed the intended blue-painted shutter/window plate as charcoal. `HouseDoor` was confirmed to sample the correct supplied layer; its brown Albedo multiplier was the demonstrated cause. The production row now uses neutral Albedo and `PaintedHouseAccent_PreservesAuthoredBlueChroma` locks the invariant.

## 1. Engine / repository alignment
- [x] Locate the existing WorldBuilder composition entry point.
- [x] Confirm the normal world/scene registration and loading path.
- [x] Identify the current material registry and material-ID conventions.
- [x] Identify the existing texture registration/loading path.
- [x] Confirm UV/repeat behavior and material projection support.
- [x] Confirm world units, axes, origin conventions, and camera orientation.
- [x] Identify reusable geometry/material helpers already in the repository.
- [x] Record which existing APIs/helpers will be reused before adding new infrastructure.

## 2. Supplied material and texture setup
- [x] Inventory all supplied house texture assets.
- [x] Map supplied textures to plaster, timber, roof, stone, painted accent, and foliage/site roles visible in the reference.
- [x] Reuse existing material IDs where appropriate (`Glass`, `Grass`, `FlowerWhite`); use the supplied `HouseDoor` layer for the blue-painted architectural accent while the plain central entry uses `HouseTimber`.
- [x] Register missing siding/stucco material (`HousePlaster`).
- [x] Register missing trim/fascia/soffit material (`HouseTimber`).
- [x] Register missing roofing material (`HouseRoof`).
- [x] Register missing glass/window material(s), if required — reused canonical `Glass`; no extra glass identity required.
- [x] Register missing supplied painted-detail material (`HouseDoor`); garage-door role is N/A because no garage exists in the supplied reference.
- [x] Register missing masonry/concrete/site material(s), if required — `HouseStone` plus canonical grass/path roles.
- [x] Set explicit texture repeat scales and default face/triplanar projection by game-owned material row.
- [ ] Verify every required material renders successfully in the final built-player minimal/module validation view.

## 3. House composition scaffold
- [x] Add a dedicated reusable WorldBuilder house authoring composition.
- [x] Define a shared house origin.
- [x] Define foundation/slab elevation and dimensions.
- [x] Define first-floor elevation and wall height.
- [x] Define second-floor elevation and wall height.
- [x] Define wall thickness.
- [x] Define garage elevation and major dimensions — N/A, absent from supplied reference.
- [x] Define roof/eave datums used by the roof helpers.
- [x] Keep major architectural dimensions centralized and easy to tune in `NewHouseReferenceConfig`.

## 4. Primary massing
- [x] Build the foundation/slab.
- [x] Build the first-floor primary mass.
- [x] Build the second-floor primary mass.
- [x] Build the garage mass — N/A, absent from supplied reference.
- [x] Add major projections and recesses visible in the reference.
- [x] Add porch/entry mass where it affects silhouette.
- [ ] Produce a final target-camera built-player render through the owned validation scene.
- [ ] Correct/accept overall width/depth from direct built-player/reference comparison.
- [ ] Correct/accept story heights and setbacks from direct built-player/reference comparison.
- [ ] Confirm the primary silhouette matches the reference closely enough to accept.

## 5. Roof system
- [x] Build the primary roof volume(s).
- [x] Add secondary front/cross-roof forms visible in the reference.
- [x] Add garage roof volume(s) — N/A, absent from supplied reference.
- [x] Add porch/entry roof volume(s) visible in the reference.
- [x] Author ridge directions from the reference composition.
- [x] Author steep roof pitches/rises from centralized datums.
- [x] Author eave heights and overhangs.
- [x] Resolve authored intersections between primary/front/cross-roof volumes without a parallel mesh path.
- [x] Add visible fascia/edge timber.
- [x] Add visible soffit/eave depth.
- [ ] Check roof/wall intersections for visible gaps in built-player evidence.
- [ ] Check for coplanar roof overlap/z-fighting in built-player evidence.
- [ ] Render and compare the roofline against the supplied reference.

## 6. Doors and windows
- [x] Add garage-door opening(s) — N/A, absent from supplied reference.
- [x] Add the compact central entry opening and plain timber door shown by the reference.
- [x] Add the largest/front-facing windows.
- [x] Add the upper blue shutter/window bank and small high gable window visible from the target camera.
- [x] Author window sill heights from shared floor datums.
- [x] Author window head heights from shared floor datums.
- [x] Author horizontal spacing/alignment from shared house dimensions.
- [x] Add visible recess depth for openings using production carve + inset geometry.
- [x] Add window frames/sills/headers where visible.
- [x] Add door and window trim where visible.
- [x] Use parameterized rectangular window/door helpers for repeated front opening roles.
- [x] Use a parameterized small arched-window helper for the high gable opening.
- [ ] Render and compare opening placement against the reference.

## 7. Architectural detail
- [x] Add exterior timber trim/corner boards.
- [x] Add porch/entry posts and heavy timber supports where they contribute to the composition.
- [x] Add entry steps.
- [x] Add railings/fence-like site accents where visible in the reference composition.
- [x] Add prominent window/door surrounds.
- [x] Add masonry/stone foundation and chimney accents.
- [x] Add gutters/downspouts if they materially affect the target render — intentionally deferred; they do not define the supplied voxel reference at target scale.
- [x] Add vents or other high-contrast façade/roof details visible in the reference — no additional vent is required at target scale; chimney/timber breaks carry the high-contrast roof detail.
- [x] Add other silhouette/depth-critical details: chimney, ridge ornaments, flower boxes, shutters, ivy/foliage.
- [x] Defer detail that does not survive the intended render scale.

## 8. Immediate site composition
- [x] Keep site construction separate from the reusable house builder (`AuthorReferenceSite`).
- [x] Add driveway geometry — N/A, absent from supplied reference.
- [x] Add curved/stepped entry walkway.
- [x] Add porch/entry step/pad geometry.
- [x] Add lawn/ground surface through the normal generated world/site material.
- [x] Add grading/step transitions needed to meet the house.
- [x] Add simple landscaping/planting masses needed for the reference composition.
- [ ] Check the house/site boundary for gaps or floating geometry in built-player evidence.

## 9. Final material assignment
- [x] Assign plaster/exterior wall material to the intended house elements.
- [x] Assign roofing material to authored roof volumes.
- [x] Assign timber material to trim/fascia/soffit/support elements and the plain central entry.
- [x] Assign `HouseDoor` supplied layer to the blue-painted shutter/window accent; garage-door material is N/A because the reference has no garage.
- [x] Assign canonical glass material to windows.
- [x] Assign `HouseStone` to foundation/chimney/masonry elements.
- [x] Assign canonical path/ground/foliage roles to the immediate site.
- [x] Configure plaster/stone/foliage as triplanar and directional timber/roof/painted detail as face projection.
- [ ] Verify roof texture orientation reads correctly along visible slopes in built-player evidence.
- [ ] Verify wood/masonry direction reads correctly in built-player evidence.
- [ ] Tune/accept texture repeat and texel density from built-player evidence.
- [ ] Check material seams at corners and transitions.
- [ ] Check for duplicate/coplanar material surfaces that can flicker.

## 10. Reference camera and lighting
- [x] Add a reference-comparison camera/view separate from reusable house geometry.
- [x] Author a frontal portrait primary camera consistent with the supplied reference, plus fixed front-left and rear-right audit views.
- [x] Author camera height independently of house geometry.
- [x] Author downward/upward viewing angle independently of house geometry.
- [x] Author perspective/FOV (`36` degrees) independently of reusable house geometry.
- [x] Author framing/crop target independently of reusable house geometry.
- [x] Use the project's normal sky/environment/rendering path.
- [x] Author a warm directional sun/key-light direction consistent with the reference.
- [x] Author trilight ambient/fill so façade depth remains readable.
- [x] Keep camera and light configuration outside the reusable house builder.
- [ ] Accept/tune camera and lighting only after final direct built-player/reference comparison.

## 11. Visual validation and iteration
- [ ] Compare overall silhouette to the supplied reference.
- [ ] Compare total width/depth/story proportions.
- [ ] Compare roofline, pitches, ridges, eaves, and intersections.
- [x] Compare garage requirement to supplied reference — N/A, supplied reference contains no garage.
- [ ] Compare door and window placement.
- [ ] Compare major trim/details and their depth.
- [ ] Compare material identity and contrast.
- [ ] Compare texture scale and orientation.
- [ ] Compare final lighting/readability.
- [ ] Inspect side/rear/debug angles for holes.
- [ ] Inspect for overlapping geometry.
- [ ] Inspect for reversed/missing faces.
- [ ] Inspect for floating elements.
- [ ] Inspect for z-fighting/coplanar surfaces.
- [ ] Inspect for incorrect material assignments.
- [ ] Fix structural/proportion issues before cosmetic issues.

## 12. Integration and cleanup
- [x] Expose the house through the normal WorldBuilder production authoring + validation scene invocation path.
- [x] Ensure all new textures/materials use the existing asset/material pipeline.
- [x] Remove temporary/debug geometry from the reusable authoring path.
- [x] Remove temporary/debug materials; only stable game material rows remain.
- [x] Refactor repeated architectural elements into reusable helpers.
- [x] Keep site composition separable from house geometry.
- [x] Keep reference camera/lighting separable from house geometry.
- [x] Add concise comments/documentation for non-obvious composition/renderer-layer ownership.
- [ ] Run and reconcile normal exact-SHA build/test/module validation gates for the final feature SHA.
- [ ] Produce and inspect the final built-player reference-comparison render.

## Acceptance checklist
- [ ] The new house composition loads through the normal project path without errors in the final built-player proof.
- [ ] Every required supplied texture/material resolves through the existing material system in built-player evidence.
- [ ] The target render matches the supplied reference's major proportions and silhouette.
- [ ] Roof pitches, ridges, eaves, and visible roof intersections are represented correctly.
- [ ] Major doors and windows are correctly placed and proportioned.
- [ ] High-value architectural details have appropriate geometry/depth.
- [ ] Texture orientation and repeat scale are believable and consistent.
- [ ] No major gaps, floating elements, z-fighting, or obvious geometry overlaps remain.
- [x] Repeated architectural components use reusable helpers instead of unnecessary duplication.
- [x] House geometry is reusable independently of the reference-specific camera, lighting, and immediate site; translation-invariance/site-separation regressions prove the boundary.
