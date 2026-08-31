# Far-World Visibility Implementation Plan

## Purpose

Define a durable architecture for keeping terrain, settlements, castles, forests, landmark trees, rock formations, and ordinary scatter visually coherent from the resident voxel world through the far horizon without making distant voxel regions resident. The design must preserve deterministic world truth, reuse current terrain/voxel systems, and make representation choice semantic and budget-driven rather than scene-specific.

This issue owns not only far-world coverage and object HLOD, but also the **visual fidelity of the entire far terrain representation**. The far landscape from the resident-world boundary to the configured horizon must continue to read as the same terrain system instead of becoming an obviously smoother, flatter, differently shaded special-purpose surface.

## Observed baseline

- `VoxelShowcase` currently streams eight 51.2 m regions (~409.6 m) and hands that radius to `VoxelFarTerrain`.
- Near surface extraction already uses progressively coarser source steps; far terrain is a 96-cell geometric clipmap sampled analytically rather than from resident terrain voxels.
- With the current ~409.6 m inner radius, far-ring spacing is approximately 12.8/25.6/51.2/102.4/204.8 m.
- The outer resident voxel ring is approximately 0.8 m source spacing, so the first far ring introduces roughly a **16x geometry-sampling jump** at the representation boundary.
- `VoxelFarTerrain` uses a distinct far shader, but its vertex material-family albedo is already selected from the same game-owned `ShowcaseMaterialSet` and renderer presentation catalogue as near terrain. The remaining visible mismatch is therefore principally surface frequency/normal/lighting language plus any residual geometric silhouette loss.
- `FarFieldStructureStore` retains authored raised/lowered surfaces in 16x16 columns per region (3.2 m columns) with a 2.4 m minimum raised-feature threshold, but `VoxelFarTerrain` point-samples that store only at clipmap vertices.
- There are no dedicated persistent far representations for ordinary trees, boulders, shrubs, or other scatter.

## Acceptance

1. A declared landmark remains visible at 8, 10, and 12 km from cardinal and diagonal views, including camera snap phases, without requiring its voxel regions to be resident.
2. A never-visited declared landmark can still appear from macro-world metadata.
3. Broad terrain, semantic structures, and deterministic scatter use independent distance representations while sharing one deterministic world definition.
4. Forested mountains remain visibly forested at horizon distance without retaining/drawing individual trees.
5. Small scatter naturally disappears by projected significance while giant natural features are promoted to landmark treatment.
6. Near/far transitions have overlap/hysteresis and do not visibly drop an object during representation handoff.
7. Configured far radius is geometrically guaranteed; tests prove coverage rather than inferring it from ring count.
8. CPU/GPU/memory budgets are measured against the authoritative device matrix before rollout.
9. **The whole far-terrain range remains visually coherent with the near terrain.** At representative views around 0.5, 1, 3, 6, 10, and 12 km, terrain must retain the same material families, broad surface character, slope/rock/soil relationships, and plausible lighting response rather than becoming a conspicuously smooth or differently colored terrain regime.
10. Far-terrain surface detail is **not limited by clipmap vertex spacing**. World-space macro/material variation and presentation-only normal/detail frequencies may be finer than the geometric grid, with distance filtering so they remain stable and do not shimmer at long range.
11. If shading/material continuity is insufficient because the current 12.8 m first far ring loses important silhouette shape, add only the minimum measured/configured **inner far-terrain geometry tier** needed to fix that defect. Do not increase near voxel residency or make the whole 12 km field high density.
12. Moving through the ~350-600 m transition from multiple directions/elevations must not reveal a hard geometry, normal, material, color, fog, or lighting boundary between resident and far terrain.

## Working hypotheses / discriminators

