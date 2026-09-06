# New House WorldBuilder Tasks

## Binding objective and authoritative reference
The goal is to recreate **the particular house in `Assets/Textures/Stylized/experiment1/house/10dddef5-de0a-4153-9c09-b1e8016830db.png` as closely as possible**, leveraging the production voxel engine's WorldBuilder, structures, materials, textures, authoring, meshing, rendering, and scene systems. **Keep iterating with actual built-player renders until the result is very close to this exact image and production-quality.** A plausible house in the same style, a completed object list, or passing CI is insufficient.

Reference Git blob: `6d87b08d4c7c9bddc1705c0f34343aa79bc18423`, verified on master `ef475182b866eabfe8e1d1a39c82bf7810a03f49` and feature baseline `544215794036261c0bfa8f71517e26700d4995ec`.

![Authoritative user-specified reference](../../../Assets/Textures/Stylized/experiment1/house/10dddef5-de0a-4153-9c09-b1e8016830db.png)

Optional texture source: `Assets/Textures/Stylized/experiment1/` (the directory above `house/`). Supplied textures may be used, adapted, combined, or replaced; creating original/generated textures is allowed. Choose assets for fidelity to the reference, not to satisfy an arbitrary requirement to use every supplied image. New assets must use the existing material/texture pipeline, with committed files, Unity metadata, and recorded provenance. Generated textures are assets, not substitute Unity render evidence.

## 0. Reference correction, ownership, and next required work
- [x] Pin the user-specified repository reference path and verified Git blob in both plan and tasks; distinguish it from optional texture inputs.
- [x] Invalidate the previous reference selection: Library image `3aad3fb3-7a3c-41b4-b87b-f2f72eaa6cda.png` is NOT this assignment's reference. Previous visual approvals and claims of close resemblance do not count.
- [ ] Open and inspect the actual pinned image before further geometry/material changes. Verify the checkout's image hash; do not substitute another image if retrieval fails. Obtain user confirmation before changing the authoritative reference.
- [ ] Validate `NewHouseReferenceSourceTests.ReferenceInput_MatchesPinnedBlob_AndPreservesOriginalForReview` and inspect the exact input copied into targeted CI `ReferenceInputs/NewHouse/`. The connector currently returns unsupported/empty binary content; this small provenance regression preserves the actual file without changing any visual acceptance or workflow. Reference input is not player-render evidence.
- [ ] Restore discovery of the three `NewHouseReferenceAuthoringTests` with fully qualified NUnit attributes: the namespace-local inert `TestAttribute` currently shadows them. Confirm nonzero execution of these actual behavioral cases in exact CI; leave the unrelated global quarantine alone.
- [ ] Record the correct reference's silhouette, major massing, visible elevations, roof forms, opening shapes/counts/positions, important details, material regions, and camera/framing relationships. Separate visible observations from assumptions about hidden sides.
- [ ] Compare the existing house render against that image and record the largest concrete mismatches; reassess the earlier compact dimensions, rectangular entry/lower windows, four-leaf upper bank, small high window, blue accent treatment, and framing rather than preserving them as requirements.
- [ ] Reconcile all previously reference-derived geometry/material/camera decisions and N/A claims. Garage, driveway, porch, dormer, gutter, and other generic checklist roles are conditional on the actual reference; document N/A with a reason only after inspecting it, not by assuming presence or absence.
- [x] Identify existing WorldBuilder ownership and focused scene: `Assets/Game/WorldBuilder/Validation/NewHouseReferenceReconstruction/NewHouseReferenceReconstruction.unity`.
- [x] Identify existing material ownership in `Assets/Game/Materials` and semantic-free Rendering texture-layer consumption; retain material unit tests and Rendering `TextureLayers` module-local proof.
- [ ] Update affected module-local validation scenes/scenarios and focused behavioral tests as the corrected implementation requires; scenes must exercise real production realization, not duplicate geometry/material/rendering behavior.
- [ ] Correct evidence timing so the standard standalone capture cadence actually records the target view plus front/side/rear audits, including the previously missed rear-right view.

Historical technical baseline only: prior work addressed complete game-material palette binding, bounded-authoring publication, fixed validation residency, and the supported production CPU fallback. CI run `34001554204` on feature `544215794036261c0bfa8f71517e26700d4995ec` passed mechanically, but it does not prove fidelity to the newly confirmed reference. Earlier texture-ordering and tint experiments are not final material choices. Remaining checked implementation items below record existing infrastructure only; all final visual and regression acceptance must be re-established after corrections. The documentation reset changed no product geometry/materials; subsequent source-evidence and test-discovery work likewise makes no visual acceptance claim.

