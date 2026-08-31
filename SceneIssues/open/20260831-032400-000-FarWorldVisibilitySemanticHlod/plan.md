# Far-World Visibility Implementation Plan

## Acceptance and ownership

Keep broad terrain in `VoxelFarTerrain`; known structures come from renderer-neutral WorldBuilder planning; settlement/vegetation HLOD derives from existing semantic truth; scene thresholds/readiness remain composition policy. Acceptance requires 8/10/12 km landmark visibility without voxel residency, never-visited semantic visibility, deterministic structure/scatter representations, forested horizon massing, stable readiness+hysteresis handoffs, guaranteed clipmap coverage, semantic/fallback separation, built-player proof, and device-matrix budgets.

## Hypotheses / discriminators

- **H1 falsified:** sparse far-terrain point sampling cannot reliably preserve known structure silhouettes; semantic HLOD is required.
- **H2 active:** deterministic sector queries plus aggregate canopy/cluster proxies can preserve distant density without persistent far object ownership. Prove stable IDs/order and camera-window changes before renderer integration.

## Selected approach / progress

Coverage math/fallback retirement (T001/T002), structure descriptors (T004), visibility manifest (T006), Kentridge planning population (T008), engine render contract (T009), semantic adapter (T010), cached instanced proxies (T011), projected-significance policy (T012), fallback suppression (T014), and deterministic settlement clusters (T017) are implemented with focused regressions pending final exact-head validation.

T018 production support already existed across `ShowcaseFarStructureSource`, `FarWorldVisibilityPolicy`, and `ProceduralFarStructureRenderer`. Added regressions proving a 12-building settlement collapses to one far cluster while its landmark remains independent, inactive clusters return members without double rendering, and cluster hysteresis holds until the member mid-enter threshold.

T019 is now implemented as stateless `VegetationVisibility` queries over existing `VegetationInstance` and `ITreeWorldReadSource` truth. Fixed sectors use floor semantics including negatives; outputs carry stable semantic IDs/source indices and deterministic ordering; tree queries expose existing damage state and never request skeletons, voxel residency, or new persistence. Independent fake-source regressions cover order stability, negative sectors, camera-window membership changes, and no world-truth mutation.

## Blockers / remaining gates

T003, T005/T007, T013, and T015/T016 require safe edits in large `VoxelFarTerrain.cs`, `ShowcaseWorld.cs`, or `VoxelShowcase.cs`; current connector writes replace complete files and the execution container has no repository checkout, so unsafe wholesale rewrites are not acceptable. Acceptance is unchanged; continue independent tasks.

Run `33409771197` completed failure: the requested Kentridge test did not compile because `KentridgeDefinition` was missing; feature commit `0f7914c5...` fixed that product cause. T018 request run `33414406079` targets exact feature source `e0ab1f438...`; it is queued and must not be replaced. Current feature head before this plan commit is `2b76687530520b3a2f5f021175b9def50ce9ecc7`, so that run is supporting evidence only. Next non-blocked work after T019 is T020; final T029–T033 still require full behavioral, built-player visual, budget, cleanup, and documentation gates on the final exact SHA.
