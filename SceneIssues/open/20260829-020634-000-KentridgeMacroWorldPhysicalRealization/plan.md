# Plan

## Observed gap / acceptance
- The existing Kentridge macro-world issue proves semantic topology and shows it in a debug overlay, but most remote destinations are still only neutral markers in the 3D world.
- Closure requires the physical built world to realize every settlement node, continuous inter-settlement roads/trails, and large geographic forms that participate in routing.
- The minimap/overlay is instrumentation only and cannot satisfy the visual gate.

## Architecture / likely owners
- Extend the existing `TopDownWorldLayout` / `MountingForceTopDownWorldDefinition` through shared WorldBuilder composition; do not create a second scene-local graph.
- Add reusable large-scale region/geography intent for water bodies, mountain/ridge barriers, valleys/passes, plains/meadows, forests/woodlands, extents/elevation, relationships, and route/placement constraints.
- Make road solving terrain-aware and make settlement envelopes generate reusable blockouts. Existing detailed Kentridge/Hightown output must override/preserve richer content rather than being replaced.
- Keep ecology species/density separate: this issue defines regional geography and exclusions that the vegetation policy can consume.

## Validation gates
- Behavioral regressions for deterministic macro-to-physical realization, every-settlement reachability, continuous roads, blockout non-overlap/grounding, geographic constraints, and rejection of an impossible blocked hard route without an explicit crossing/pass solution.
- Exact-SHA built-player `KentridgePlayableSlice` with no startup/runtime exceptions.
- Durable visual evidence for roads entering/leaving settlements; physical blockouts at every settlement node (including Moordell, Rossdam, Fairy Village, Orc Village); at least one substantial lake and one mountain/ridge; and a road visibly routed around/through/across constrained geography.
- Representative real CharacterMotor traversal with collision and streaming active; elevated/survey evidence must show physical geography matching the semantic network.
- Measure world-build, route-solving, CPU/GPU, memory, voxel/brick, streaming, and far-field cost against existing device budgets.