## 1. Engine / repository alignment
- [x] Locate the existing WorldBuilder composition entry point.
- [x] Confirm the normal world/scene registration and loading path.
- [x] Identify the current material registry and material-ID conventions.
- [x] Identify the existing texture registration/loading path.
- [x] Confirm UV/repeat behavior and material projection support.
- [x] Confirm world units, axes, origin conventions, and camera orientation controls.
- [x] Identify reusable geometry/material helpers already in the repository.
- [x] Record existing APIs/helpers in the plan before adding infrastructure.
- [ ] Assess the corrected house's authoring, geometry, material/texture memory, and runtime cost against repository budgets; do not relax budgets or unrelated assertions to make proof pass.

## 2. Supplied material and texture setup
- [ ] Inspect candidate textures under `Assets/Textures/Stylized/experiment1/` and map useful assets to surfaces actually visible in the pinned reference; do not infer roles from filenames alone.
- [ ] Record which supplied textures are retained, adapted, or unused and where original/generated textures are needed to get closer to the reference.
- [ ] Reuse existing material IDs where appropriate; reassess earlier house-role assignments rather than treating them as final.
- [ ] Register or correct missing siding/stucco/exterior-wall materials through the existing registry, as required by the reference.
- [ ] Register or correct missing trim/fascia/soffit/timber materials, as required.
- [ ] Register or correct missing roofing materials, as required.
- [ ] Register or correct missing glass/window materials, as required.
- [ ] Register or correct door/garage-door/painted-detail materials, as required; do not require blue paint or a garage based on the discarded image.
- [ ] Register or correct masonry/concrete/site materials, as required.
- [ ] Create or adapt any needed texture assets and commit their source/provenance and Unity metadata; use normal material registration, not a one-off texture bootstrap. Record N/A if existing assets suffice.
- [ ] Set explicit, believable texture repeat scales and face/triplanar projection from the correct reference and engine scale.
- [ ] Verify every material/texture actually chosen for the reconstruction resolves and renders correctly in module-local built-player evidence. Unused supplied textures are not a failure.

## 3. House composition scaffold
- [x] Add a dedicated reusable WorldBuilder house authoring composition.
- [ ] Reassess the shared house origin and reference-relative layout.
- [ ] Reassess foundation/slab elevation and dimensions.
- [ ] Reassess first-floor elevation and wall height.
- [ ] Reassess second-floor elevation and wall height, or document its absence from the correct reference.
- [ ] Reassess wall thickness.
- [ ] Define garage elevation and major dimensions if present; otherwise document reference-grounded N/A.
- [ ] Reassess roof/eave datums used by roof helpers.
- [x] Keep major architectural dimensions centralized in configuration rather than scattered through scene code.

## 4. Primary massing
- [ ] Build/correct the foundation/slab to match the pinned reference.
- [ ] Build/correct the first-floor primary mass.
- [ ] Build/correct the second-floor primary mass as applicable.
- [ ] Build/correct the garage mass if present; otherwise document N/A.
- [ ] Match major projections and recesses visible in the reference.
- [ ] Match porch/entry mass where it affects silhouette; document N/A if absent.
- [ ] Produce an early target-camera built-player render through the owned validation scene before further detail work.
- [ ] Correct overall width/depth from direct comparison with the pinned reference.
- [ ] Correct story heights, setbacks, and roof-to-body proportions before cosmetic detail.
- [ ] Confirm the primary silhouette is very close to the reference before accepting massing.

## 5. Roof system
- [ ] Build/correct primary roof volumes to the reference.
- [ ] Match secondary gables/hips/dormers/cross-roofs that are actually present; document absent roles as N/A.
- [ ] Match garage roof volumes if present; otherwise document N/A.
- [ ] Match porch/entry roof volumes if visible; otherwise document N/A.
- [ ] Match ridge directions.
- [ ] Match roof pitches/rises.
- [ ] Match eave heights and overhangs.
- [ ] Resolve valleys/intersections between roof volumes through production authoring.
- [ ] Match visible fascia/edge trim.
- [ ] Match visible soffit/eave depth.
- [ ] Check roof/wall intersections for visible gaps in built-player evidence.
- [ ] Remove coplanar roof overlap/z-fighting.
- [ ] Render and compare the roofline against the pinned reference before accepting it.

