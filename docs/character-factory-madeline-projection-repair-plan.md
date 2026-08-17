# Madeline Projection Repair Plan

This document is the source of truth for repairing the generated Madeline base character after the successful `madeline-base-body` run #48. Check an item only when repository state, a local/CI verifier, or a published visual proof demonstrates it.

## Goal

Produce a reusable, rigged Madeline base body whose geometry, proportions, face, hair, and projected appearance match the approved references closely enough to serve as the stable character underneath modular clothing. The repaired asset must animate correctly and must not bake Cleric clothing or equipment into the base mesh.

## Baseline

- [x] Start the repair branch from the latest published Madeline proof commit (`0ba152129833df31249fec96160b1752a59361ba`).
- [x] Preserve run #48 (`31962023227`) and artifact `madeline-base-body` as the visual/technical baseline.
- [x] Record the primary visible defects from the baseline: vertical texture smears on arms/hair, ladder-like white/blue streaks on torso and legs, shoulder/armpit tearing, muddy face/hair contamination, and ribbon-like arms in the idle proof.
- [x] Make the Madeline workflow branch-safe so repair commits can run on `agent/madeline-projection-repair`, use branch-scoped concurrency, and publish proof images back to the branch that produced them.
- [x] Add durable diagnostic output that makes source-view selection, projected UV misses, subject masks, large snap distances, outer-span side projections, and normal-transform disagreements inspectable after a build.

## Phase 1 — Reproduce and localize the projection failure

- [x] Identify the exact script(s) that project front/back/left/right appearance onto the canonical skinned mesh.
- [x] Trace the coordinate spaces used by the projection path: generated reconstruction space, canonical bind-pose space, camera/view space, normal space, and UV/image space.
- [x] Verify that projection positions are evaluated in world space and switch body/hair normal evaluation to the mathematically correct inverse-transpose world normal basis; run #66 proved the prior and corrected normal transforms select the same source view for this baseline (`0` disagreements).
- [x] Verify source-image orientation for all four views, including horizontal mirroring rules and right/left assignment; run #67 proves the approved left/right profile sources require the corrected side mappings.
- [ ] Verify projection coordinates are bounded/rejected before sampling and never move arbitrarily to an unrelated strip of the image.
- [ ] Verify the subject bounds/mask used by each turnaround view excludes gray/white canvas and neighboring content robustly at difficult hair/hand/foot rows.
- [ ] Add a deterministic regression fixture for representative head, torso, arm, hand, thigh, calf, and hair points with expected source view and valid projected coordinates.
- [ ] Add a failure mode that rejects implausibly elongated or discontinuous projected islands instead of publishing a visibly corrupted texture.

### Phase 1 findings

- `production/madeline/build.sh` invokes `runtime/blender_texture_rigged_character.py`, which imports the already aligned/skinned FBX and delegates body/hair appearance projection to `runtime/blender_multiview_texture.py`.
- `blender_multiview_texture.py` computes character bounds and loop positions in Blender world space, maps world X/Z for front/back and world Y/Z for side views, selects a single source view per polygon from the polygon normal, then writes UVs into a 2x2 source-image atlas.
- Face identity is a second independent pass in `runtime/blender_project_face_texture.py`; it uses Head weights plus an anatomical gate and front-facing world normals to assign a separate face material/UV set.
- The clean run #48 body-only front/back/left/right sources and atlas prove the large smears are introduced after source preparation, not by the turnaround generation itself.
- The current multiview code has an important failure amplifier: `_nearest_foreground_uv()` searches arbitrarily far for foreground and snaps a miss to the first nearby foreground row/run. On thin/foreshortened regions this can collapse many unrelated loops onto a narrow strip instead of rejecting the projection and trying another view.
- Run #66 (`32044604749`) quantified that failure. Front/back had roughly 13–16% snapped loops, while left/right had 49.0%/33.7% snapped loops. Large snaps over the 20.48 px diagnostic threshold affected 36.3% of left-view loops and 20.1% of right-view loops; p95 snap distance was 100 px and the maximum was 316 px.
- Side projection intentionally drops world X. In a T-pose, the extended arms lie largely along that dropped axis and are severely foreshortened/occluded in the side references. Run #66 found 778 left-view and 760 right-view polygons in the high-risk outer arm span.
- The approved left-side image faces image-left and the right-side image faces image-right. The prior `_source_uv()` implementation had both horizontal mappings reversed. Commit `89edf2d2a93ffbc7b3fc9b3d1dfca07a31f1dfdd` corrected them and switched body normal transformation to inverse-transpose.
- Run #67 (`32044802894`) passed after that correction and materially improved sampling statistics: left large snaps dropped from 36.3% to 28.8%, right from 20.1% to 9.1%, and global p95 snap distance from 100 px to 71 px. The corrected render is still visibly unacceptable, so orientation was a real defect but not the dominant remaining one.
- Large run #66 samples also show 160–200 px front/back misses on outer-arm vertices. Therefore the next repair cannot simply disable side views; it must validate candidate projections and bound/reject implausible silhouette corrections across all views.

## Phase 2 — Fix body/hair appearance projection

