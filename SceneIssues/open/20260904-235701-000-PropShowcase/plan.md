# PropShowcase plan

## Acceptance and ownership
Browse every independently previewable production prop exactly once, render its production realization, retire prior state, and prove useful framing/materials/contact plus bounded switching through exact standalone-player evidence. Only production-quality visuals pass; no gate or checkbox is waived.

Canonical set remains 529 entries: 440 registered decorations, 25 presets, 8 mine-cave kinds, 8 natural-cave kinds, 48 world-object kinds. Structures owns enumeration/shared presenters; SceneRuntime owns browser/resource orchestration; Materials owns procedural-material composition. Each runtime owner has a local validation surface. Top-level showcase scenes are integration consumers only.

## Current source and material results
Current feature source `c52db27801214a00e6284a9b8ed01fd27118a4bc` replaced Forge Hearth's mostly-solid voxel grammar with a grounded plinth, firebox frame, fire bed and attached chimney. Exact request `8319b86cdaa74ca82f4b0f4dc90fe5c90c8d3e6f` / run `34015677843` passed every automated phase, but direct standalone-player review still rejects Forge Hearth: the semantic-front capture reads as a solid masonry block with fire/effect behind or above it rather than an open grounded firebox.

This is the same acceptance symptom after two materially different fixes, so no third visual tweak is allowed before root-cause isolation. Hypothesis A: canonical Hearth authoring ignores `DecorationPlacement.Facing`; `AuthorShape` receives facing, but `AuthorHearth` hard-codes its open side toward world `-Z`, while Smithy placement logic defines `Facing` as the working/front direction. Hypothesis B: authoring creates the correct aperture but later voxel publication/meshing obscures it. The next discriminator is a focused production-authoring regression that records authored boxes for cardinal facings and proves whether the aperture follows `Facing` before publication.

## Selected architecture and prior fixes
Shared production paths provide framed painting-family thin surfaces, detailed Door/SecretDoor/Trapdoor mechanism meshes, semantic decoration light/particle presentation, corrected horizontal Trapdoor baseline, truthful voxel `LOADING`→`READY`, integration-only top-level scene CI planning, and per-scene artifact isolation. Forge Hearth remains unresolved pending the facing discriminator.

## Remaining gates
Prove the repeated Hearth root cause, apply only the proven production correction, and add the focused regression. Then issue a new exact-SHA request on `ci-test/fixes/agent-9`, inspect module-local and SceneIssue standalone captures directly, and verify Sign, Door/Trapdoor, Hearth/effects, readiness, representative framing, switching and three-cycle resources. Complete all tasks/metadata, move open→closed, merge current master, open/update the final PR, enable auto-merge and pass `affected` until the closed state is visible on master.
