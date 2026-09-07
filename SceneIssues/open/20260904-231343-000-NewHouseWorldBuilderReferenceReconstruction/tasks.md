# New House WorldBuilder Tasks

## Binding objective and authoritative reference
Recreate **the particular house in `Assets/Textures/Stylized/experiment1/house/10dddef5-de0a-4153-9c09-b1e8016830db.png` as closely as possible** through production WorldBuilder, Structures, storage/meshing/rendering, and normal game material/texture systems. Keep iterating with exact-SHA standalone-player renders until the result is **very close and production-quality**. Passing CI or having the right object categories is insufficient.

Pinned reference Git blob: `6d87b08d4c7c9bddc1705c0f34343aa79bc18423`.

## 0. Correct reference, ownership, and evidence
- [x] Pin the user-specified repository reference path/blob and invalidate the earlier wrong Library reference.
- [x] Inspect the actual pinned image before further visual work. Iteration-6 artifact copied the exact file to `ReferenceInputs/NewHouse/`; local `git hash-object` of that artifact is `6d87b08d4c7c9bddc1705c0f34343aa79bc18423`.
- [x] Validate `NewHouseReferenceSourceTests.ReferenceInput_MatchesPinnedBlob_AndPreservesOriginalForReview`; exact run `34136916256` passed it and preserved the source plus provenance.
- [x] Restore/verify discovery of `NewHouseReferenceAuthoringTests`; exact run `34136916256` executed and passed all four discovered cases, including translation/site separation, shallow steep-front roof/compact crest, material-role coverage, and reference-site separation.
- [x] Record the visible reference relationships: tall three-register portrait composition; pale stone lower storey; arched central portal and narrow lower side windows; timber/plaster middle storey with blue-shuttered arch; steep blue portrait gable with smaller arch; transverse roof shoulders; swept eaves; compact warm crest; left chimney; flower boxes/ivy; blue-gold banner and bracketed sign.
- [x] Record hidden/absent-role conclusions from the visible reference: garage, driveway, dormer, porch roof, and materially visible gutters are N/A for target-view fidelity; hidden elevations are audit correctness surfaces rather than invented reference matching.
- [x] Identify reusable authoring ownership in `Assets/Game/WorldBuilder`, material ownership in `Assets/Game/Materials`, semantic-free Rendering consumption, and module-local validation scene ownership.
- [x] Correct evidence cadence: exact SceneIssue captures now include target/front-left/rear-right at ~10/20/30 seconds.
- [ ] Keep every subsequent meaningful visual iteration tied to exact reference blob, feature SHA, CI request/run, artifact, and inspected frame paths.

## 1. Production path / budgets
- [x] Use the existing WorldBuilder composition, structure-authoring session, voxel storage, material registry, texture registration, meshing/rendering, environment, and built-player paths.
- [x] Keep major architectural dimensions centralized in `NewHouseReferenceConfig`.
- [x] Keep reference site construction separate from reusable house geometry.
- [x] Keep reference camera/light policy separate from reusable house geometry.
- [x] Retain translation/configuration reuse proof and site-policy separation regression.
- [ ] Confirm final authoring write count/budget, player geometry memory, and relevant runtime budgets after the last product change; do not relax repository budgets.
- [ ] Verify final implementation has no test-only/parallel renderer, billboard, scene-local duplicate geometry, or fake material path.

## 2. Materials and textures
- [ ] Inspect the supplied `Assets/Textures/Stylized/experiment1/` candidates visually and record which are retained, adapted, or unused for plaster, timber, blue roof, masonry, door/accent, and foliage.
- [ ] Decide explicitly whether the current supplied textures are sufficient for final fidelity; create/adapt original assets through the normal pipeline if they are not.
- [ ] Reassess and finalize game material IDs/roles for plaster, timber/trim, roof, glass, painted details/door, masonry/foundation/chimney, ground, flowers/foliage, and ornament.
- [ ] Set/finalize believable repeat scale, projection, roughness/detail strength, and tint relationships against the pinned reference.
- [ ] Verify final roof texture orientation, wood/masonry direction, texel density, corner/opening seams, and absence of coplanar flicker in standalone-player evidence.
- [ ] Verify every selected material/texture resolves through the existing material system in final module-local built-player proof.

## 3. Primary massing and scaffold
- [x] Add reusable `NewHouseReferenceAuthoring` plus reference-driven refinement through production structure authoring.
- [ ] Reassess/finalize width, depth, foundation height, lower/upper storey heights, wall thickness, facade projection, eave/ridge datums, and roof/body proportions against the target frame.
- [ ] Match foundation/lower stone mass, upper timber/plaster mass, projections/recesses, and entry step/pad silhouette closely.
- [x] Garage mass/garage opening: N/A from the pinned reference.
- [x] Driveway: N/A from the pinned reference.
- [ ] Confirm the final primary silhouette is very close before accepting cosmetic finish.