- **H1 falsified for semantic structures:** sparse far-terrain point sampling cannot reliably preserve known structure silhouettes; semantic HLOD is required.
- **H2 active:** deterministic sector queries plus aggregate canopy/cluster proxies can preserve distant density without persistent far object ownership. Prove stable IDs/order and camera-window changes before renderer integration.
- **H3 active:** most perceived far-terrain quality loss should be recoverable without materially increasing geometry by sharing world-space terrain/material semantics and adding distance-filtered shader detail independent of mesh spacing.
- **H4 active:** if material/normal fidelity is fixed and the inner far field still looks geometrically flattened, measure the defect before adding a bounded denser inner clipmap tier.

## Selected direction

Keep `VoxelFarTerrain` for broad analytic terrain and `FarFieldStructureStore` as a generic authored-surface fallback. Do **not** solve far-terrain quality by extending voxel residency.

Treat far terrain as two decoupled frequency domains:

- **Geometry / low frequency:** the clipmap carries mountain silhouettes, valleys, ridges, and broad slopes. Keep outer rings aggressively coarse.
- **Surface appearance / higher frequency:** derive material family, macro color, slope/rock/soil breakup, roughness, and presentation normals from deterministic world-space terrain inputs. These visual frequencies are allowed to be finer than the underlying triangles and are progressively filtered/faded with projected distance to avoid aliasing and shimmer.

The far shader now applies deterministic world-space triplanar macro luminance variation and presentation-only detail-normal perturbation, with distance filtering before kilometre-scale aliasing. It modulates the already-selected shared material-family albedo rather than introducing far-only material identity. Built-player evidence must determine whether this materially closes the visual gap and whether a bounded inner geometry tier is still required.

Alongside terrain fidelity, add a small far-visibility data layer with semantic structure records, deterministic scatter-cell descriptors, projected-size/importance tier policy, structure HLOD, forest/canopy aggregation, and natural-landmark promotion. Full rationale, contracts, phases, tests, and migration details are in `architecture-proposal.md`.

## Implementation progress

Coverage math/fallback retirement (T001/T002), structure descriptors (T004), visibility manifest (T006), Kentridge planning population (T008), engine render contract (T009), semantic adapter (T010), cached instanced proxies (T011), projected-significance policy (T012), fallback suppression (T014), and deterministic settlement clusters (T017) are implemented with focused regressions.

T001/T002 are concretely present in production: `VoxelFarTerrain.RingCount` delegates to testable guaranteed snapped-coverage math, logs when `MaxRings` cannot satisfy the requested extent, and startup fallback retirement requires a contiguous current authoritative ring prefix whose guaranteed coverage reaches the configured radius. `VoxelFarTerrainCoverageTests` covers the shipped 409.6 m -> 12 km case, representative/worst snap phases, impossible-range failure, independent reuse, and no-coverage-shrink fallback retirement.

T003B implementation has started in `VoxelEngine/FarTerrain`: stable absolute-world-space macro variation is independent of ring origin/sample spacing, and higher-frequency presentation-normal detail fades out with distance. This is presentation only and leaves analytic geometry/collision/world truth unchanged. Shader compilation and built-player visual proof are still required before T003B can be checked complete.

T018 support exists across `ShowcaseFarStructureSource`, `FarWorldVisibilityPolicy`, and `ProceduralFarStructureRenderer`. Regressions prove a dense settlement collapses to one far cluster while its landmark remains independent, inactive clusters return members without double rendering, and cluster hysteresis holds until member mid-enter threshold.

T019 is implemented as stateless `VegetationVisibility` queries over existing `VegetationInstance` and `ITreeWorldReadSource` truth. Fixed sectors use floor semantics including negatives; outputs carry stable semantic IDs/source indices and deterministic ordering; tree queries expose existing damage state and never request skeletons, voxel residency, or new persistence.

T022 derives deterministic forest canopy clusters from T019 tree visibility records while excluding independent landmark trees and severed trees; persistent foliage-health changes only invalidate the affected sector cluster revision. T023 supplies renderer-neutral deterministic natural-scatter records for ordinary boulders from world seed + fixed sector and explicit landmark records for exceptional rock features.

