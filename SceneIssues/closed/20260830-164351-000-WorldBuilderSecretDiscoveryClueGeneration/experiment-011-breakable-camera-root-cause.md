# Experiment 011 — breakable camera root cause

## Symptom

Exact-SHA run `33506791733` passed focused, module, and standalone gates for feature `98f5ff7bab04f7e2f3bbd94027f196c6b2a1bcc0`, but the full-resolution Gallery capture `02-authored-breakable-boundary.png` was visually unacceptable: the camera was below/inside solid terrain and the authored false wall was not visible.

The same breakable-frame symptom survived materially different earlier framing work and the later surface-route fix, so another visual tweak was not justified without a geometry discriminator.

## Competing hypotheses

1. The cave/pocket was authored incorrectly or the retained barrier is missing.
2. The voxel renderer cannot present the authored underground cavity.
3. Scene acceptance camera policy moves a valid authored-tunnel camera outside guaranteed carved space.

## Evidence

- Runtime acceptance completed with authored clue evidence and no authoring failure: `boundaryClueVoxels=318`, `naturalClueVoxels=30`.
- The earlier captured breakable target was `(-137.60, 18.50, 19.25)` metres and the final camera was `(-137.05, 18.92, 22.40)` metres.
- `CaveSecretPocketAuthoring` retains the barrier immediately *forward* of the selected terminal and carves only the connector/pocket beyond it.
- The barrier center implies the selected deterministic terminal is approximately `(-1376, 175, 194)` voxels facing South. The production Gallery helper places its eye behind that terminal, along the final cave segment.
- The original acceptance layer normalized helper-to-barrier and forced a `3.15 m` stand-off, moving the camera beyond the final segment. That failure supported hypothesis 3 and was corrected.
- Follow-up exact-SHA run `33508045854` for feature `1a30df7a27fe2b1a86a2ebace0b645ee310a27da` passed all automated gates, but the full-resolution breakable audit still showed the world underside/sky void. Its logged camera was `(-137.60, 18.60, 21.10)` with target `(-137.60, 18.50, 19.25)`.
- That follow-up position is only about `1.85 m` from the barrier target and uses the helper's `17`-voxel retreat in an `18`-voxel final segment. This places the eye at the segment's far boundary, where a turned preceding segment and carve endpoint convention do not guarantee interior volume around the camera frustum.
- The natural surface-mouth frame in the same run renders a real cave opening, so a blanket inability to render cave surfaces remains unsupported.

## Discriminator / refined root cause

Hypothesis 3 remains supported, but the first correction was insufficient: it moved the camera from clearly beyond the final segment to the far edge of that segment, not into a safely carved interior. The screenshot proves the edge position is still not a reliable camera volume.

## Minimal correction

Keep cave generation, pocket authoring, renderer APIs, and clue semantics unchanged. Move only the SceneIssue acceptance camera toward the authored barrier from the helper edge position so the eye lies comfortably inside the final terminal segment while retaining a gameplay-scale view. The current implementation interpolates 35% toward the barrier and requires at least `1.1 m` of framing distance.

Validation must still prove the resulting full-resolution authored-breakable capture visibly presents the retained/clued false wall before closure.