## 4. Roof and gable
- [x] Replace the conflicting monolithic/side-wing roof composition with transverse shoulders plus a steep shallow portrait gable.
- [x] Restore front openings after the destructive roof-clear pass.
- [x] Repair side/rear shell infill and rear gable after roof clear; exact iteration-6 rear-right audit no longer shows the previous wall-sized voids.
- [x] Implement iteration-7 finish pass for layered portrait-gable timber, smaller high arch, swept eave tips, compact crest, and rebuilt hanging details.
- [x] Validate iteration-7 finish pass in exact-SHA run `34145422534` and inspect target/front-left/rear-right frames; mechanical proof passed but direct visual inspection rejected the result.
- [x] Isolate iteration-7 high-window depth-ordering defect: opaque `FillArch` repair extended to `frontZ-4` while replacement carve began at `frontZ-1`, hiding the glass behind plaster.
- [x] Implement iteration-8 correction to carve through the full repair depth, clear duplicate crest layers from `ridge+1`, and extend/increase the swept-eave hook.
- [ ] Validate the iteration-8 correction in a new exact-SHA run and directly inspect target/front-left/rear-right frames.
- [ ] Match final roof pitch/rise, ridge direction, transverse shoulder height, swept-eave silhouette, fascia/edge depth, intersections, and chimney relationship closely.
- [ ] Remove/verify absence of roof overlap, z-fighting, holes, and unsupported roof pieces across multiple frames.
- [x] Dormer roof: N/A from the pinned reference.
- [x] Garage roof: N/A from the pinned reference.
- [x] Porch roof: N/A from the pinned reference.

## 5. Doors and windows
- [x] Use production carve/inset authoring for the central arched portal and arched windows rather than flat evidence geometry.
- [x] Restore the middle-storey blue-shuttered arched window after roof replacement.
- [x] Implement a smaller high-gable arched window in the iteration-7 finish pass.
- [x] Prove the iteration-7 high window was structurally authored but visually occluded by the front plaster repair depth; regression now checks a full-depth replacement carve.
- [ ] Match final entry arch/door width-height, lower side-window size/spacing, middle window/shutters, high window, sill/head heights, surrounds, frames/muntins, and recess depth to the reference.
- [ ] Render/compare all visible opening shapes and proportions after the last product change.

## 6. Architectural detail and planting
- [ ] Match timber belts/posts/braces and prominent stone/timber surrounds with believable thickness/support.
- [ ] Match entry steps and lower masonry accents.
- [ ] Match left chimney silhouette and cap detailing.
- [x] Implement a compact replacement crest/finial in the iteration-7 finish pass.
- [x] Implement a pointed blue-gold banner and a closer-to-wall bracketed hanging sign in the iteration-7 finish pass.
- [x] Validate crest, banner, sign, and swept-eave proportions in iteration 7; crest/eave remained blockout-like and require the iteration-8 production correction.
- [ ] Refine flower boxes, ivy/foliage masses, and blossoms so they read as connected intentional planting rather than thick voxel columns/dotted primitives.
- [x] Porch columns/posts: N/A from the visible reference.
- [x] Materially visible gutters/downspouts: N/A at the target reference scale.
- [ ] Add/refine any remaining high-contrast trim/ornament that survives target render scale; do not defer visible mismatches required for very-close fidelity.

## 7. Immediate site
- [x] Keep site composition outside reusable house authoring.
- [ ] Finalize walkway/entry approach route, width, elevation, and stone relationship to the portal.
- [ ] Finalize neutral ground appearance and low planting masses to support the reference composition without a distracting rectangular lawn/apron.
- [ ] Verify house/site contact, grading, step transitions, and no floating/intersecting site pieces in target/audit frames.

## 8. Camera and lighting
- [x] Use production sky/environment and keep camera/light composition outside the house builder.
- [ ] Match target camera azimuth, height, vertical angle, FOV/perspective, crop, and aspect relationship to the pinned image.
- [ ] Match key/sun direction, ambient/fill, contrast, and facade readability without using darkness/crop/occlusion to hide geometry defects.
- [ ] Accept camera/lighting only after direct side-by-side comparison of the final product render.

## 9. Mandatory render / compare / correct loop
### Iteration 6 — exact result
- [x] Run exact request `e9c814785db3477a9b74a29c5705afbaf52c221f` / run `34136916256` for feature `a679777dc24cba437837fe50050b55c019026fdc`; mechanical CI and module validation passed.
- [x] Inspect exact reference plus `SceneIssue/Screenshots/frame_001_t010.0.png`, `frame_002_t020.0.png`, and `frame_003_t030.0.png` from artifact `single-test-34136916256`.
- [x] Confirm previous side/rear wall-sized shell holes are repaired in rear-right audit and surface diagnostics report `missingVisible=0` / complete coverage.
- [x] Reject iteration 6 as **prototype/blockout quality** despite green CI. Concrete target defects: blank/straight portrait gable, oversized high opening, large blocky gold crest, insufficient swept eaves, flat banner/sign, coarse facade/foliage detail density.

