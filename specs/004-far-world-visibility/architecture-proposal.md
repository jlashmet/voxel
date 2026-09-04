# Far-World Visibility Architecture

## Final ownership boundaries

Far visibility is presentation derived from one deterministic world definition; it is not a second simulation.

- **Broad terrain:** `VoxelFarTerrain` owns the analytic geometric clipmap. It samples the same terrain query as the near voxel world and accepts only authored surface deviations / anonymous voxel fallback from `FarFieldStructureStore`.
- **Known semantic structures:** canonical feature catalogue/planning data is baked into renderer-neutral `FeaturePresentationBake` records before voxel residency. `FeaturePresentationManifest` provides deterministic spatial queries. Semantic structures are drawn through the engine-facing far-feature API and `ProceduralFarFeatureRenderer`, not by relying on a sparse far-terrain vertex entering the footprint.
- **Legacy surface fallback:** `FarFieldStructureStore` retains authored lowered surfaces, materials, and anonymous/arbitrary positive voxel silhouettes. When a semantic presentation source is bound, positive columns inside semantic feature bounds are suppressed so the semantic proxy and terrain clipmap cannot both own the building silhouette.
- **HLOD selection:** shared selection/aggregation code uses stable IDs, projected significance, semantic importance, hysteresis/readiness, and composition-owned thresholds. Rendering APIs do not reference WorldBuilder intent types or named scene content.
- **Settlements / natural scatter / vegetation:** deterministic aggregation and visibility APIs choose bounded member/cluster/canopy representations. Existing deterministic placement and `TreeWorldState` remain authoritative; far tiers do not create a second persistence model.
- **Persistent structure state:** `StructureVisualStateStore` carries coarse authoritative state into far presentation. GPU/render output is never world truth.
- **Reuse:** Showcase and the built `KentridgePlayableSlice` consume the same far-feature contracts. Kentridge is the independent assembled-game consumer.

## Coverage and presentation strategy

The old heuristic ring count is replaced by explicit worst-case snapped coverage math. Ring spacing, half-extent, camera snap loss, and guaranteed coverage are testable. The shipped validation exercises views from the near handoff through 1, 3, 6, 8, 10, and 12 km. Startup coarse coverage remains active until the required authoritative rings publish contiguously.

Far-terrain material detail is evaluated in deterministic world space. Macro/fine variation, roughness and normal detail are filtered by projected footprint rather than tied to the 12.8-204.8 m outer geometry spacing. Geometry therefore carries broad ridges/valleys while shader detail supplies sub-grid surface character without becoming collision/world truth. No uniformly dense 12 km voxel or terrain representation is introduced.

## Handoff rules

Near voxel surfaces win only when their required surface coverage is published. Semantic far proxies remain present while near regions are merely resident/building, and return before near representation is removed. Enter/exit thresholds are hysteretic. Terrain keeps bounded overlap/guard coverage across the resident/far boundary so a camera snap cannot expose a gap.

## Built-player evidence and measured limits

Baseline exact-SHA run `3d30c6c6785316bd59bd3c934c8b4a7262d56aad` (source `8843766864509f57db2e25ece1d3a1b3480e7d4e`) passed module validation and standalone-player replay on an Apple M4 Max. The module tableau reported 66 semantic far features, 3,185 near-terrain vertices, 6,321 far-terrain vertices, and eight staged views through 12 km. Direct inspection of the 10 km artifact showed the coarse far terrain and semantic proxies present together without requiring detailed voxel residency.

The final validation scene includes `FarWorldBudgetProbe`, which records built-player CPU/GPU frame timing (when the platform exposes GPU timings), Unity allocated/reserved memory, graphics-driver memory, semantic instance count, batch count, and mesh/material cache counts after warmup. Final acceptance values are recorded in the SceneIssue evidence from the exact final-head run; they are compared to `specs/001-destructible-voxel-engine/device-matrix.md`. Missing GPU timing support is reported as zero samples rather than fabricated data.

## Explicit exclusions

Far visibility does not retain distant voxel bricks, collision, interiors, physics, NPC simulation, gameplay scripts, or one persistent GameObject per building. Device tiers may reduce presentation complexity and thresholds, but they do not change deterministic world truth, gameplay interest radius, collision, or authoritative simulation.
