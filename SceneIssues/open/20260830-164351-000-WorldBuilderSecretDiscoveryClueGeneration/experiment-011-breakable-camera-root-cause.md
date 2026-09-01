# Experiment 011 — breakable camera root cause

## Symptom

Exact-SHA run `33506791733` passed focused, module, and standalone gates for feature `98f5ff7bab04f7e2f3bbd94027f196c6b2a1bcc0`, but the full-resolution Gallery capture `02-authored-breakable-boundary.png` is visually unacceptable: the camera is below/inside solid terrain and the authored false wall is not visible.

The same breakable-frame symptom survived materially different earlier framing work and the later surface-route fix, so another visual tweak is not justified without a geometry discriminator.

## Competing hypotheses

1. The cave/pocket was authored incorrectly or the retained barrier is missing.
2. The voxel renderer cannot present the authored underground cavity.
3. Scene acceptance camera policy moves a valid authored-tunnel camera outside guaranteed carved space.

## Evidence

- Runtime acceptance completed with authored clue evidence and no authoring failure: `boundaryClueVoxels=318`, `naturalClueVoxels=30`.
- The captured breakable target was `(-137.60, 18.50, 19.25)` metres and the final camera was `(-137.05, 18.92, 22.40)` metres.
- `CaveSecretPocketAuthoring` retains the barrier immediately *forward* of the selected terminal and carves only the connector/pocket beyond it.
- The barrier center implies the selected deterministic terminal is approximately `(-1376, 175, 194)` voxels facing South. The production Gallery helper places its eye `17` voxels *behind* that terminal, at about z=211, which is inside the final cave segment: production cave segments are 18 voxels long and a south-facing final segment approaches the terminal from the north.
- `WorldbuildingGallerySecretDiscoveryAcceptance` then normalizes the helper-to-barrier vector and forces a `3.15 m` stand-off. That moves the camera to about z=224, roughly 30 voxels behind the terminal — beyond the only final segment the terminal itself proves is carved. The extra +0.25 m height/+0.55 m lateral offset does not restore that topology guarantee.
- The resulting full-resolution frame shows the expected consequence of placing a camera in non-carved terrain: surface/vegetation undersides and blue void, not a cave wall.
- The natural surface-mouth frame in the same exact-SHA run renders a real cave opening, so a blanket inability to render cave surfaces is not supported by this evidence.

## Discriminator / root cause

Hypothesis 3 is supported. The presentation layer discarded the topology guarantee already encoded by `WorldbuildingGalleryBreakableSecretCameraPosition()` in order to satisfy an arbitrary 3 m framing distance. A cave terminal guarantees the just-authored final segment behind it, not 3+ metres of straight tunnel behind it (the previous segment may have turned).

## Minimal correction

Use the production Gallery helper position directly and lower the acceptance-only framing-distance guard to fit the guaranteed 18-voxel terminal segment. Do not change cave generation, pocket authoring, renderer APIs, or acceptance semantics.

Validation must still prove the resulting full-resolution authored-breakable capture visibly presents the retained/clued false wall before closure.
