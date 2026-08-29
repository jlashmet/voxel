# Plan

## Observed defect / acceptance
- Human review rejected the prior closure: the built VoxelShowcase showed a floating red square but no visually substantial mountain or readable winding ascent.
- Closure requires durable built-application captures from the normal approach, path entrance, representative switchbacks, and summit; the full route must be walked normally, and normal proximity must display `Hello, I'm Mr. Dragon.`
- The checked-in startup bake must contain the accepted result; structural/catalogue assertions and a crash-free launch are supplemental only.
- The issue also requires cutscene/dialogue UI behavior to live in reusable shared modules rather than scene-local showcase code.

## Competing hypotheses / discriminator
1. **Stale checked-in startup asset.** The checked-in VoxelShowcase bake predates the mountain feature, while bake loading marks its regions generated and therefore prevents the authored mountain from being realized at runtime.
2. **Runtime realization/render defect.** Current authored primitives exist, but production realization may omit/overwrite the mountain or fail to publish it visibly.
3. **Authored geometry defect.** The source mountain/path might itself be too small, steep, disconnected, or unsupported for normal traversal.

Discriminator: compare bake provenance against the mountain landing, inspect current authored dimensions/path topology, then validate a current-source bake in the exact built application.

## Material results
- `Assets/Resources/VoxelShowcase/ShowcaseWorld.bytes` was last refreshed Aug 25; the mountain feature landed Aug 28. The checked-in startup bake is definitively stale and hypothesis 1 is confirmed for checked-in launches.
- `GenerateForBakeBlocking` directly authors source content and does not restore the runtime bake, so the editor baker's default startup-source enum is not the cause of stale-image reuse. The failure is that the newly authored world was never promoted into the checked-in startup asset.
- Current authored geometry is already substantial and traversal-oriented: 100 m base diameter, 28 m rise, 8 m summit radius, 3 m path width, 36 m alternating runs with 4.6 m rises and explicit supported landings. Hypothesis 3 is not supported by source inspection; exact built-player traversal remains mandatory validation.
- The existing structural regression verifies shallow connected primitives and story/proximity semantics, but it does not prove those primitives exist in the checked-in bake or rendered built application.
- `MountainDragonShowcaseDriver` still renders dialogue in scene-local `OnGUI`, which violates the issue's reusable cutscene/UI boundary even though proximity, campaign rules, and cutscene execution already use shared modules.

## Selected fix / remaining gates
- Add a durable startup-bake content/freshness invariant tied to required production landmarks, refresh the checked-in bake from current WorldBuilder output, and add storage/bake-level regressions that verify mountain/path/summit/dragon occupancy.
- Route dialogue rendering through a reusable shared cutscene presentation component and leave the showcase driver as a thin player-coordinate/composition adapter.
- Preserve WorldBuilder/Story/Campaign/Cutscene boundaries; quantify one-time world-build/bake cost plus steady-state trigger/presentation cost and inspect blast radius.
- Then exact-SHA built-player normal-movement traversal, required captures, human AAA review, final targeted CI, metadata/pending/closed workflow, latest-master merge, and non-force master push.