- [ ] Correct source-view selection so front-facing body regions prefer front/back views and side-facing regions prefer the appropriate side view without unstable switching.
- [ ] Correct projected image coordinates so arm, leg, torso, and hair samples remain inside the intended character silhouette without arbitrary long-distance snapping.
- [ ] Replace hard source-view boundaries with confidence-weighted blending where adjacent views have comparable visibility.
- [ ] Add normal-angle, depth/occlusion, and subject-mask confidence to prevent projecting background or hidden surfaces.
- [ ] Prevent front/back projection from painting through the body onto the opposite surface.
- [ ] Prevent arm projection from sampling narrow vertical image strips that create the current ribbon/smear artifacts.
- [ ] Prevent long hair projection from borrowing torso/background pixels.
- [ ] Add seam-safe fallback fill for texels that have no trustworthy source sample.
- [ ] Produce a diagnostic body-only base-color preview and confirm the torso, arms, legs, and hair read coherently before face identity projection.

## Phase 3 — Repair face identity transfer

- [ ] Constrain original-face projection to a geometrically defined facial region rather than a broad front-facing head mask.
- [ ] Align the authoritative face artwork to the canonical head using stable landmarks/bounds rather than canvas-relative assumptions.
- [ ] Preserve skin/hair separation around forehead, temples, ears, jaw, and neck.
- [ ] Eliminate obvious center/front seams and abrupt transitions from face artwork to projected head texture.
- [ ] Ensure eyes, brows, nose, mouth, and skin tone remain recognizable at gameplay camera distance.
- [ ] Add a face-specific regression image or metric that fails when facial sampling escapes the intended source region.

## Phase 4 — Geometry and rig verification

- [ ] Confirm the base mesh contains body + hair only and no robe, cape, boots, staff, armor, jewelry, book pouch, or other equipment geometry.
- [ ] Confirm the temporary fitted modeling-layer cues do not survive as clothing-like geometry.
- [ ] Confirm Madeline retains the approved shorter/compact silhouette after canonical alignment.
- [ ] Check shoulder, elbow, wrist, hip, knee, ankle, neck, and hair-adjacent weighting for obvious deformation problems.
- [ ] Pass the skinned-character deformation verifier.
- [ ] Verify canonical skeleton names, bind pose, mesh skin weights, and root transforms remain compatible with the Unity importer.

## Phase 5 — Animation proof

- [ ] Render and inspect bind pose.
- [ ] Render and inspect Idle.
- [ ] Render and inspect Walk.
- [ ] Render and inspect Run.
- [ ] Render and inspect Cast.
- [ ] Render and inspect StaffAttack.
- [ ] Confirm arms no longer read as flat ribbons in relaxed/animated poses.
- [ ] Confirm face/hair texture remains stable under animation and does not reveal severe stretching or seams.

## Phase 6 — Automated acceptance gates

- [ ] Extend `verify_madeline_base_contract.py` (or the appropriate verifier) with projection-specific checks that detect the baseline smear failure class.
- [ ] Add checks for excessive texture-coordinate discontinuity / narrow repeated strips where measurable.
- [ ] Add checks that projected samples stay within subject masks and expected image bounds.
- [ ] Keep the existing no-equipment, skinned-mesh, and non-flattened-geometry requirements intact.
- [ ] Ensure failed visual/projection verification prevents `madeline-base-body` artifact publication.
- [ ] Preserve failed diagnostics as workflow artifacts so the next failure can be inspected without rerunning expensive reconstruction.

## Phase 7 — Final artifact and Unity staging

- [ ] Run the full Madeline base-body workflow from the repair branch.
- [ ] Publish a successful `madeline-base-body` artifact containing the repaired model, textures, manifest, diagnostics, and proof renders.
- [ ] Compare final bind/idle renders side-by-side against run #48 and the approved turnaround/reference face.
- [ ] Confirm visual acceptance: compact proportions, straighter/less-curly hair, recognizable face, coherent body color, no large projection smears, no clothing baked into the base.
- [ ] Stage the verified Madeline base into Unity through the existing Character Factory bridge.
- [ ] Run Unity EditMode importer/catalogue/prefab validation against the repaired asset.
- [ ] Compose the separate Cleric robe/cape and Sun Staff over the repaired base to verify modularity was preserved.
- [ ] Render a final modular Cleric composition for review.

## Completion discipline

- A task is checked only after there is evidence.
- Every code-fix commit should update this checklist in the same commit or in the immediately following evidence commit.
- When a hypothesis is disproven, record the result in this document rather than silently deleting the task.
- Expensive CI runs should be triggered only after cheap deterministic checks and local/static inspection pass.
- The final acceptance decision is visual as well as automated: a green workflow is not sufficient if Madeline still visibly contains projection corruption.

## Current status

The generation/reconstruction and canonical-rig pipeline are functional. Projection instrumentation is now durable, the four source orientations are verified, and the side-view mirroring bug is fixed. The next repair must replace unbounded nearest-foreground snapping with candidate-view validation and bounded rejection/fallback, with special attention to outer T-pose arms and long hair. Run #67 is an improvement over run #66 but remains visually unacceptable and is not an acceptance candidate.