T024/T025 coarse presentation state is adapter-side and renderer-neutral: removed/restored structure state suppresses or restores far proxies without mutating the semantic planning manifest, and tree damage/severing flows through existing vegetation truth into canopy visibility. The shipped campaign planning path separately exposes `KentridgeCampaignGenerationPlan.Visibility`, providing a real non-Showcase consumer of the renderer-neutral visibility source.

T029 behavioral coverage includes explicit 8/10/12 km cardinal+diagonal horizon-landmark selection and a narrow 10 m semantic structure query proving the proxy path does not depend on a coarse far-terrain sample landing inside its footprint.

## Validation gates

Implement in independently testable phases: coverage correctness -> **whole-range far-terrain material/normal/detail fidelity -> measured inner far geometry fidelity if still required -> near/far terrain transition proof** -> visibility manifest -> semantic structure HLOD -> deterministic scatter -> canopy/forest HLOD -> natural landmark promotion -> transition/budget stress validation.

Built-player evidence must include terrain-dominant views at approximately 0.5, 1, 3, 6, 10, and 12 km plus camera travel across ~350-600 m from multiple directions/elevations. Inspect for smooth/flat far-world appearance, material-family changes, normal/lighting discontinuity, color/fog mismatch, shimmer/aliasing, silhouette loss, popping, and any seam.

Performance validation must separately record far-terrain ring vertex counts, CPU sampling/build time, rebuild churn, shader/GPU cost, draw count, and memory. Existing SceneIssues remain the implementation units; do not duplicate active macro-world or terrain-streaming work.

## CI history / root causes

Run `33409771197` failed because a Kentridge semantic test was missing its content namespace import; feature work corrected that product cause.

Run `33414406079` repeated the `Int2` compile symptom. Minimal root-cause isolation showed `Int2` is owned by `MountingForce.WorldGen`, not `MountingForce.WorldGen.Content.Kentridge`; production cluster/scatter files were corrected accordingly before another request.

Run `33415875148` correctly targeted its intended feature parent and exposed regression-source compile defects only: two HLOD tests omitted worldgen/Kentridge imports, the Kentridge planning test omitted its content import, and the canopy test used an NUnit containment form that bound to a string overload. Those exact causes were corrected.

Run `33418238980` is green for source SHA `8341b488...`: requested `ShowcaseFarStructureSourceTests`, automatic module validation, and standalone `KentridgePlayableSlice` SceneIssue replay all passed. The built-player capture remained near the Kentridge opening/interior, so this is supporting rather than final visual proof of 8/10/12 km HLOD, canopy HLOD, transition behavior, or the newly required terrain-distance fidelity captures.

## Dependency / authority audit

The engine render contract remains Game-agnostic: `VoxelEngine.Rendering.Api` references Vegetation/AmbientLife/Unity.Mathematics only and `FarWorldRendering.cs` contains render-ready values rather than WorldBuilder intent, storage, or residency state. `Game.WorldBuilder.Api` remains `noEngineReferences=true`; `WorldVisibilityManifest` owns only deterministic descriptors/sector membership and has no voxel/storage/render hooks. Structure removal state is a lightweight CPU-side presentation source keyed by existing semantic identity, while tree damage/sever state continues to come from the existing tree read source. No duplicate voxel/tree authority is introduced by these completed pieces.

## Blockers / remaining gates

T003A-T003D remain required. T003A needs broader shared terrain classification beyond the already-shared base material-family selection if built-player evidence demonstrates slope/rock/soil mismatch; T003B now has an implementation candidate but still needs exact-head compile and visual proof. T003C is conditional on measured residual geometry loss after those changes; T003D requires built-player transition proof. T020/T021, T026, and T028 still require renderer/composition hookup work. Final T029-T033 still require exact-head behavioral validation, purpose-built far-visibility/terrain captures, canonical Kentridge integration, device/budget evidence, cleanup, and documentation before closure.
