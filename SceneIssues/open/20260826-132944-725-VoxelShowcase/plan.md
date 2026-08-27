# Plan — generated-house vertical circulation

## Observed defect / acceptance
The capture has no circle annotations, so the saved VoxelShowcase pose and note define one defect: generated multi-storey stairs are not structurally coordinated with the floor above. The saved camera is at `(-414.095, 9.142, -321.975)` and the note calls for constraints that produce a real ceiling/floor opening and upper-floor guarding. Acceptance is a bounded stair stack whose rise equals each storey, a slab opening derived from stair/headroom geometry, and guards on non-egress edges.

## Competing hypotheses / discriminator
1. **Missing circulation integration.** Supported: `HouseProgramCompiler` emits every intermediate floor as a full solid slab; the Kentridge production pass adds furniture only. The repo already owns `StairConfig`, `StructureStairAuthoring`, and semantic `InteriorConnectionKind.Stairwell`, but generated houses never consume that circulation contract. Falsifier: an evaluated production program already containing a stair-aligned slab carve and upper guard.
2. **Placement/orientation bug.** Rejected as primary: the local program lacks circulation primitives before cardinal placement.
3. **Stale bake/capture.** Possible only for presentation; final saved-pose real-player replay is the discriminator after the production regression is green.

## Selected fix / regression
Compose a constrained switchback stairwell after furniture. Storey height determines step count/rise/run; required headroom determines where the first-flight slab opening begins; the return flight and landing share the bounded shaft. The stair sits in the front half on the side opposite the authored door bias, avoiding the rear furniture zone. Upper floors get side/north guards while the return-flight egress remains open.

The PlayMode regression executes `KentridgeSharedStructureVoxelCatalogue.Build` + `ShapeProgram.Evaluate` for all 13 current generated Kentridge roles. It requires intermediate-slab carves, a rising tread sequence intersecting the opening, upper guards, successful evaluation, and the existing `MaxPrimitives=256` budget. All current generated consumers are 2–3 storeys; the composer still returns unchanged input for any future one-storey generated form.

## Blast radius / cost / remaining gates
Bespoke roles are untouched; shared engine compiler/opcodes are unchanged. Added work is bounded per floor transition to `stepCount + 7` box/carve primitives, with no runtime search/cache. Current source head is `0350855ef52abbfc2a79b5f9d0ec0a4a7f8b22c6`. Remaining gates: exact targeted PlayMode CI from final feature head, artifact/log inspection, saved-pose replay, `verification-final.png`, metadata, close, merge current master, non-force master advance.
