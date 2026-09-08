# New House WorldBuilder Tasks

## Binding objective
Recreate `Assets/Textures/Stylized/experiment1/house/10dddef5-de0a-4153-9c09-b1e8016830db.png` as closely as possible through production WorldBuilder/Structures/material/rendering paths. Pinned Git blob: `6d87b08d4c7c9bddc1705c0f34343aa79bc18423`. Green CI alone is insufficient; final exact built-player evidence must be **very close** and **production-quality**.

## 0. Reference, ownership, evidence
- [x] Pin and inspect the correct repository reference/blob.
- [x] Preserve exact reference bytes/provenance in targeted-CI artifacts.
- [x] Use WorldBuilder production authoring, material registry/texture layers, storage/meshing/rendering, and built-player replay.
- [x] Keep reusable house geometry separate from reference site/camera/light composition.
- [x] Keep module-local WorldBuilder validation and canonical standalone integration in the normal repo path.
- [x] Keep meaningful visual iterations tied to exact feature SHA, request/run, artifact, and target/front-left/rear-right frames through iteration 13.
- [ ] Preserve the same exact evidence linkage for every subsequent product-changing iteration through final acceptance.

## 1. Production path / budgets / reuse
- [x] Major architectural dimensions/configuration remain centralized and translation-invariant.
- [x] Site policy and camera/light policy remain outside reusable house geometry with regression proof.
- [ ] Confirm final authoring write count/budget, player geometry memory, and relevant runtime budgets after the last product change; do not relax budgets.
- [ ] Verify final implementation has no test-only/parallel renderer, billboard, scene-local duplicate visual authority, fake material path, or evidence-only production substitute.
- [ ] Remove obsolete blockout-only layers/helpers made redundant by the accepted final correction when they can remain visible or wasteful.
- [ ] Keep repeated architectural components reusable/config-driven; update stale comments/regressions that encode obsolete assumptions.

## 2. Materials / textures
- [ ] Record retained/adapted/unused supplied texture candidates for plaster, timber, blue roof, masonry, door/accent, foliage, flowers, ground, glass, and ornament.
- [ ] Decide explicitly whether current supplied textures are sufficient for final fidelity; adapt/create original assets through the normal pipeline if not.
- [ ] Finalize semantic material roles/IDs for plaster, timber/trim, roof, glass, painted details/door, masonry/chimney, ground, flowers/foliage, and ornament.
- [ ] Finalize repeat scale, projection, roughness/detail strength, and tint relationships against the pinned reference.
- [ ] Verify roof texture orientation, wood/masonry direction, texel density, seams, and no coplanar flicker in final exact built-player evidence.
- [ ] Verify every selected material/texture resolves through the existing material system in final module-local/built-player proof.

## 3. Massing / roof / silhouette
- [x] Replace monolithic roof with transverse shoulders plus shallow steep portrait gable.
- [x] Repair destructive roof-clear side/rear shell holes and restore intentional openings afterward.
- [x] Replace straight portrait roof with nonlinear swept/concave profile and close roof/plaster seams.
- [x] Widen portrait spring line to the reference-driven broader proportion.
- [x] Restore upper flower box after destructive portrait rebuild.
- [x] Extend left chimney to a tall ridge-relative continuous masonry stack with restrained stepped cap.
- [ ] Finalize width/depth/storey proportions, facade projection, eave/ridge datums, lower stone mass, upper plaster/timber mass, and entry-step silhouette against the target.
- [ ] Match final roof pitch/rise, ridge direction, shoulder height, swept eaves, fascia/edge depth, intersections, and chimney relationship closely.
- [ ] Verify no roof overlap, unsupported roof pieces, holes, or z-fighting across target/front-left/rear-right final frames.
- [ ] Confirm final overall silhouette and major massing are very close to the reference before closure.

