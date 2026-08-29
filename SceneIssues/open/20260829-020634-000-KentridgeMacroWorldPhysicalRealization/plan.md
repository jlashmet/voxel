# Plan

## Observed gap / acceptance
- `captures` is empty, so the note is the complete repro/acceptance contract and there are no marked poses to omit.
- The current production macro path is semantic graph -> deterministic grid placement -> terrain-grounded Manhattan road paint + small neutral node markers. It proves topology, not a physical regional world.
- Closure requires every settlement physically realized, continuous terrain-aware hard routes, reusable geographic-region intent/constraints, a substantial lake + ridge, geography-constrained routing, real CharacterMotor traversal, visual built-player evidence, and measured cost.

## Competing hypotheses / discriminator
1. **The existing voxel catalogue only needs larger markers and more road tiles.** Falsifier: if route/settlement placement cannot query water/barriers/slope or express crossings, scaling the current paint pass cannot satisfy blocked-route acceptance. Current code already supports this falsifier: `TopDownWorldVoxelCatalogue` samples only endpoint/tile ground height and does no obstacle/region solving.
2. **The macro graph is sufficient; the missing seam is a reusable physical-plan layer between semantic layout and voxel emission.** Supported so far: the graph/planner/one-shot selection are reusable and authoritative, while physical realization is isolated in `TopDownWorldVoxelCatalogue` and can be generalized without a second graph.

Next discriminator: inspect existing terrain/feature primitives, richer settlement generation ownership, Kentridge real-player harness, and device budgets. Reuse existing terrain/structure contracts where possible; only add new shared region/route/settlement physical-plan contracts where no equivalent exists.

## Selected direction
- Preserve `TopDownWorldLayout` / `MountingForceTopDownWorldDefinition` as topology authority.
- Add reusable macro-region specs/relationships plus a deterministic physical planner that produces region plans, settlement blockout plans, and terrain-aware route plans with explicit crossing/pass semantics.
- Keep Kentridge/Hightown richer generators authoritative; generic settlement blockouts fill only unrealized settlement envelopes.
- Keep scene code limited to selecting semantic intent. Shared WorldBuilder generation emits terrain/roads/blockouts and exposes region queries for later ecology consumers.

## Validation gates
- Production-path regression: deterministic physical plan, all-settlement realization/reachability, continuous route surfaces, non-overlap/grounding, geography-constrained route, blocked-route rejection, richer-settlement preservation.
- Exact-SHA built-player `KentridgePlayableSlice` with durable settlement/road/lake/ridge/constrained-route/survey evidence and representative CharacterMotor traversal.
- Measure planning/build counts and built-player CPU/GPU/memory/streaming/far-field cost against existing budgets; no budget weakening.
- Maintain `tasks.md`; do not promote until every feature and acceptance checkbox is complete.
