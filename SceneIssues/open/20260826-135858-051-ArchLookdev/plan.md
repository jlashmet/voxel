# Plan

## Reopen reason
- Original ask: carry the tuned ArchLookdev hero arch into Kentridge.
- Prior closure proved catalogue/program behavior, but durable visual evidence came from `ArchLookdev`, not the production `KentridgePlayableSlice`. The user could not find/read the result in Kentridge.

## Evidence and hypotheses
- The exact production slice builds `KentridgeCombinedVoxelCatalogue` from the deterministic `SettlementPlan`; landmark roles are therefore realized through the reusable WorldBuilder path, not scene-only objects.
- Warehouse, mansion, and church entrance programs call `ArchitectureVoxelPatterns.FramedArchedOpening`.
- That helper currently adds twelve zero-radius `PrimitiveMode.SurfaceDetail` / `MasonryJoint` capsules around a continuous arched frame. Existing regression checks those operations, so it can pass even if the player cannot visually read segmented stones.
- The production slice already exposes deterministic landmark survey coordinates/frontage from `_kentridgePlan`, so the landmark itself is locatable.

Competing hypotheses:
1. **Presentation failure:** landmark placement/access is valid, but zero-radius surface-detail seams are too subtle at normal gameplay distance; the arch exists logically yet does not read like the projecting segmented ArchLookdev treatment.
2. **Composition failure:** the treatment is visually adequate, but landmark frontage/public approach leaves it practically unreachable or hidden.

Smallest discriminator: load the exact playable scene, resolve one generated landmark entrance/frontage from the production settlement plan, physically move the production player host to its public approach, and inspect/frame it at player eye height. Verify the same generated program carries the hero segmentation.

## Fix / regression
- If hypothesis 1 wins, strengthen only `FramedArchedOpening` hero entrance decoration into durable shallow projecting voussoir geometry while preserving both opening carves; keep glazed/window arches unchanged.
- If hypothesis 2 wins, correct only reusable Kentridge planning/access/presentation—never add a scene-local showcase object.
- Add a PlayMode regression through `KentridgePlayableSlice` that proves a deterministic landmark hero entrance is generated, connected to public circulation, reachable by the production player host, and remains body-clear.

## Gates
1. Targeted PlayMode regression green.
2. Exact built `Assets/Scenes/KentridgePlayableSlice.unity` replay at player height with durable wide-context and close arch screenshots.
3. Confirm no window/unrelated-settlement spillover and report primitive/runtime cost delta.
4. Final targeted exact-SHA CI on `ci-test/fixes/agent-5`; then pending/closed metadata and non-force master integration.