## 6. Doors and windows
- [ ] Add/correct garage-door openings if present; otherwise document N/A.
- [ ] Match the main entry opening and door shape, size, position, and material to the actual reference.
- [ ] Match the largest/front-facing windows.
- [ ] Match remaining visible windows, shutters, and dormer/gable openings without inheriting the discarded image's arrangement.
- [ ] Match window sill heights.
- [ ] Match window head heights.
- [ ] Match horizontal spacing/alignment.
- [ ] Add visible recess depth using production carve/inset geometry, not flat visual stand-ins.
- [ ] Match frames, sills, and headers where visible.
- [ ] Match door/window trim.
- [ ] Create or reuse parameterized helpers for the required repeated window assemblies and curved openings where appropriate.
- [ ] Create or reuse parameterized helpers for the required door assemblies.
- [ ] Render and compare opening placement/proportions against the pinned reference.

## 7. Architectural detail
- [ ] Match exterior trim, timber, and corner boards where present.
- [ ] Match porch columns/posts/supports where present; document N/A otherwise.
- [ ] Match entry steps.
- [ ] Match railings/fence-like accents where visible; document N/A otherwise.
- [ ] Match prominent window/door surrounds.
- [ ] Match visible masonry/foundation/chimney accents.
- [ ] Add gutters/downspouts if materially visible at target scale; otherwise record a reference-grounded N/A, not an unmet requirement deferred to later.
- [ ] Add visible vents and other high-contrast facade/roof details as required by the actual reference.
- [ ] Match other silhouette/depth-critical details from the actual image, including ornaments, planting, flower boxes, and ivy only where applicable.
- [ ] Defer only details that do not survive target render scale; do not defer a visible mismatch needed for very-close fidelity.

## 8. Immediate site composition
- [x] Keep site construction separate from reusable house geometry.
- [ ] Match driveway geometry if present; otherwise document N/A.
- [ ] Match the entry walkway's route, width, height, and material.
- [ ] Match porch/entry step/pad geometry.
- [ ] Match visible ground/lawn appearance through the normal production world/material path.
- [ ] Match grading/step transitions needed to meet the house.
- [ ] Match landscaping/planting masses where they contribute to the reference composition through applicable production systems.
- [ ] Check house/site boundaries for gaps, floating geometry, and unsupported contact in built-player evidence.

## 9. Final material assignment
- [ ] Assign exterior-wall materials to the correct elements/faces from the pinned image.
- [ ] Assign correct roofing materials to visible roof volumes.
- [ ] Assign trim/fascia/soffit/support materials.
- [ ] Assign entry, garage-door, shutter, and painted-detail materials only to the roles actually present; document absent roles as N/A.
- [ ] Assign window/glass materials.
- [ ] Assign masonry/concrete/foundation/chimney materials.
- [ ] Assign driveway/walkway/ground/foliage roles as applicable.
- [ ] Correct siding/stucco projection and orientation.
- [ ] Verify roof texture orientation along visible slopes in built-player evidence.
- [ ] Verify wood/masonry direction.
- [ ] Tune repeat, motif size, and texel density against the reference, not merely for consistency with the discarded reconstruction.
- [ ] Check material seams at corners, openings, and transitions.
- [ ] Remove duplicate/coplanar material surfaces that can flicker.

## 10. Reference camera and lighting
- [x] Keep the reference-comparison camera/view separate from reusable house geometry.
- [ ] Match camera azimuth/visible side to the pinned image; do not assume a frontal view.
- [ ] Match camera height.
- [ ] Match vertical viewing angle.
- [ ] Match perspective/projection/FOV; the previous 36-degree setting is not an acceptance requirement.
- [ ] Match framing, crop, and output aspect ratio to the reference.
- [x] Use the project's normal sky/environment/rendering path.
- [ ] Match the dominant sun/key direction and overall lighting character through production controls.
- [ ] Tune ambient/fill and contrast so facade depth remains readable and close to the reference.
- [x] Keep camera/light configuration outside the reusable house builder.
- [ ] Accept camera and lighting only after direct comparison; do not use angle, darkness, crop, or occlusion to hide geometric mismatches.

## 11. Mandatory render / compare / correct loop
For each meaningful iteration, capture an actual standalone-player render of the current exact feature SHA, view it side by side with the pinned repository image, record the concrete mismatches below or in linked durable evidence, fix their production causes, and repeat. No arbitrary iteration count, elapsed effort, or mechanically green run makes the result acceptable. Continue until it is very close to the specific reference and production-quality; genuine blockers keep the issue open. After two materially different unsuccessful fixes of the same symptom, isolate the cause before trying again.

