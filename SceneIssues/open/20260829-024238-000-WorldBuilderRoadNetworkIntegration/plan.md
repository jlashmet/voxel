# Plan

## Observed gap / scope
- The macro-world model can express settlement/region connectivity, and Kentridge has previously generated physical Dirt streets/roads, but there is no single reusable contract carrying a road from semantic world connectivity all the way through terrain deformation, rendering, vegetation, and navigation/travel semantics.
- Existing multi-strip grassy shoulders are useful evidence of the intended visual behavior, but they approximate a continuous road-to-terrain transition with repeated primitives/material bands.
- The open `20260829-020634-000-KentridgeMacroWorldPhysicalRealization` issue is a consumer/integration target for this work; this issue owns the reusable road capability so the macro ticket does not invent a second implementation.

## Recommended architecture
1. **Semantic connection**
   - Extend the existing authoritative top-level world/macro graph so stable endpoints (settlement, region, gate/entrance, landmark/POI, crossing as supported) can declare a reusable road/trail profile.
   - Keep logical connectivity independent of resolved spline/control-point geometry.
2. **Terrain-aware route resolver**
   - Deterministically resolve a logical connection into a physical centerline/polyline/spline.
   - Respect geography, maximum grade, cut/fill limits, settlement/building reservations, water/barriers, and explicit crossing/pass solutions.
   - Smooth the elevation profile rather than projecting onto terrain micro-bumps.
3. **Shared road influence**
   - Derive one compact world-space distance/influence field from the resolved route.
   - Use that same influence for terrain grading, surface transition, vegetation falloff, and other local road effects.
4. **Voxel deformation**
   - Grade/cut/fill through normal WorldBuilder/voxel generation.
   - Keep roads walkable, destructible where expected, deterministic, chunk-safe, and streaming/LOD compatible.
5. **Surface/material presentation**
   - Prefer a constrained `primary/base terrain material + optional secondary terrain material + scalar coverage` extension that reuses/generalizes existing terrain/coating machinery.
   - Dirt road core = full Dirt influence; shoulder continuously returns to local terrain; optional deterministic world-space edge noise prevents mathematical/voxel-looking borders.
   - Preserve exposed-top/slope material rules and do not create a road-only shader unless shared terrain coverage cannot fit the current renderer.
6. **Ecology**
   - Feed the same road influence into the shared vegetation policy: suppress incompatible vegetation in the core and ramp baseline ecology back through the shoulder.
7. **Semantic consumers**
   - Preserve the logical/resolved road graph for navigation, maps/travel, NPCs, encounters, town entrances, and future systems; never reconstruct semantic roads from rendered voxels.
8. **Migration**
   - Move existing Kentridge road/street generation onto the shared primitive where equivalent.
   - Let the macro-world physical-realization ticket instantiate inter-town roads through this capability.

## Investigation / evidence gates
- Trace the current production owners for top-level macro layout, WorldBuilder composition, existing road/street generation, voxel density/surface extraction, terrain material/coating presentation, vegetation/ecology, and streaming/LOD before choosing concrete types/files.
- Inspect the road-shoulder behavior represented by commit `336cb6e63e19bc6039f3f89bb4d2056e2d0efb60` and preserve the intended graded-road behavior while eliminating repeated shoulder bands as the final abstraction.
- Preserve the slope/material correctness represented by `8cd28a5ea7133a4012a17112375f70384bee79ec`.
- Where current code has refactored away from those historical paths, follow current production ownership rather than reviving stale architecture.

## Behavioral regressions
- Top-level connection -> semantic road -> deterministic resolved route -> physical generation instructions is traceable end to end.
- Fixed seed/input yields stable route geometry.
- Non-flat terrain respects maximum grade and cut/fill constraints.
- Blocked/invalid geography is rejected/rerouted or requires an explicit semantic crossing/pass solution.
- Terrain deformation and surface shoulder consume the same road influence.
- Shoulder coverage changes continuously/monotonically from core to local terrain without requiring discrete strip bands.
- Vegetation suppression and recovery follow road influence.
- Segment/chunk/LOD boundaries do not break geometry/material continuity.
- Semantic/resolved road data remains available to navigation/map/travel consumers.
- Existing Kentridge connectivity remains correct after migration.

## Exact-scene visual validation
- Build and launch the exact `KentridgePlayableSlice`; no startup/runtime exceptions.
- Capture an elevated/survey view showing a road generated from top-level connectivity actually reaching its intended endpoints.
- Traverse a representative segment at player height using normal CharacterMotor collision and streaming.
- Capture close shoulder views on uneven/sloped terrain proving a natural Grass↔Dirt transition with no hard binary border, staircase, or repeated bands.
- Capture medium/far views across chunk/LOD boundaries proving no seams.
- Show road-core vegetation suppression and gradual recovery through the shoulder.
- Capture route/influence debug instrumentation when available to prove the rendered road derives from the same semantic connection.
- Visual evidence is mandatory for closure; tests/debug overlays alone are insufficient.

## Blast radius / cost
- Compare route-solving/world-build time, voxel/brick work, primitive/GameObject count, resident memory, CPU/GPU rendering cost, and far-field/LOD/streaming behavior against existing budgets.
- Do not weaken budgets to land the feature.
- Prefer compact/analytic road influence from route data over dense masks or per-segment scene objects unless the existing engine already has a more suitable compact representation.
