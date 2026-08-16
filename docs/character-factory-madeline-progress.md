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

- Madeline base-body workflow: run #28 (`31931575922`).
- Madeline branch at build start: `feature/character-weapon-asset-pipeline` @ `16292d58ee3e0f18574ffee87e776df1a47b2f8c`.
- Current observed Madeline state: the `Build clothing-free Madeline base character` step is still running on the self-hosted macOS runner; artifact upload and lookdev publication have not started yet.
- Repaired robe workflow: run #2 (`31934273891`) from `d6495fa94603921479e7962253e0c8fd81dc9e59`, currently queued behind the active Madeline build.
- The branch has advanced with downstream verifier/workflow/docs commits, but none of those paths are watched by the Madeline base-body workflow, so run #28 remains the active reconstruction rather than being cancelled/restarted.
