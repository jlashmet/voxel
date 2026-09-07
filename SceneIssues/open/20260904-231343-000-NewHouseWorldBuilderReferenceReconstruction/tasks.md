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
- [x] Validate iteration-8 correction with feature `ef132fadc51c93ec520e611254d65949a4691b48`, exact request `6d825d7de506287a253fd229687f29d6eb6f53d9`, run `34148998318`, and direct target/front-left/rear-right inspection. The corrected openings/crest and closed rear shell are mechanically proven, but visual quality remains prototype/blockout quality.
- [x] Iteration 9/9b: complete the narrow concave swept portrait-gable profile while holding camera/light/material/site fixed; exact run `34155498206` verifies the roof/plaster interface seams are closed and the side/rear shell remains closed in audit evidence.
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

### Iteration 8 — exact result
- [x] Fix the high-window production depth ordering by carving from `frontZ-4` through the repair layer before drawing glass/frame.
- [x] Clear duplicate crest mass from `ridge+1` and rebuild one compact tapered finial.
- [x] Extend the portrait eave hook to sixteen voxels with a stronger quadratic drop.
- [x] Update the focused refinement regression to prove all three corrected invariants.
- [x] Run feature `ef132fadc51c93ec520e611254d65949a4691b48` with exact request `6d825d7de506287a253fd229687f29d6eb6f53d9` / run `34148998318`; module validation, standalone replay, previews, artifact upload, and final status passed.
- [x] Inspect the exact pinned reference and iteration-8 target/front-left/rear-right captures from artifact `single-test-34148998318`.
- [x] Reject iteration 8 as **prototype/blockout quality** despite corrected openings/crest/shell. Largest demonstrated structural mismatch: the outer portrait-gable roof remains a very large straight triangular plane rather than the reference's narrow concave/swept silhouette; front-left evidence confirms authored geometry rather than camera perspective.

### Iteration 9 — exact failed result and 9b repair
- [x] Replace only the shallow straight portrait-gable outer profile with a row-by-row concave swept production profile; hold camera, light, material, site, shoulder roof, side/rear shell, and opening layout fixed.
- [x] Add a focused regression proving the destructive clear is shallow/front-only, the profile narrows nonlinearly, and final embedded upper-opening carving happens after the rebuild.
- [x] Run feature `34ed16f6c845e7a7436ed02fd7ecb1f7ba8a61d0` with exact request `22ed95b36c074f65ab3d41329866cc38a728b352` / run `34153598913`; the run completed **failed** in full WorldBuilder module validation.
- [x] Inspect diagnostic-only exact reference plus t=10/t=20/t=30 frames from failed artifact `single-test-34153598913`. The unchanged-camera experiment materially narrows/sweeps the profile, supporting the geometry hypothesis, but black one-voxel roof/plaster separation seams remain and visual quality is still **prototype/blockout quality**; rear shell remains closed.
- [x] Isolate the mechanical failure: the older swept-tip regression used `FindIndex` on only `x/z` and bound to an earlier pre-audit duplicate coordinate (`1188`) rather than the intended late finish-pass operation after rear-shell index `1506`; the requested concave-profile test itself was not the failing case.
- [x] Implement iteration 9b production seam closure by making plaster meet both roof inner edges exactly and placing timber bargboard across the shared joint; strengthen the focused regression to prove both interfaces have no gap.
- [x] Tighten the older swept-tip regression to search only after the rear-shell checkpoint, preserving rather than lowering its post-audit invariant.
- [x] Re-run the same focused concave-profile regression on feature `3574429e2374cb3d1d311839edd397cbf15602c5` through exact request `1dabd0e974e70c13e1e93b6375f8aeb5ab7c1559` / run `34155498206`; full module validation and standalone-player replay passed.
- [x] Inspect exact iteration-9b pinned reference plus target/front-left/rear-right frames from artifact `single-test-34155498206`. Roof/plaster seams are closed and the shell remains closed; visual closure is rejected because the final destructive portrait rebuild erases the dense upper-gable flower box beneath the high window.

### Iteration 10 — upper portrait planting ordering
- [x] Isolate the missing upper flower box as a production-order defect: `AddIvyAndFlowers` authors it before `NewHouseReferenceFinishPass` clears/rebuilds the portrait shell, while the finish pass restores the high arch but previously omitted the flower box.
- [x] Restore the same 24-voxel timber/foliage/blossom flower box after the final arch carve, holding camera/light/material/site and the iteration-9b silhouette fixed.
- [x] Add focused regression `NewHouseReferenceFinishPassOrderTests.FinishPass_RestoresUpperGableFlowerBoxAfterDestructivePortraitRebuild` proving the final ordering and production palette use.
- [ ] Run the iteration-10 feature head through exact-SHA targeted CI without replacing a queued/running request.
- [ ] Inspect exact iteration-10 target/front-left/rear-right frames. Verify the dense upper flower box survives and select only the next largest demonstrated mismatch if the house is still below production-quality.

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
