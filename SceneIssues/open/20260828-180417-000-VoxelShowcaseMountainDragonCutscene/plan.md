# Plan

## Observed defect / acceptance
- Human review rejected the prior closure: the built VoxelShowcase showed a floating red square but no visually substantial mountain or readable winding ascent.
- Closure requires durable built-application captures from the normal approach, path entrance, representative switchbacks, and summit; the full route must be walked normally, and normal proximity must display `Hello, I'm Mr. Dragon.`
- The checked-in startup bake must contain the accepted result; structural/catalogue assertions and a crash-free launch are supplemental only.

## Competing hypotheses / discriminator
1. **Stale startup bake.** The checked-in VoxelShowcase bake predates the mountain feature, while bake loading marks regions generated and suppresses feature realization for those regions.
2. **Runtime realization/render defect.** Current authored primitives exist, but production realization may omit/overwrite the mountain or fail to publish it visibly.

Discriminator: compare bake provenance against the mountain landing, then validate a current-source bake in the exact built application. If the stale asset predates the feature and a current-source bake renders the supported mountain/path, hypothesis 1 wins; if a current-source bake still lacks it, inspect runtime occupancy/realization.

## Material results
- `Assets/Resources/VoxelShowcase/ShowcaseWorld.bytes` was last refreshed Aug 25; the mountain feature landed Aug 28. The checked-in startup bake is therefore definitively stale and hypothesis 1 is confirmed for checked-in launches.
- A later successful VoxelShowcase CI run generated/cached a current-source bake and launched the player cleanly, so current source can bake/run; that artifact did not export the generated `.bytes` for reuse.
- Prior source `0168881859b560e75033e993809480f512916fae` passed structural assertions but did not prove the required rendered route or normal player-triggered dialogue.

## Selected fix / remaining gates
- Repair the startup-bake path without scene-local voxel/mesh/polling workarounds, add a regression that proves the shipped bake/world contains the authored mountain and normal proximity flow, and add only reusable evidence/navigation support needed to traverse/capture it.
- Preserve WorldBuilder/Story/Campaign/Cutscene boundaries; quantify one-time world-build cost plus steady-state trigger cost and inspect blast radius.
- Then exact-SHA built-player traversal, required captures, human AAA review, final targeted CI, metadata/pending/closed workflow, latest-master merge, and non-force master push.
