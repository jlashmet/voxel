# Madeline Character Factory Progress

This checklist is the source of truth for the production Madeline base-character work. Mark an item complete only after the repository/build output proves it.

## Reference and modular design

- [x] Select the approved Madeline four-view turnaround.
- [x] Preserve the original Madeline face artwork as the authoritative identity reference.
- [x] Define the base character as body + hair only; keep robe, cape, boots, armor, jewelry, book pouch, staff, and other equipment out of the base asset.
- [x] Preserve Madeline's shorter, compact silhouette during canonical-rig alignment.
- [x] Add preprocessing that removes the temporary fitted modeling-layer cues before multiview reconstruction.
- [x] Add an explicit precleaned right-side override for the anomalous source view.
- [x] Re-encode/recover the approved turnaround inputs into valid JPEGs before preprocessing.

## Production base-body build

- [ ] Complete Hunyuan multiview reconstruction of the clothing-free Madeline body.
- [ ] Transfer the generated body to the canonical gameplay skeleton.
- [ ] Project the approved four-view body/hair appearance onto the rigged body.
- [ ] Project the original Madeline face artwork onto the final head.
- [ ] Pass the skinned-character deformation verifier.
- [ ] Pass verification for Idle, Walk, Run, Cast, and StaffAttack.
- [ ] Render the bind-pose lookdev proof.
- [ ] Render the animated Idle lookdev proof.
- [ ] Upload the `madeline-base-body` build artifact.
- [ ] Stage the verified Madeline base into Unity.

## Pipeline verification wiring

- [x] Add an automated Madeline base-contract verifier that rejects named Cleric clothing/equipment, unskinned rigid mesh content, and obviously flattened geometry.
- [x] Add an automated complete-loadout verifier for a character + separate skinned clothing + rigid weapon socket.
- [x] Wire the Cleric modular-composition workflow to consume the generated Madeline base rather than the generic canonical mannequin.
- [x] Wire the Cleric modular-composition workflow to use the separate generated robe and separate Sun Staff.
- [x] Make downstream composition accept a verified Madeline artifact even if only the later review-publication step of the expensive base workflow fails.
- [x] Diagnose the original robe workflow timeout as model-cache bootstrap/download time rather than robe reconstruction.
- [x] Rewire the robe workflow to reuse the same persistent Hunyuan multiview cache as the Madeline production build.
- [x] Restrict Hunyuan cache bootstrap to the exact multiview-turbo checkpoint/config instead of downloading the entire multi-checkpoint repository.
- [x] Allow the first shared Hunyuan cache fill enough CI time to complete and be reused by later character/robe builds.

## Unity runtime integration

- [x] Add stable `partId` catalogue lookup for runtime-switchable character parts.
- [x] Add an equipment controller that equips/unequips catalogue parts through the existing modular-character assembler.
- [x] Stage completed Character Factory manifests into Unity as FBX + portable `.characterfactory.json` import descriptors.
- [x] Auto-import staged equipment descriptors into the shared `CharacterPartCatalogue` without manual Inspector setup.
- [x] Auto-create a generated character prefab wired to the shared catalogue, canonical skeleton root, and dedicated equipment root.
- [x] Configure staged FBX animation import mode automatically for character, clothing, and rigid weapon assets.
- [x] Preserve clothing skeleton-rebind metadata and rigid weapon socket metadata through the staging/import bridge.
- [x] Preserve generated weapon socket local position/rotation/scale and apply it when equipping the weapon.
- [x] Add Unity EditMode contract coverage for descriptor-relative FBX resolution, safe catalogue paths, and generated socket-transform metadata.
- [x] Add concurrency cancellation to the modular-equipment validation workflow so future superseding runtime commits do not keep piling up redundant runs.
- [ ] Pass the latest Unity EditMode catalogue/equipment-controller/importer validation workflow.

## Visual acceptance

- [ ] Confirm the final mesh does not contain robe/cape/boots/staff/accessory geometry.
- [ ] Confirm the temporary beige modeling layer does not read as clothing in geometry or texture.
- [ ] Confirm the final proportions still read as the approved shorter/compact Madeline silhouette.
- [ ] Confirm the face reads as the approved Madeline face without an obvious frontal/center seam.
- [ ] Confirm hair shape remains consistent with the approved straighter/less-curly reference.

## Modular composition

- [ ] Produce a verified `sunlit-cleric-modular-robe` artifact from the repaired robe workflow.
- [ ] Compose the verified Madeline base with the separate Cleric robe/cape asset.
- [ ] Compose the separate Sun Staff weapon through the hand socket.
- [ ] Verify clothing can be switched without changing/rebuilding the Madeline base mesh.
- [ ] Render a final modular Cleric composition for review.

## Current builds

- Madeline run #28 (`31931575922`) proved the repaired reference preprocessing path, then spent the remainder of its 90-minute window downloading the full Hunyuan multiview repository and was cancelled before reconstruction.
- The Hunyuan bootstrap now downloads only `hunyuan3d-dit-v2-mv-turbo/config.yaml` and `model.fp16.safetensors` into the persistent self-hosted-runner cache.
- Madeline base-body workflow: run #29 (`31936579943`) from `a4046b503f3edf3644d228168a81fa5f750d74fb`, currently queued for the self-hosted macOS runner.
- Repaired robe workflow: run #3 (`31936661842`) from `1f8f16596702b8f94458ef0f3ccc5a691d03d6de`, currently using the self-hosted macOS runner in the shared turbo-checkpoint cache-fill step.
- Latest Unity catalogue/equipment/importer validation: run #11 (`31937528878`) from `00242daafba440d5af03e97ba8ec49ec0bc9a3d3`, currently pending behind the same self-hosted runner workload. Superseded validation runs are being cancelled by the workflow concurrency group.
- Current branch: `feature/character-weapon-asset-pipeline`.