## 4. Doors / windows / facade hierarchy
- [x] Use production carve/inset authoring for the central portal and arched windows.
- [x] Restore middle-storey blue-shuttered arched window after earlier roof replacement.
- [x] Restore smaller high-gable arched window after destructive silhouette repair.
- [ ] Match final lower portal width/height, lower side-window size/spacing, sill/head heights, surround depth, frames/muntins, and recess depth.
- [x] Match middle-storey central arch/shutters to a materially more compact reference-driven scale; final combined facade acceptance remains pending.
- [ ] Match high-gable opening size/placement/surround to the reference.
- [ ] Render/compare all visible openings after the last product change.

## 5. Architectural detail / planting / site
- [ ] Match timber belts/posts/braces and prominent stone/timber surrounds with believable thickness/support.
- [ ] Match entry steps and lower masonry accents.
- [x] Left chimney now has the correct tall continuous silhouette direction; final exact comparison still required for cap/detail acceptance.
- [x] Compact crest/finial, pointed banner, and closer-to-wall hanging sign are present through production authoring.
- [ ] Refine crest/banner/sign/ornament so target-scale detail is compact and intentional rather than block-symbol-like.
- [ ] Refine flower boxes, ivy/foliage masses, and blossoms so they read as connected natural planting rather than thick columns/dotted primitives.
- [ ] Finalize walkway/entry approach, neutral ground, planting masses, contact/grading, and step transitions.
- [ ] Verify no floating/intersecting site pieces in target/audit frames.

## 6. Camera / lighting
- [x] Use production sky/environment; camera/light composition remains outside reusable house builder.
- [ ] Match target camera azimuth, height, vertical angle, FOV/perspective, crop, and aspect relationship to the pinned image.
- [ ] Match key/sun direction, ambient/fill, contrast, and facade readability without hiding geometry defects.
- [ ] Accept camera/lighting only after final side-by-side target comparison.

## 7. Exact render / compare / correct history
- [x] Iteration 6 run `34136916256`: shell repaired; rejected as prototype/blockout quality.
- [x] Iteration 7 run `34145422534`: finish-pass details authored; rejected due high-window occlusion/crest/eave defects.
- [x] Iteration 8 run `34148998318`: high-window/crest/eave corrections green; rejected due giant straight gable.
- [x] Iteration 9 run `34153598913`: nonlinear profile experiment exposed regression-selection ambiguity and roof/plaster seams; failed module validation.
- [x] Iteration 9b feature `3574429e...`, request `1dabd0e9...`, run `34155498206`: seams/module/player green; rejected because final rebuild erased upper flower box.
- [x] Iteration 10 feature `6e5501cf...`, request `ec5b0ec2...`, run `34162021441`: flower box survives; rejected because spring line too narrow.
- [x] Iteration 11 feature `47000509...`, request `f0158433...`, run `34165061316`: broader spring line green; rejected because chimney was too short.
- [x] Iteration 12 feature `800f5c5d1333cad1169b69f589de5b3928bd7f9e`, request `772e9e73eeb89bb790f37599e140a803d8302f2b`, run `34166993216`, artifact `single-test-34166993216`: tall chimney corrected; visual closure rejected because middle opening/framing remained oversized.
- [x] Iteration 13 feature `b045e276b727f785d78d6f9069ca36e0b27df83e`, request `ac63e59647ab7d72baaabd3c4aa16647dfbe11b1`, run `34174645322`, artifact `single-test-34174645322`: full module validation and standalone replay succeeded; exact target/front-left/rear-right inspection confirms the compact middle arch/shutters improve hierarchy while gable/chimney/upper flower box/side openings/rear shell remain intact. Visual closure rejected as **prototype/blockout quality** because the ground-floor entry still reads as a flat rectangular door instead of the deep projecting round stone portal in the reference.