- [ ] Compare and correct overall silhouette.
- [ ] Compare and correct total width/depth/story proportions and major massing.
- [ ] Compare and correct roofline, pitches, ridges, eaves, and intersections.
- [ ] Compare garage applicability against the pinned image; match it if present or document N/A.
- [ ] Compare and correct door/window shapes, counts, placement, and spacing.
- [ ] Compare and correct major trim/details and their depth.
- [ ] Compare and correct material identity, colour, roughness, and contrast.
- [ ] Compare and correct texture scale, motif, and orientation; replace or create textures when existing assets prevent fidelity.
- [ ] Compare and correct lighting, framing, and overall readability.
- [ ] Inspect durable side/rear audit captures for holes; the actual capture cadence must include each required view.
- [ ] Inspect for unintended overlapping geometry.
- [ ] Inspect for reversed/missing faces.
- [ ] Inspect for floating elements and unsupported contact.
- [ ] Inspect for z-fighting/coplanar surfaces across more than one frame.
- [ ] Inspect for incorrect material assignments.
- [ ] Fix structural/proportion mismatches before cosmetic mismatches.
- [ ] Preserve unaltered reference/render originals and record reference path/blob, feature SHA, exact CI request/run, artifact and frame paths, camera settings, discrepancies, and resulting decisions with each reviewed iteration. Label any comparison overlays as diagnostics, not replacement render evidence.
- [ ] Rebuild and inspect after the last product change; evidence from an earlier implementation is not final proof.
- [ ] Document a final side-by-side review showing very-close fidelity across all major visual relationships AND classify the actual render as production-quality. Any material visible mismatch remains required work, not a follow-up deferred past closure.

## 12. Integration and cleanup
- [x] Expose reusable house authoring through the existing WorldBuilder production composition path.
- [ ] Verify the corrected house and all chosen textures/materials use the normal asset/material/scene path; no image billboard, parallel renderer, or scene-local art implementation may substitute for real voxel-engine content.
- [ ] Remove temporary/debug geometry from the final authored result.
- [ ] Remove temporary/debug materials while retaining required production assets.
- [ ] Refactor repeated architectural components into reusable/config-driven helpers where needed by the corrected house.
- [x] Keep site composition separable from house geometry.
- [x] Keep reference camera/lighting separable from house geometry.
- [ ] Update comments/documentation and material/geometry regressions that encode assumptions from the wrong image; test correct production behavior instead of freezing the mistaken result.
- [ ] Prove translation/configuration reuse with an independent consumer/fixture and retain site/camera separation coverage after changes.
- [ ] Run and reconcile final exact-SHA build/test/module-local built-player gates on `ci-test/fixes/agent-5`; never replace queued/running requests.
- [ ] Produce and inspect the final reference-comparison render and required audit evidence.

## Acceptance checklist — all required before closure
- [ ] The corrected house loads through the normal project path without errors in final built-player proof.
- [ ] Every texture/material actually selected for the reconstruction resolves through the existing material system; supplied assets are optional and original/generated textures are permitted.
- [ ] The final target render is very close to `Assets/Textures/Stylized/experiment1/house/10dddef5-de0a-4153-9c09-b1e8016830db.png`, not the discarded Library image: silhouette, massing, proportions, and distinctive composition match closely.
- [ ] Roof pitches, ridges, eaves, and visible intersections match closely.
- [ ] Major doors/windows/openings are correctly shaped, placed, and proportioned.
- [ ] High-value architectural details have matching geometry/depth and believable support.
- [ ] Material identity, colour relationships, texture orientation, and repeat scale match closely and remain believable.
- [ ] Camera, framing, and lighting support a faithful comparison without disguising errors.
- [ ] No major gaps, floating elements, z-fighting, missing faces, or obvious unintended overlaps remain in target and audit views.
- [ ] Repeated architectural components use reusable helpers rather than unnecessary duplication or evidence-only geometry.
- [ ] House geometry remains reusable independently of reference-specific camera, lighting, and immediate site, with regression proof.
- [ ] Durable final reference-comparison evidence identifies the exact reference blob, feature SHA, CI run, and frames; direct inspection establishes both very-close resemblance and production quality, separately from green automation.

## Closure and promotion — subsequent workflow, not pre-closure checkboxes
Only after every required checkbox and acceptance criterion above passes, complete fixed metadata and move only this assignment from `open/` to `closed/`; never use `pending/`. Fetch and merge current `origin/master` into `fixes/agent-5`, resolve only in-scope conflicts, and complete required revalidation. Open/update the final PR to `master`, enable auto-merge, and monitor the required `affected` gate and canonical built-player integration until merged. Never push the feature head directly to master. Verify the closed SceneIssue on `origin/master` before reporting the assignment complete. Follow `SceneIssues/README.md` for the exact workflow.
