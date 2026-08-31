# Far-World Visibility Implementation Plan

## Acceptance and ownership

Keep broad terrain in `VoxelFarTerrain`; known structures come from renderer-neutral WorldBuilder planning; settlement/vegetation HLOD derives from existing semantic truth; scene thresholds/readiness remain composition policy. Acceptance requires 8/10/12 km landmark visibility without voxel residency, never-visited semantic visibility, deterministic structure/scatter representations, forested horizon massing, stable readiness+hysteresis handoffs, guaranteed clipmap coverage, semantic/fallback separation, built-player proof, and device-matrix budgets.

## Hypotheses / discriminators

- **H1 falsified:** sparse far-terrain point sampling cannot reliably preserve known structure silhouettes; semantic HLOD is required.
- **H2 active:** deterministic sector queries plus aggregate canopy/cluster proxies can preserve distant density without persistent far object ownership. Prove stable IDs/order and camera-window changes before renderer integration.

## Selected approach / progress

Coverage math/fallback retirement (T001/T002), structure descriptors (T004), visibility manifest (T006), Kentridge planning population (T008), engine render contract (T009), semantic adapter (T010), cached instanced proxies (T011), projected-significance policy (T012), fallback suppression (T014), and deterministic settlement clusters (T017) are implemented with focused regressions pending final exact-head validation.

T018 production support already existed across `ShowcaseFarStructureSource`, `FarWorldVisibilityPolicy`, and `ProceduralFarStructureRenderer`. Added regressions proving a 12-building settlement collapses to one far cluster while its landmark remains independent, inactive clusters return members without double rendering, and cluster hysteresis holds until the member mid-enter threshold.

T019 is implemented as stateless `VegetationVisibility` queries over existing `VegetationInstance` and `ITreeWorldReadSource` truth. Fixed sectors use floor semantics including negatives; outputs carry stable semantic IDs/source indices and deterministic ordering; tree queries expose existing damage state and never request skeletons, voxel residency, or new persistence. Independent fake-source regressions cover order stability, negative sectors, camera-window membership changes, and no world-truth mutation.

T022 derives deterministic forest canopy clusters from T019 tree visibility records while excluding independent landmark trees and severed trees; persistent foliage-health changes only invalidate the affected sector cluster revision.

T023 supplies renderer-neutral deterministic natural-scatter records for ordinary boulders from world seed + fixed sector and explicit landmark records for exceptional rock features.

T024/T025 coarse presentation state is adapter-side and renderer-neutral: removed/restored structure state suppresses or restores far proxies without mutating the semantic planning manifest, and tree damage/severing flows through existing vegetation truth into canopy visibility.

T027 reuse boundary identified in shipped campaign composition: `KentridgeCampaignGenerationPlan.Visibility` exposes the same `IWorldVisibilitySource` produced during pre-voxel campaign planning, independently of the showcase renderer path.

## CI history / root causes

Run `33409771197` failed because a Kentridge semantic test was missing its content namespace import; feature work corrected that product cause.

Run `33414406079` repeated the `Int2` compile symptom. Minimal root-cause isolation showed `Int2` is owned by `MountingForce.WorldGen`, not `MountingForce.WorldGen.Content.Kentridge`; the production cluster/scatter files were corrected accordingly before another request.

Run `33415875148` correctly targeted feature parent `865f5a7b...` and exposed only regression-source compile defects: two HLOD tests omitted `MountingForce.WorldGen`/Kentridge imports, the Kentridge planning test omitted its content import, and the canopy test used an NUnit containment form that bound to a string overload. Those exact causes are corrected on the current feature head; no third production fix was attempted for the prior symptom.

## Blockers / remaining gates

T003, T005/T007, T013, and T015/T016 require safe edits in large `VoxelFarTerrain.cs`, `ShowcaseWorld.cs`, or `VoxelShowcase.cs`; current connector writes replace complete files and the execution container has no repository checkout, so unsafe wholesale rewrites are not acceptable. Acceptance is unchanged; continue independent tasks.

Final T029–T033 still require exact-head behavioral validation, built-player visual evidence, budget/device validation, cleanup, and documentation before closure. The next CI request must be a direct child of the current feature head and use only `ci-test/fixes/agent-7`.
