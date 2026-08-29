# Plan

## Reopen / acceptance
- Original ask: carry the tuned ArchLookdev hero arch into production Kentridge.
- Prior closure was rejected because its durable visual proof came from `ArchLookdev`, not `KentridgePlayableSlice`. Final proof must be the built Kentridge player at normal player height, with recognizable building context and readable segmented projecting voussoirs.
- Original capture had no marked circles; the defect is whole-frame integration/readability. Its recorded dimensions were 1928x836. The reopened ticket intentionally clears the ArchLookdev pose and uses semantic Warehouse role 14 instead.

## Hypotheses / discriminator
1. **Presentation failure — supported.** Warehouse resolves public exterior/entrance/interior access and is traversable through the production `KentridgeCharacterHost`; master nevertheless emits twelve zero-radius `MasonryJoint` capsules, so count-based coverage can pass while the segmentation disappears visually.
2. **Composition/access failure — falsified for Warehouse.** Deterministic access data exposes a reachable production entrance.

## Selected fix / regressions
- Keep the reusable `FramedArchedOpening` path and both clearance carves; retain twelve bounded hero joints but give them a minimum 1 dm radius. `FramedArchedGlazedOpening` remains continuous, so window-scale arches do not inherit the landmark treatment.
- `KentridgeInteriorScaleTests.ProductionCatalogue_LandmarkEntrancesCarryReadableHeroVoussoirJoints` rejects zero-width joints and treatment spillover.
- `KentridgeHeroArchPlayableSceneTests.GeneratedWarehouseHeroArch_IsReachableThroughProductionPlayerHost` loads the exact scene and physically drives the production player host from the generated public approach through the entrance.
- Ticket-gated `KentridgeLandmarkEvidenceHarness` stages only this capture at the semantic Warehouse approach for built-player screenshots; ordinary gameplay/other SceneIssues are unchanged.

## Blast radius / cost
- Only landmarks already opting into `FramedArchedOpening` change. Glazed/window arches and unrelated settlement programs remain unchanged.
- Primitive count is unchanged: twelve surface-detail capsules per hero entrance before and after. Cost increase is limited to the narrow voxel footprint of radius 1 dm joints; no new runtime loop or per-frame production work is added.

## Current source / remaining gates
- Replay metadata corrected at `935f9f31011ddf02dfa54d3288c7915cc56c8b1f`; discriminator evidence commit is `32f75938fc1e067da21356be43f8abb1da3fbd2a` and the feature is refreshed to current master with no unrelated SceneIssue/workflow/request-file diff.
- Remaining: run one exact-SHA final targeted request, inspect green regression + built-player Kentridge artifacts/screenshots, then pending metadata/move, final closed metadata/move, refresh master, and push exact head non-force.
