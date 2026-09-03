# Far-World Visibility Implementation Plan

## Purpose

Define a durable architecture for keeping terrain, settlements, castles, forests, landmark trees, rock formations, and ordinary scatter visually coherent from the resident voxel world through the far horizon without making distant voxel regions resident. The design must preserve deterministic world truth, reuse current terrain/voxel systems, and make representation choice semantic and budget-driven rather than scene-specific.

This issue owns not only far-world coverage and object HLOD, but also the **visual fidelity of the entire far terrain representation**. The far landscape from the resident-world boundary to the configured horizon must continue to read as the same terrain system instead of becoming an obviously smoother, flatter, differently shaded special-purpose surface.

## Observed baseline

- `VoxelShowcase` currently streams eight 51.2 m regions (~409.6 m) and hands that radius to `VoxelFarTerrain`.
- Near surface extraction already uses progressively coarser source steps; far terrain is a 96-cell geometric clipmap sampled analytically rather than from resident terrain voxels.
- With the current ~409.6 m inner radius, far-ring spacing is approximately 12.8/25.6/51.2/102.4/204.8 m.
- The outer resident voxel ring is approximately 0.8 m source spacing, so the first far ring introduces roughly a **16x geometry-sampling jump** at the representation boundary.
- `VoxelFarTerrain` also uses a distinct far-terrain material/shading path. Coarse geometric normals plus a different surface treatment can make the entire far landscape look smoother and visually unrelated even when there is no literal seam or coverage hole.
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

- **H1:** Existing far-terrain structure sampling is sufficient if made conservative. Test a narrow castle footprint across outer-ring sample phases. If still silhouette-poor, semantic structure HLOD is required.
- **H2:** Deterministic macro-cell scatter can reproduce convincing forests without persistent per-tree records. Compare regenerated cell identity/distribution across sessions and evaluate mid/far visual continuity.
- **H3:** Most of the perceived far-terrain quality gap can be removed without materially increasing geometry by deriving far surface appearance from the same world-space terrain facts/material families as the near presentation and adding distance-filtered macro/detail normals, color, and roughness variation in the far shader.
- **H4:** The current 12.8 m first far-ring spacing may still lose terrain silhouette/shape near the residency boundary after shading is corrected. Measure this separately. Add a denser inner far tier only if built-player evidence demonstrates residual geometric flattening; do not assume a fixed spacing before measurement.

## Selected direction

Keep `VoxelFarTerrain` for broad analytic terrain and `FarFieldStructureStore` as a generic authored-surface fallback. Do **not** solve far-terrain quality by extending voxel residency.

Treat far terrain as two decoupled frequency domains:

- **Geometry / low frequency:** the clipmap carries mountain silhouettes, valleys, ridges, and broad slopes. Keep outer rings aggressively coarse.
- **Surface appearance / higher frequency:** derive material family, macro color, slope/rock/soil breakup, roughness, and presentation normals from deterministic world-space terrain inputs. These visual frequencies are allowed to be finer than the underlying triangles and are progressively filtered/faded with projected distance to avoid aliasing and shimmer.

First make the material/normal language coherent. Then measure whether an additional bounded inner far-terrain annulus/tier is required for silhouette fidelity around the resident boundary. Any denser tier must be configurable, limited to the inner far range, and justified by CPU mesh-build, GPU vertex/draw, and memory measurements.

Alongside terrain fidelity, add a small far-visibility data layer with semantic structure records, deterministic scatter-cell descriptors, projected-size/importance tier policy, structure HLOD, forest/canopy aggregation, and natural-landmark promotion. Full rationale, contracts, phases, tests, and migration details are in `architecture-proposal.md`.

## Validation gates

Implement in independently testable phases: coverage correctness -> **whole-range far-terrain material/normal/detail fidelity -> measured inner far geometry fidelity if still required -> near/far terrain transition proof** -> visibility manifest -> semantic structure HLOD -> deterministic scatter -> canopy/forest HLOD -> natural landmark promotion -> transition/budget stress validation.

Built-player evidence must include terrain-dominant views at approximately 0.5, 1, 3, 6, 10, and 12 km plus camera travel across ~350-600 m from multiple directions/elevations. Inspect for smooth/flat far-world appearance, material-family changes, normal/lighting discontinuity, color/fog mismatch, shimmer/aliasing, silhouette loss, popping, and any seam.

Performance validation must separately record far-terrain ring vertex counts, CPU sampling/build time, rebuild churn, shader/GPU cost, draw count, and memory. Existing SceneIssues remain the implementation units; do not duplicate active macro-world or terrain-streaming work.

## Current execution note

- T025 remains unchecked because exact-SHA repository validation is blocked externally: run `33734577506` validated source `ac445caac1b310a29eef2925390cfeac4804406d`; the T025 requested/preceding phases pass, then 16 existing `VoxelEngine.Rendering.Tests.EditMode` GPU/arena parity assertions fail. `origin/master` is still `b18d470f66221c7cb6091249f4683c2d994bffec`; GPU renderer restoration is owned elsewhere, so agent-7 will not weaken or patch those failures.
- Independent T026 work now wires `VoxelShowcase` to a lazy `FeaturePresentationManifest` derived from its canonical feature catalogue, the shared `FarFeaturePresentationAdapter`/selection policy, persisted coarse structure state, and `ProceduralFarFeatureRenderer`. Runtime queries the scene camera without requesting distant voxel residency, owns renderer lifetime with the Showcase, and extends existing far diagnostics with semantic source/visible counts.
- Focused regression `VoxelEngine.Tests.EditMode.ShowcaseFarFeatureRuntimeTests` verifies the scene runtime queries only its injected renderer-neutral presentation source. T026 remains unchecked until exact-SHA validation completes.
- While staging that validation, a non-triggering `.github/test-request.json.tmp-agent7` commit briefly advanced the sole CI transport; repeated run lookup returned no workflow run. Temporary issue-local staging files were removed. The next request must be rebuilt directly from the exact feature head with only `.github/test-request.json`; no queued/running CI is being replaced.
- Once the Rendering baseline is repaired on master, merge current master and revalidate the exact feature head before checking blocked tasks or closure.
