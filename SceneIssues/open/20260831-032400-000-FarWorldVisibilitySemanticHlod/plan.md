# Far-World Visibility Implementation Plan

## Acceptance and ownership

Keep broad terrain in `VoxelFarTerrain`; known structures come from renderer-neutral WorldBuilder planning; settlement/vegetation HLOD derives from existing semantic truth; scene thresholds/readiness remain composition policy. Acceptance requires 8/10/12 km landmark visibility without voxel residency, never-visited semantic visibility, deterministic structure/scatter representations, forested horizon massing, stable readiness+hysteresis handoffs, guaranteed clipmap coverage, semantic/fallback separation, built-player proof, device-matrix budgets, **and visual fidelity of the entire far-terrain range rather than only seam/coverage correctness**.

## Newly added required far-terrain fidelity scope

The SceneIssue task list on `master` now includes **T003A-T003D**. These are new required closure work and are not implied complete by T001/T002 coverage work:

- **T003A:** make far terrain derive the same visual terrain families from the same deterministic world-space facts as near terrain while keeping `VoxelFarTerrain` as the cheap analytic clipmap.
- **T003B:** decouple far surface appearance from coarse clipmap vertex spacing using stable world-space macro/material variation and presentation/detail normals, with distance filtering to prevent shimmer/aliasing.
- **T003C:** after T003A/T003B, measure actual silhouette loss from the ~12.8 m first far ring. Add only the minimum bounded/configurable inner far-terrain density tier if built-player evidence still demonstrates geometric flattening. Do not increase near voxel residency or uniformly densify 12 km.
- **T003D:** after whole-range fidelity is correct, prove the ~350-600 m resident/far transition has no conspicuous height, normal, material/color, fog, lighting, flattening, popping, or seam discontinuity.

T029-T031 are also extended on `master` to require terrain-dominant built-player evidence around ~0.5, 1, 3, 6, 10, and 12 km and to measure per-ring geometry/build churn plus far-terrain shader/GPU cost. Passing far coverage or structure HLOD alone is no longer sufficient for closure. Fetch/reconcile these task-list changes before final validation/closure; do not discard existing branch progress while doing so.

## Hypotheses / discriminators

- **H1 falsified:** sparse far-terrain point sampling cannot reliably preserve known structure silhouettes; semantic HLOD is required.
- **H2 active:** deterministic sector queries plus aggregate canopy/cluster proxies can preserve distant density without persistent far object ownership. Prove stable IDs/order and camera-window changes before renderer integration.
- **H3 active:** most perceived far-terrain quality loss should be recoverable without materially increasing geometry by sharing world-space terrain/material semantics and adding distance-filtered shader detail independent of mesh spacing.
- **H4 active:** if material/normal fidelity is fixed and the inner far field still looks geometrically flattened, measure the defect before adding a bounded denser inner clipmap tier.

## Selected approach / progress

Coverage math/fallback retirement (T001/T002), structure descriptors (T004), visibility manifest (T006), Kentridge planning population (T008), engine render contract (T009), semantic adapter (T010), cached instanced proxies (T011), projected-significance policy (T012), fallback suppression (T014), and deterministic settlement clusters (T017) are implemented with focused regressions pending final exact-head validation.

T001/T002 are concretely present in production: `VoxelFarTerrain.RingCount` delegates to testable guaranteed snapped-coverage math, logs when `MaxRings` cannot satisfy the requested extent, and startup fallback retirement now requires a contiguous current authoritative ring prefix whose guaranteed coverage reaches the configured radius. `VoxelFarTerrainCoverageTests` proves the shipped 409.6 m -> 12 km case, representative/worst snap phases, impossible-range failure, independent reuse, and no-coverage-shrink fallback retirement.

T018 production support exists across `ShowcaseFarStructureSource`, `FarWorldVisibilityPolicy`, and `ProceduralFarStructureRenderer`. Regressions prove a dense settlement collapses to one far cluster while its landmark remains independent, inactive clusters return members without double rendering, and cluster hysteresis holds until member mid-enter threshold.

