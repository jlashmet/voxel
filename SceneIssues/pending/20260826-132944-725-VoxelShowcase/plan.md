# Plan — generated-house vertical circulation

## Observed defect / acceptance
The capture has no circle annotations, so the saved VoxelShowcase pose and note define one defect: generated multi-storey stairs were not structurally coordinated with the floor above. The saved camera is at `(18.664, 28.850, 38.962)` with FOV 70. Acceptance is a bounded stair stack whose rise equals each storey, a slab opening derived from stair/headroom geometry, and guards on non-egress upper-floor edges.

## Competing hypotheses / discriminator
1. **Missing circulation integration.** Confirmed: `HouseProgramCompiler` emitted each intermediate floor as a solid slab; Kentridge decoration added furniture but no circulation contract. The repo already owned `StairConfig`, `StructureStairAuthoring`, and `InteriorConnectionKind.Stairwell`, but generated houses did not consume them.
2. **Placement/orientation bug.** Rejected as primary because the evaluated local production program lacked circulation primitives before cardinal placement.
3. **Stale bake/capture.** Rejected by the final exact-SHA real-player saved-pose replay.

## Fix / regression
Compose a constrained switchback stairwell after furniture. Storey height determines step count/rise/run; required headroom determines the slab opening; return flight and landing share the bounded shaft. The stair sits in the front half opposite the authored door bias, avoiding the rear furniture zone. Upper floors receive guards while the return-flight egress remains open.

`VoxelEngine.Tests.PlayMode.KentridgeHouseVerticalCirculationPlayModeTests.ProductionGeneratedHousesCoordinateStairsOpeningsAndUpperGuards` executes production catalogue build + `ShapeProgram.Evaluate` across all 13 generated Kentridge roles and checks intermediate-slab carves, rising treads through the opening, upper guards, successful evaluation, and `MaxPrimitives=256`.

## Blast radius / cost / gates
Bespoke roles and shared engine opcodes are unchanged. Added work is bounded per floor transition to `stepCount + 7` box/carve primitives with no runtime search/cache. Source under test: `1209a81b3681b11d694c805b58a51e06c7851748`; exact CI request `a4c89c3b0b5430f7e3c24cc76c0ff21eb1ae57c8` passed 1/1 and its 45-second real-player replay completed with zero assertion failures. Final replay evidence is committed as `verification-final.png`.
