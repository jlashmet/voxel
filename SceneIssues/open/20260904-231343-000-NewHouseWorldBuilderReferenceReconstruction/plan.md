# New House WorldBuilder Implementation Plan

## Binding objective and authoritative reference
Recreate **this particular house image as closely as possible** as real voxel-engine content, using the production WorldBuilder, structure authoring, voxel storage/meshing/rendering, and material/texture systems. Keep rendering, comparing, and correcting until the built-player result is **very close to the reference** and production-quality. A generally similar house or green CI is not completion.

- Reference: `Assets/Textures/Stylized/experiment1/house/10dddef5-de0a-4153-9c09-b1e8016830db.png`.
- Verified Git blob: `6d87b08d4c7c9bddc1705c0f34343aa79bc18423`, present on reviewed master `ef475182b866eabfe8e1d1a39c82bf7810a03f49` and feature baseline `544215794036261c0bfa8f71517e26700d4995ec`.
- Optional supplied textures: `Assets/Textures/Stylized/experiment1/`. Use, adapt, or replace them; creating original/generated textures is explicitly allowed. Register chosen assets through the normal material/texture pipeline with provenance and Unity metadata. Using every supplied texture is not required.

![Authoritative user-specified reference](../../../Assets/Textures/Stylized/experiment1/house/10dddef5-de0a-4153-9c09-b1e8016830db.png)

Never substitute a Library/search result, generated concept, or CI screenshot for this reference, or modify the reference to fit the implementation. Reconfirm any changed reference hash with the user.

## Current result and reset
Previous iterations used the wrong Library image, `3aad3fb3-7a3c-41b4-b87b-f2f72eaa6cda.png`. Their visual-acceptance conclusions are invalid. Reassess geometry, proportions, openings, materials, camera, lighting, and prior N/A decisions against the correct image; reference-dependent tasks are reopened. Existing technical work and CI results are historical regression evidence only, not proof of resemblance. This update changes documentation only; direct inspection of the correct image remains required before further product changes.

## Ownership and constraints
`Assets/Game/WorldBuilder` owns reusable/config-driven house assemblies over existing Structures APIs; reference-specific composition, site, camera, and lighting remain separate. `Assets/Game/Materials` owns material identity/presentation; Rendering receives semantic-free data. Use existing curvature/SDF authoring where appropriate, not a parallel mesh/art stack or a picture pasted over substitute geometry. Preserve authoritative voxel truth, engine scale, and repository budgets.

WorldBuilder's `Validation/NewHouseReferenceReconstruction` is the primary module-local proof; retain affected-module tests and Rendering's `TextureLayers` validation. Material data projections retain unit coverage and production-consumer proof. The supported production CPU fallback remains the current validation path, not evidence that GPU restoration is complete.

## Next experiment and iteration
Hypothesis A: wrong-reference geometry/material decisions dominate the mismatch. Hypothesis B: camera/framing differences compound it. Inspect the pinned image, identify its landmarks/proportions, and compare the existing built-player render before choosing corrections.

Iterate silhouette/proportions, roof/openings, structural detail, materials/textures, then lighting/framing. Each cycle records concrete discrepancies, fixes their production cause, reruns exact-SHA validation, and compares unaltered target-view captures side by side with the pinned reference. Preserve source/run/hash provenance and side/rear audit captures. Isolate the cause after two materially different unsuccessful fixes. Do not stop at recognizable, acceptable-but-improvable, or mechanically green.

## Remaining gates
Complete `tasks.md`, including very-close reference fidelity and production quality; record blockers without lowering acceptance. Then close only this assignment, merge current master into `fixes/agent-5`, open PR, enable auto-merge, and verify `affected` and final merge per `SceneIssues/README.md`. Never use `pending/` or push directly to master.
