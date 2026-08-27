# Plan — generated-house vertical circulation

## Observed defect / acceptance
The capture has no circle annotations, so the saved VoxelShowcase pose and note define one defect: generated multi-storey stairs are not structurally coordinated with the floor above. The saved camera is at `(-414.095, 9.142, -321.975)` and the note specifically calls for constraints that produce a real ceiling/floor opening and upper-floor guarding. Acceptance is a bounded stair run whose rise equals one storey, a slab opening derived from stair/headroom geometry, and guards on the non-egress edges; single-storey houses must stay unchanged.

## Competing hypotheses / discriminator
1. **Missing circulation integration.** Supported: `HouseProgramCompiler` emits every intermediate floor as a full solid slab; `KentridgeHouseInteriorPropCatalogue` adds furniture only. The repo already owns `StairConfig`, `StructureStairAuthoring`, and semantic `InteriorConnectionKind.Stairwell`, but the Kentridge generated-house production program never consumes them. Falsifier: an evaluated production program already containing a stair-aligned slab carve and upper guard.
2. **Placement/orientation bug.** Rejected as primary: the local program lacks those circulation primitives before cardinal placement is applied.
3. **Stale bake/capture.** Still possible for presentation only; the final saved-pose real-player replay is the discriminator after the production regression is green.

## Selected fix / regression
Add a small Kentridge generated-house circulation composer that uses shared `StairConfig` constraints. Storey rise determines step count/rise/run; required headroom determines the opening start; alternating flights share a bounded shaft; upper floors receive side guards plus a guard on the non-egress edge. Compose it after furniture so the final carve wins over the slab and stair/guard fills win after the carve.

Add a PlayMode regression through `KentridgeSharedStructureVoxelCatalogue.Build` + `ShapeProgram.Evaluate`. For every generated multi-storey role it requires an intermediate-slab carve, multiple rising stair treads intersecting that opening, upper-floor guards, successful evaluation, and the existing `MaxPrimitives=256` budget. It also proves generated single-storey roles receive no circulation slab carve.

## Blast radius / cost / remaining gates
Scope is generated Kentridge houses only; bespoke roles and one-storey generated houses are unchanged. Cost is bounded by `(storeys-1) * (stepCount + 4)` box/carve primitives; the production budget regression catches excess. Current base is `94d390cac3fda5199a87033e2cae5bbd5f65287f`. Remaining gates: exact targeted PlayMode CI from the feature head, artifact/log inspection, saved-pose replay, `verification-final.png`, metadata, close, merge current master, non-force master advance.
