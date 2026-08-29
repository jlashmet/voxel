# Plan

## Observed defect / acceptance
- Human review rejected the prior closure: the built VoxelShowcase showed a floating red square but no visually substantial mountain or readable winding ascent.
- Closure requires durable built-application captures from the normal approach, path entrance, representative switchbacks, and summit; the complete route must be walked without jumps, teleportation, impassable intersections, or missing support, and normal proximity must display `Hello, I'm Mr. Dragon.`
- The checked-in startup bake used by VoxelShowcase must contain the accepted result. Primitive/catalogue assertions and a crash-free launch are supplemental only.

## Competing hypotheses / next discriminator
1. **Stale startup bake.** The checked-in VoxelShowcase bake predates the mountain feature, while bake loading marks regions generated and clears pending feature work.
2. **Runtime realization/render defect.** The catalogue emits a frustum and paths, but the production world may refuse, omit, overwrite, or fail to publish the mountain while still showing the placeholder.

Next: build the exact application from the checked-in state, capture the approach view, and inspect authoritative occupancy plus feature-build diagnostics at the mountain footprint. A visible, supported mountain in that build falsifies hypothesis 1; a refreshed bake with missing mountain occupancy supports hypothesis 2.

## Prior result / remaining gates
- Prior source `0168881859b560e75033e993809480f512916fae` passed structural PlayMode assertions and a generic 30-second scene harness, but no rendered mountain poses were captured; those results did not satisfy visual acceptance.
- Preserve the reusable WorldBuilder/Story/Campaign/Cutscene boundaries and existing performance budgets.
- Remaining: proven fix, focused production regression, refreshed checked-in bake if required, exact-SHA built-player traversal, saved visual evidence at all required views, and human visual approval before pending promotion.
