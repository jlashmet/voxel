# Plan

## Observed defect / acceptance
- Kentridge currently reads as nearly barren: grass is extremely sparse and does not visibly animate.
- Closure requires a built-player Kentridge meadow with at least 3,000 procedural grass blades attributable to one contiguous meadow region, visually dense at player height, plus time-separated visual evidence proving wind animation.
- Kentridge's current area policy must allow only the new procedural grass vegetation, no trees for this initial vegetation pass, and no ambient animal kinds.

## Current architecture / evidence
- The issue's historical `Scripts/Runtime/Presenters/KentridgeWorldBuilder.cs` path has moved. Current Kentridge realization lives in `Assets/Game/Composition/Kentridge/Playable/SceneRuntime/KentridgeRegionLife.cs`, invoked by `KentridgePlayableSlice.cs`.
- `KentridgeRegionLife.BuildUndergrowth` currently samples the whole road corridor at 1.4 m, then applies a second `Density = 0.45` placement filter while `VegetationPlacement` may choose any catalog kind. Trees and wildlife are generated independently.
- `ProceduralGrassBatch` already renders arbitrarily large instance lists in chunks of 1,023 matrices, so a 3,000+ blade meadow does not require a renderer rewrite or GameObject flood.
- WorldBuilder generation is engine-agnostic (`MountingForce.WorldGen.Core` has `noEngineReferences`), so the reusable policy should live there using engine-neutral kind identifiers and numeric placement constraints. Runtime composition translates that policy to vegetation enums/masks.

## Competing hypotheses / discriminator
1. **Renderer capacity bottleneck** — rejected by source inspection: grass instances are split across repeated 1,023-instance `DrawMeshInstanced` batches.
2. **Sparse/over-broad semantic placement** — supported: 1.4 m sampling, 0.45 density, unrestricted catalog selection, plus independent trees/wildlife cannot satisfy the requested grass-only dense meadow.
3. **Broken wind binding** — unresolved until built-player evidence. Do not change shader/material code unless stationary time-separated frames show no motion with a dense meadow.

## Implementation strategy
- Add a backward-compatible allowed-kind mask to shared `VegetationPlacementSettings`; its default must preserve all existing kinds and callers.
- Add a reusable engine-neutral `RegionEcologyPolicy` in WorldBuilder Core covering allowed vegetation/tree/animal kind IDs, vegetation density, sample spacing, max slope, and route clearance.
- Give `KentridgeDefinition` a grass-only policy with empty tree/animal allowlists.
- Pass the top-level policy through `KentridgePlayableSlice` into `KentridgeRegionLife`; do not serialize new scene objects or edit `Kentridge.unity`.
- Build the meadow through the existing sampled-ground + `VegetationPlacement` production path. Use sub-metre sampling and high density, preserve riverbank/built-content/slope rejection, and add explicit road/bridge-route clearance.
- Keep the existing 12,000-sample cap unless measured evidence requires adjustment. With ~0.4 m spacing and a ~90 m corridor, the cap concentrates dense coverage near Kentridge and leaves substantial headroom above 3,000 grass instances on one contiguous side of the road.
- Expose concise runtime diagnostics for total grass and primary contiguous meadow grass so CI evidence can attribute the count to this policy rather than to unrelated vegetation.
- Keep `ProceduralGrassBatch` and shared wind code unchanged unless the built-player discriminator proves motion is broken.

## Regression strategy
- Verify default placement settings remain unrestricted/backward-compatible.
- Verify a grass-only mask never emits another vegetation kind and generation remains deterministic.
- Verify Kentridge policy allows only Grass and has empty tree/animal allowlists.
- Verify a synthetic production-path meadow grid with Kentridge policy parameters yields >=3,000 grass instances in one contiguous meadow side while the road-clearance band remains empty.
- Prefer tests in an existing assembly already referencing WorldBuilder Core and Vegetation API; do not introduce broad dependency changes solely for the test.

## Blast radius / cost expectation
- Expected production blast radius: shared vegetation-placement API (backward-compatible default) plus Kentridge WorldBuilder policy/runtime composition. No scene serialization, shader/material, mesh, collider, lighting, camera, or prefab changes are planned.
- Dense grass increases placement work and instanced draw batches. At 12,000 maximum samples/instances it remains bounded; final CI/runtime evidence must record actual count and relevant performance diagnostics against existing budgets.
- Assigned estimate remains `$2` / small. If implementation requires shader redesign, scene surgery, or materially higher runtime budgets, stop expanding scope and record the discovered blocker instead.

## Validation gates
- Run canonical scene validation, no-prefab-lighting, tree-mutation load, semantic baseline, snapshot, and editmode-behavior gates for the targeted module list; produce the required validation artifacts and blast-radius report.
- Exact-SHA built-application Kentridge harness with no startup/runtime exceptions.
- Durable visual evidence: approach view, player-height dense meadow view, >=3,000-blade meadow diagnostic, and two or more fixed-view time-separated frames (or short sequence) showing blade motion.
- Human visual review is mandatory before pending/closed. Static source assertions, primitive counts, or crash-free launch alone cannot satisfy closure.
- After green exact-SHA CI, run workflow-stability and strict lifecycle/hash checks before closure and publication.
