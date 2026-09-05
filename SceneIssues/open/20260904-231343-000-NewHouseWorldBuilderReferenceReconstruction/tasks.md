# New House WorldBuilder Tasks

## 0. Required ownership and validation path (discovered)
- [x] Runtime/player-visible ownership is `Assets/Game/WorldBuilder`; reuse the existing Structures/Voxel APIs without modifying those runtime modules unless a demonstrated acceptance blocker requires it.
- [x] Module-local validation path is `Assets/Game/WorldBuilder/Validation/NewHouseReferenceReconstruction/NewHouseReferenceReconstruction.unity`, exercising the real production WorldBuilder composition/catalogue path and producing built-player/reference-render evidence.
- [x] Preserve every imported generic checklist item while reconciling it to the supplied reference: garage/driveway-specific items that are absent from the supplied house reference will be completed explicitly as `N/A — absent from supplied reference`, rather than by adding non-reference geometry.

## 1. Engine / repository alignment
- [ ] Locate the existing WorldBuilder composition entry point.
- [ ] Confirm the normal world/scene registration and loading path.
- [ ] Identify the current material registry and material-ID conventions.
- [ ] Identify the existing texture registration/loading path.
- [ ] Confirm UV/repeat behavior and per-face material assignment support.
- [ ] Confirm world units, axes, origin conventions, and camera orientation.
- [ ] Identify reusable geometry/material helpers already in the repository.
- [ ] Record which existing APIs/helpers will be reused before adding new infrastructure.

## 2. Supplied material and texture setup
- [ ] Inventory all supplied house texture assets.
- [ ] Map each supplied texture to the corresponding surface in the reference image.
- [ ] Reuse existing material IDs where appropriate.
- [ ] Register missing siding/stucco material(s), if required.
- [ ] Register missing trim/fascia/soffit material(s), if required.
- [ ] Register missing roofing material(s), if required.
- [ ] Register missing glass/window material(s), if required.
- [ ] Register missing door/garage-door material(s), if required.
- [ ] Register missing masonry/concrete/site material(s), if required.
- [ ] Set believable texture repeat scales and default orientation.
- [ ] Verify every required material renders successfully in a minimal test/view.

## 3. House composition scaffold
- [ ] Add a dedicated WorldBuilder composition/module for the new house.
- [ ] Define a shared house origin.
- [ ] Define foundation/slab elevation and dimensions.
- [ ] Define first-floor elevation and wall height.
- [ ] Define second-floor elevation and wall height.
- [ ] Define wall thickness.
- [ ] Define garage elevation and major dimensions.
- [ ] Define roof/eave datums used by the roof helpers.
- [ ] Keep major architectural dimensions centralized and easy to tune.

## 4. Primary massing
- [ ] Build the foundation/slab.
- [ ] Build the first-floor primary mass.
- [ ] Build the second-floor primary mass.
- [ ] Build the garage mass.
- [ ] Add major projections and recesses visible in the reference.
- [ ] Add porch/entry mass where it affects silhouette.
- [ ] Produce an early target-camera render using simple materials.
- [ ] Correct overall width/depth before adding detail.
- [ ] Correct story heights and setbacks before adding detail.
- [ ] Confirm the primary silhouette matches the reference closely enough to proceed.

## 5. Roof system
- [ ] Build the primary roof volume(s).
- [ ] Add secondary gables/hips.
- [ ] Add garage roof volume(s).
- [ ] Add porch/entry roof volume(s), if visible.
- [ ] Match ridge directions to the reference.
- [ ] Match roof pitches to the reference.
- [ ] Match eave heights and overhangs.
- [ ] Resolve valleys/intersections between roof volumes.
- [ ] Add visible fascia.
- [ ] Add visible soffits.
- [ ] Check roof/wall intersections for gaps.
- [ ] Remove coplanar overlaps and roof z-fighting.
- [ ] Render and compare the roofline before proceeding.

## 6. Doors and windows
- [ ] Add garage-door opening(s).
- [ ] Add the main entry opening and door.
- [ ] Add the largest/front-facing windows.
- [ ] Add secondary windows visible from the target camera.
- [ ] Match window sill heights.
- [ ] Match window head heights.
- [ ] Match horizontal spacing/alignment.
- [ ] Add visible recess depth for openings.
- [ ] Add window frames/sills/headers where visible.
- [ ] Add door and window trim where visible.
- [ ] Create or reuse parameterized helpers for repeated window units.
- [ ] Create or reuse parameterized helpers for repeated door units.
- [ ] Render and compare opening placement against the reference.