### Iteration 14 — projecting lower entry portal
- [x] Isolate the dominant iteration-13 mismatch as authored lower-portal depth/surround geometry rather than camera framing.
- [ ] Rebuild only the shallow central lower-entry patch with a recessed timber door, visibly projecting round stone voussoir ring/jambs, and restored compact hardware; preserve both lower side windows and all upper/roof/site/audit geometry.
- [ ] Add a focused behavioral regression proving final entry refill, protruding arch/jamb geometry, recessed door, and shallow repair ordering through the production refinement path.
- [ ] Run the resulting exact feature SHA through `ci-test/fixes/agent-5` with only `.github/test-request.json` on the transport.
- [ ] Inspect exact target/front-left/rear-right artifact; if still below production-quality, record only the next largest demonstrated mismatch before another product change.

## 8. Final visual acceptance — all required
- [ ] Overall silhouette very close to reference.
- [ ] Width/depth/storey proportions and major massing very close.
- [ ] Roofline/pitches/ridges/eaves/intersections/chimney very close.
- [ ] Door/window shapes/counts/placement/spacing/depth very close.
- [ ] Major trim/ornament/detail depth very close and physically supported.
- [ ] Planting/flower boxes/ivy read naturally at target scale.
- [ ] Material identity, color/roughness relationships, texture scale/orientation very close and believable.
- [ ] Lighting/framing/readability support faithful comparison.
- [ ] Side/rear audits show no wall-sized holes, missing/reversed faces, floating pieces, unintended overlaps, or unsupported contact.
- [ ] Multiple final frames show no obvious z-fighting/coplanar flicker.
- [ ] No incorrect material assignment survives final inspection.
- [ ] Preserve final reference/render originals plus reference blob, feature SHA, request/run, artifact/frame paths, camera settings, discrepancies, and decisions.
- [ ] Rebuild and inspect after the **last** product change; older evidence cannot serve as final proof.
- [ ] Document final side-by-side review and classify the exact target render **production-quality**.

## 9. Acceptance checklist — all required before closure
- [ ] Corrected house loads through the normal project path without errors in final built-player proof.
- [ ] Every selected texture/material resolves through the existing material system.
- [ ] Final target render is very close to the pinned image in silhouette, massing, proportions, openings, and distinctive composition.
- [ ] Roof/chimney relationships match closely.
- [ ] Major doors/windows/openings are correctly shaped, placed, and proportioned.
- [ ] High-value architectural details have matching geometry/depth and believable support.
- [ ] Material identity/color/texture orientation/repeat scale match closely and remain believable.
- [ ] Camera/framing/lighting support faithful comparison without disguising errors.
- [ ] No major gaps, floating elements, z-fighting, missing faces, or unintended overlaps remain.
- [ ] Repeated architectural components use reusable helpers rather than unnecessary duplicate/evidence-only geometry.
- [ ] House geometry remains reusable independently of reference-specific camera, lighting, and immediate site, with regression proof.
- [ ] Durable final evidence identifies exact reference blob, feature SHA, CI run, and frames; direct inspection establishes both very-close resemblance and production quality separately from green automation.

## 10. Closure / promotion — only after every box above passes
- [ ] Complete final cleanup/reuse/comment checks and exact-SHA module + standalone-player gates.
- [ ] Complete `issue.json` fixed metadata: `status`, `resolvedUtc`, `resolutionSummary`, `regressionTest`, `fixCommit`.
- [ ] Fetch and merge current `origin/master` into `fixes/agent-5`, resolving only in-scope conflicts; revalidate exact source as required.
- [ ] Move only this assignment `SceneIssues/open/20260904-231343-000-NewHouseWorldBuilderReferenceReconstruction` → `SceneIssues/closed/20260904-231343-000-NewHouseWorldBuilderReferenceReconstruction`; never use `pending/`.
- [ ] Push `fixes/agent-5`, open/update PR to `master`, and enable auto-merge immediately.
- [ ] Monitor required PR `affected` gate and canonical standalone integration until merged.
- [ ] Verify the closed SceneIssue is visible on `origin/master` and the open path is absent before reporting completion.
