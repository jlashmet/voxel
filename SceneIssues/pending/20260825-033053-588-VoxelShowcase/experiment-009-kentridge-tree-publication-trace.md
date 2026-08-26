# Experiment 009 — Kentridge tree publication trace

## Hypothesis
The trees visible from the saved capture camera are Kentridge-authored vegetation, and they are non-interactive because `ShowcaseTreePopulation` publishes only castle vegetation into the runtime `TreeWorld`.

## Action
- Re-synced `fixes/agent-6` with current `origin/master` before continuing.
- Traced the exact capture camera (`z=154.718`, looking strongly toward +Z) against the semantic tree publishers and world-generation vegetation adapters.
- Confirmed `ShowcaseTreePopulation` builds only `CastleVegetationPlanner` instances around the castle and then replaces the runtime tree world with that list.
- Confirmed `KentridgeVegetationPlanner` separately converts Kentridge semantic vegetation candidates into real `TreeInstance` objects and exposes both resident-storage (`TryBuild`) and deterministic analytic (`BuildAnalytic`) realization paths.
- Confirmed far-terrain/far-LOD code consumes semantic tree state rather than defining another independent interactable tree population.

## Result
The prior exact-camera replay found none of the 36 castle semantic trees in view. The source trace identifies a second authored tree population for Kentridge that is capable of producing runtime `TreeInstance`s but is not merged by the VoxelShowcase tree publisher. This matches the capture location and the observed symptom pair: visible trees that neither receive tree damage nor block the player through semantic wood collision.

## Conclusion
Supported. The next experiment should feed Kentridge's deterministic analytic `TreeInstance`s through the exact saved camera selector. If those trees are visible/shootable from the capture, update `ShowcaseTreePopulation` to publish both castle and Kentridge instances from resident storage, then rerun the exact captured-view regression.