T019 is implemented as stateless `VegetationVisibility` queries over existing `VegetationInstance` and `ITreeWorldReadSource` truth. Fixed sectors use floor semantics including negatives; outputs carry stable semantic IDs/source indices and deterministic ordering; tree queries expose existing damage state and never request skeletons, voxel residency, or new persistence. Independent fake-source regressions cover order stability, negative sectors, camera-window membership changes, and no world-truth mutation.

T022 derives deterministic forest canopy clusters from T019 tree visibility records while excluding independent landmark trees and severed trees; persistent foliage-health changes only invalidate the affected sector cluster revision. T023 supplies renderer-neutral deterministic natural-scatter records for ordinary boulders from world seed + fixed sector and explicit landmark records for exceptional rock features.

T024/T025 coarse presentation state is adapter-side and renderer-neutral: removed/restored structure state suppresses or restores far proxies without mutating the semantic planning manifest, and tree damage/severing flows through existing vegetation truth into canopy visibility. The shipped campaign planning path separately exposes `KentridgeCampaignGenerationPlan.Visibility`, providing a real non-Showcase consumer of the renderer-neutral visibility source even though complete T027 rendering integration remains outstanding.

T029 behavioral coverage was extended on the current branch with explicit 8/10/12 km cardinal+diagonal horizon-landmark selection and a narrow 10 m semantic structure query proving the proxy path does not depend on a coarse far-terrain sample landing inside its footprint.

## CI history / root causes

Run `33409771197` failed because a Kentridge semantic test was missing its content namespace import; feature work corrected that product cause.

Run `33414406079` repeated the `Int2` compile symptom. Minimal root-cause isolation showed `Int2` is owned by `MountingForce.WorldGen`, not `MountingForce.WorldGen.Content.Kentridge`; production cluster/scatter files were corrected accordingly before another request.

Run `33415875148` correctly targeted its intended feature parent and exposed regression-source compile defects only: two HLOD tests omitted worldgen/Kentridge imports, the Kentridge planning test omitted its content import, and the canopy test used an NUnit containment form that bound to a string overload. Those exact causes were corrected; no third speculative production fix was attempted for the prior symptom.

Run `33418238980` is green for source SHA `8341b488...`: requested `ShowcaseFarStructureSourceTests`, automatic module validation, and the standalone `KentridgePlayableSlice` SceneIssue replay all passed. Its module-validation plan exercised `kentridge-integration` and `spatial-reservations`. The built-player screenshot/log proves startup/runtime compatibility and stable near-surface publication, but the captured camera remained near the Kentridge opening/interior (`DIST landmark=33.2m`), so this run is **not** visual proof of 8/10/12 km structure HLOD, canopy HLOD, transition behavior, or the newly required terrain-distance fidelity captures. The feature has since advanced, so this run is supporting rather than final exact-head evidence.

## Dependency / authority audit

The new engine render contract remains Game-agnostic: `VoxelEngine.Rendering.Api` references Vegetation/AmbientLife/Unity.Mathematics only and `FarWorldRendering.cs` contains render-ready values rather than WorldBuilder intent, storage, or residency state. `Game.WorldBuilder.Api` remains `noEngineReferences=true`; `WorldVisibilityManifest` owns only deterministic descriptors/sector membership and has no voxel/storage/render hooks. Structure removal state is a lightweight CPU-side presentation source keyed by existing semantic identity, while tree damage/sever state continues to come from the existing tree read source. No duplicate voxel/tree authority is introduced by these completed pieces.

## Blockers / remaining gates

T003, T005/T007, T013, T015/T016, T026-T028, and the new T003A-T003D integration/fidelity work require safe edits in large scene/runtime composition surfaces (`VoxelFarTerrain.cs`, `ShowcaseWorld.cs`, `VoxelShowcase.cs`, vegetation population/render wiring). Current connector writes replace complete files and the execution container has no repository checkout, so unsafe wholesale rewrites are not acceptable. Acceptance is unchanged; continue independent tests/audits and record the tooling blocker rather than weakening requirements.

T003C is conditional on measured residual geometry loss after T003A/T003B; the acceptance outcome is not conditional. Final T029-T033 still require final exact-head behavioral validation, purpose-built module-owned built-player far-visibility captures, the canonical Kentridge integration gate, budget/device evidence, cleanup, and documentation before closure. The next CI request must be a direct child of the current feature head and use only `ci-test/fixes/agent-7`.
