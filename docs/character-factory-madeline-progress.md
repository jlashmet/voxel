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

## Visual acceptance

- [ ] Confirm the final mesh does not contain robe/cape/boots/staff/accessory geometry.
- [ ] Confirm the temporary beige modeling layer does not read as clothing in geometry or texture.
- [ ] Confirm the final proportions still read as the approved shorter/compact Madeline silhouette.
- [ ] Confirm the face reads as the approved Madeline face without an obvious frontal/center seam.
- [ ] Confirm hair shape remains consistent with the approved straighter/less-curly reference.

## Modular composition

- [ ] Compose the verified Madeline base with the separate Cleric robe/cape asset.
- [ ] Compose the separate Sun Staff weapon through the hand socket.
- [ ] Verify clothing can be switched without changing/rebuilding the Madeline base mesh.
- [ ] Render a final modular Cleric composition for review.

## Current build

- Madeline base-body workflow: run #28 (`31931575922`).
- Branch at build start: `feature/character-weapon-asset-pipeline` @ `16292d58ee3e0f18574ffee87e776df1a47b2f8c`.
- Status when this checklist was created: build step running on the self-hosted macOS runner.