## 7. Architectural detail
- [ ] Add exterior trim/corner boards.
- [ ] Add porch columns/posts.
- [ ] Add entry steps.
- [ ] Add railings where visible.
- [ ] Add prominent window/door surrounds.
- [ ] Add masonry/stone accents where visible.
- [ ] Add gutters/downspouts if they materially affect the target render.
- [ ] Add vents or other high-contrast façade/roof details visible in the reference.
- [ ] Add other silhouette/depth-critical details from the reference.
- [ ] Defer detail that does not survive the intended render scale.

## 8. Immediate site composition
- [ ] Keep site construction separate from the reusable house builder.
- [ ] Add driveway geometry.
- [ ] Add entry walkway.
- [ ] Add porch/entry pad.
- [ ] Add lawn/ground plane.
- [ ] Add grading transitions where needed to meet the house cleanly.
- [ ] Add simple landscaping/planting masses needed for the reference composition.
- [ ] Check the house/site boundary for gaps or floating geometry.

## 9. Final material assignment
- [ ] Assign exterior wall/siding materials to the correct faces/elements.
- [ ] Assign roofing material to all visible roof surfaces.
- [ ] Assign trim/fascia/soffit materials.
- [ ] Assign door and garage-door materials.
- [ ] Assign window/glass materials.
- [ ] Assign masonry/concrete materials.
- [ ] Assign driveway/walkway/ground materials.
- [ ] Correct siding/stucco texture orientation.
- [ ] Correct roof texture orientation along slopes.
- [ ] Correct wood/masonry direction where applicable.
- [ ] Tune texture repeat/texel density across adjacent surfaces.
- [ ] Check material seams at corners and transitions.
- [ ] Remove duplicate/coplanar material surfaces that can flicker.

## 10. Reference camera and lighting
- [ ] Add a reference-comparison camera/view separate from reusable house geometry.
- [ ] Match camera azimuth/side angle.
- [ ] Match camera height.
- [ ] Match vertical viewing angle.
- [ ] Match perspective/FOV.
- [ ] Match framing and crop.
- [ ] Use the project's normal sky/environment path.
- [ ] Match the dominant sun/key-light direction as closely as practical.
- [ ] Tune ambient/fill so façade depth remains readable.
- [ ] Keep camera and light configuration outside the reusable house builder.

## 11. Visual validation and iteration
- [ ] Compare overall silhouette to the supplied reference.
- [ ] Compare total width/depth/story proportions.
- [ ] Compare roofline, pitches, ridges, eaves, and intersections.
- [ ] Compare garage, door, and window placement.
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
- [ ] Expose the house through the project's normal WorldBuilder/world-loading or scene invocation path.
- [ ] Ensure all new textures/materials use the existing asset/material pipeline.
- [ ] Remove temporary/debug geometry.
- [ ] Remove temporary/debug materials.
- [ ] Refactor duplicated architectural elements into reusable helpers.
- [ ] Keep site composition separable from house geometry where practical.
- [ ] Keep reference camera/lighting separable from house geometry.
- [ ] Add concise comments/documentation for any non-obvious composition/helper usage.
- [ ] Run the normal build/test/lint checks applicable to touched code.
- [ ] Produce the final reference-comparison render.

## Acceptance checklist
- [ ] The new house composition loads through the normal project path without errors.
- [ ] Every required supplied texture/material resolves through the existing material system.
- [ ] The target render matches the supplied reference's major proportions and silhouette.
- [ ] Roof pitches, ridges, eaves, and visible roof intersections are represented correctly.
- [ ] Major doors and windows are correctly placed and proportioned.
- [ ] High-value architectural details have appropriate geometry/depth.
- [ ] Texture orientation and repeat scale are believable and consistent.
- [ ] No major gaps, floating elements, z-fighting, or obvious geometry overlaps remain.
- [ ] Repeated architectural components use reusable helpers instead of unnecessary duplication.
- [ ] House geometry is reusable independently of the reference-specific camera, lighting, and immediate site.
- [ ] A final reference-comparison render exists for visual validation.