### Iteration 7 — exact result
- [x] Add a production finish pass after audit-shell repair for layered gable framing, smaller high arch, swept eave tips, compact crest, and rebuilt hanging details.
- [x] Add focused regression proving the finish pass runs after shell repair and emits the smaller high opening, crest replacement, and swept tip.
- [x] Run feature `cd9ae8c2ed0f8789e5f2ddf858365ef78e0ba9a1` with exact request `02d3bf547696212b0530e42cb797884c687b7bae` / run `34145422534`; module validation, standalone replay, previews, artifact upload, and final status all passed.
- [x] Inspect exact iteration-7 reference plus target/front-left/rear-right frames from artifact `single-test-34145422534` and classify visual quality.
- [x] Reject iteration 7 as **prototype/blockout quality**. Largest demonstrated defect: the intended smaller high window is occluded by three opaque plaster layers left in front of its glass; crest still accumulates duplicate ridge layers and eave hook remains too short.

### Iteration 8 — selected correction
- [x] Fix the high-window production depth ordering by carving from `frontZ-4` through the repair layer before drawing glass/frame.
- [x] Clear duplicate crest mass from `ridge+1` and rebuild one compact tapered finial.
- [x] Extend the portrait eave hook to sixteen voxels with a stronger quadratic drop.
- [x] Update the focused refinement regression to prove all three corrected invariants.
- [ ] Run exact-SHA targeted CI for the final iteration-8 feature head; never replace it while queued/running.
- [ ] Inspect exact iteration-8 target/front-left/rear-right frames and classify visual quality.
- [ ] If below production-quality, record the largest failed visual relationship here and fix the production cause before another broad pass.

### Final visual checks — required before closure
- [ ] Overall silhouette very close to reference.
- [ ] Width/depth/storey proportions and major massing very close.
- [ ] Roofline/pitches/ridges/eaves/intersections very close.
- [ ] Door/window shapes/counts/placement/spacing very close.
- [ ] Major trim/ornament/detail depth very close and physically supported.
- [ ] Material identity, color/roughness relationships, texture scale/orientation very close and believable.
- [ ] Lighting/framing/readability support faithful comparison.
- [ ] Side/rear audits show no wall-sized holes, missing/reversed faces, floating pieces, obvious unintended overlaps, or unsupported contact.
- [ ] Multiple frames show no obvious z-fighting/coplanar flicker.
- [ ] No incorrect material assignment survives final inspection.
- [ ] Preserve exact final reference/render originals plus reference blob, feature SHA, request/run, artifact/frame paths, camera settings, discrepancies, and decisions.
- [ ] Rebuild and inspect after the **last** product change; older evidence cannot serve as final proof.
- [ ] Document final side-by-side review and classify the exact target render **production-quality**.

## 10. Integration / cleanup
- [ ] Remove any obsolete/blockout-only geometry made redundant by the final correction rather than merely layering over it when overlap can remain visible or wasteful.
- [ ] Remove temporary/debug materials/geometry while retaining required production assets and regressions.
- [ ] Keep repeated architectural components reusable/config-driven; refactor only where needed for acceptance/reuse correctness.
- [ ] Update comments/regressions that encode obsolete visual assumptions.
- [ ] Run/reconcile final exact-SHA build/test/module-local built-player gates on `ci-test/fixes/agent-5`.
- [ ] Produce and inspect final reference-comparison plus audit evidence.

## Acceptance checklist — all required before closure
- [ ] The corrected house loads through the normal project path without errors in final built-player proof.
- [ ] Every selected texture/material resolves through the existing material system.
- [ ] Final target render is very close to the pinned repository image in silhouette, massing, proportions, and distinctive composition.
- [ ] Roof pitches/ridges/eaves/intersections match closely.
- [ ] Major doors/windows/openings are correctly shaped, placed, and proportioned.
- [ ] High-value architectural details have matching geometry/depth and believable support.
- [ ] Material identity, color relationships, texture orientation, and repeat scale match closely and remain believable.
- [ ] Camera, framing, and lighting support faithful comparison without disguising errors.
- [ ] No major gaps, floating elements, z-fighting, missing faces, or obvious unintended overlaps remain in target/audit views.
- [ ] Repeated architectural components use reusable helpers rather than unnecessary duplicate/evidence-only geometry.
- [ ] House geometry remains reusable independently of reference-specific camera, lighting, and immediate site, with regression proof.
- [ ] Durable final evidence identifies exact reference blob, feature SHA, CI run, and frames; direct inspection establishes both very-close resemblance and production quality separately from green automation.

## Closure and promotion — only after all boxes above pass
- [ ] Complete fixed metadata (`resolutionSummary`, `regressionTest`, `fixCommit`, `resolvedUtc`) supported by the verified final result.
- [ ] Merge current `origin/master` into `fixes/agent-5`, resolving only in-scope conflicts, and revalidate as required.
- [ ] Move only this assignment `SceneIssues/open/...` → `SceneIssues/closed/...`; never use `pending/`.
- [ ] Push `fixes/agent-5`, open/update PR to `master`, and enable auto-merge immediately.
- [ ] Monitor required PR `affected` gate and canonical built-player integration until the PR merges.
- [ ] Verify the closed SceneIssue is visible on `origin/master` before reporting completion.
