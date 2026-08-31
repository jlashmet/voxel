# Far-World Visibility Implementation Tasks

**Baseline analyzed:** `master` at `86506ec2315b18401a82a0e409544537644edaec`

This is the code-grounded implementation delta for `004-far-world-visibility`. It deliberately reuses the existing WorldBuilder semantic structure data, voxel surface scheduler, vegetation/tree world state, and procedural renderers. It does **not** make distant voxel regions resident and does not create duplicate world truth.

## Phase 0 — Correct far-terrain coverage before adding structure HLOD

- [ ] **T001 — Replace heuristic clipmap ring count with guaranteed-coverage math.**
  - **Modify:** `Assets/Game/Composition/Showcase/SceneRuntime/VoxelFarTerrain.cs`
  - Extract testable helpers for ring spacing, ring half-extent, camera-snap loss, and guaranteed coverage.
  - Replace `RingCount()`'s `innerRadius * 2` doubling heuristic with selection of the minimum ring count whose **worst-case snapped authoritative extent** covers `m_OuterRadiusMetres` on every cardinal side.
  - Keep `MaxRings` as a guard, but emit an explicit failure/diagnostic when the requested range cannot be covered rather than silently under-covering it.
  - Do not hard-code a six-ring answer; calculate from inner radius, resolution, requested outer radius, and snap spacing.
  - **Regression:** new `Assets/Tests/EditMode/VoxelFarTerrainCoverageTests.cs` proves the shipped `409.6 m -> 12,000 m` configuration has at least 12 km guaranteed coverage for representative and worst-case camera snap phases.

- [ ] **T002 — Retire the startup fallback only after authoritative coverage is complete.**
  - **Modify:** `Assets/Game/Composition/Showcase/SceneRuntime/VoxelFarTerrain.cs`
  - Replace the current rule "last allocated ring published => disable startup fallback" with "all rings required for requested guaranteed coverage are published and collectively cover the requested extent".
  - Preserve fallback coverage while outer authoritative rings are still pending.
  - **Regression:** `VoxelFarTerrainCoverageTests` verifies there is no frame/state transition where requested far coverage shrinks when the fallback is removed.

- [ ] **T003 — Expose coverage diagnostics needed to validate the system.**
  - **Modify:** `VoxelFarTerrain.cs` and the existing Showcase debug/diagnostic surface used by the scene.
  - Report requested outer radius, guaranteed authoritative radius, ring count, per-ring spacing, and whether startup fallback is active.
  - Diagnostics are presentation/debug data only; they must not become authoritative world state.

## Phase 1 — Produce semantic far-structure data before voxel residency

- [ ] **T004 — Add a renderer-neutral far-presentation descriptor derived from existing WorldBuilder facts.**
  - **Add:** `Assets/Game/WorldBuilder/Generation/Architecture/StructureFarPresentation.cs`.
  - Add a compact deterministic record containing only data needed to reconstruct/select a distant exterior proxy: stable structure key, world bounds/footprint, height, facing, structure/archetype key, architecture/material-family key, settlement/cluster key, visibility class, and deterministic revision/hash.
  - Add `StructureVisibilityClass` with semantic categories sufficient for policy selection (ordinary structure, settlement anchor, landmark, horizon landmark).
  - Add a resolver that consumes the existing `StructureIntent`, `StructureForm`, `StructureSiteGeometry`, and applicable `StructureGeometryProfile`/theme data. Do **not** add `Mesh`, `Material`, `GameObject`, renderer, or camera dependencies to WorldBuilder generation.
  - **Regression:** identical planning inputs produce byte/value-equivalent far descriptors; bounds/facing agree with `StructureSiteGeometry`; landmark classification is semantic/config-driven rather than scene-coordinate-driven.

- [ ] **T005 — Carry far-presentation records in existing planning results instead of reconstructing them from voxels.**
  - **Modify:** the existing WorldBuilder planning result types in `Assets/Game/Composition/WorldBuilderWorldGen/Runtime/KentridgeCampaignWorldRealization.cs` and the structure/castle planning result types returned by `StructuresComposition.PlanCastle(...)`.
  - Populate far descriptors at planning time, when structure intent/form/site geometry is already known.
  - Do not wait for physical voxel realization and do not scan voxel storage to discover known semantic structures.
  - **Regression:** a planned castle/house has a stable far descriptor before any intersecting voxel region is generated.

## Phase 2 — Add a spatial visibility manifest without duplicating world truth

- [ ] **T006 — Add a deterministic spatial index for semantic far descriptors.**
  - **Add:** `Assets/Game/WorldBuilder/Api/IWorldVisibilitySource.cs` and a concrete implementation under `Assets/Game/WorldBuilder/Runtime/` (for example `WorldVisibilityManifest.cs`).
  - Store lightweight references/descriptors only; the manifest must not own voxel bricks, interiors, colliders, NPCs, physics, or render objects.
  - Index records by deterministic integer world sectors and allow bounds/sector queries without loading voxel regions.
  - Structures crossing sector boundaries must be returned once; query output must have deterministic ordering independent of insertion/hash-map order.
  - Support stable replacement/removal by structure key so regenerated or persistently changed semantic structures do not duplicate.
  - **Regression:** new `Assets/Tests/EditMode/WorldVisibilityManifestTests.cs` covers cross-sector queries, deterministic order, replacement/removal, and verifies queries do not call region generation/storage residency paths.

- [ ] **T007 — Register planned Showcase landmarks before their voxel regions are queued/generated.**
  - **Modify:** `Assets/Game/Composition/Showcase/ShowcaseWorld.cs`.
  - Own or receive an `IWorldVisibilitySource`/manifest for the Showcase composition.
  - In the existing `QueueLandmarks()` / castle-plan flow, register the planned castle's semantic far descriptor immediately after planning and **before** `_castleRegions` are required to become resident.
  - Keep `StepLandmarks()` responsible for physical voxel realization only; it must no longer be a prerequisite for distant visual existence.
  - Expose the read-only visibility source to scene composition.
  - **Regression:** after castle planning but before any castle region has completed generation, a query around the castle returns its far descriptor.

- [ ] **T008 — Populate the same visibility source from Kentridge/campaign planning.**
  - **Modify:** `Assets/Game/Composition/WorldBuilderWorldGen/Runtime/KentridgeCampaignWorldRealization.cs` and its composition handoff.
  - Register the existing planned settlement structures using their existing IDs/site geometry; do not create a second Kentridge-specific structure database.
  - Ensure settlement, landmark, and ordinary-house records exist as soon as deterministic planning finishes, independent of later voxel physical realization.
  - **Regression:** campaign plan output can enumerate/query the planned settlement without generating its voxel regions.

## Phase 3 — Add an engine rendering contract and independent far-structure renderer

- [ ] **T009 — Add a Game-agnostic far-structure rendering API.**
  - **Add:** `Assets/VoxelEngine/Rendering/Api/FarWorldRendering.cs` (or equivalent file in the existing Rendering API assembly).
  - Define render-ready types such as `FarStructureInstance`, `FarStructureTier`, and `IFarStructureRenderer`.
  - The engine-facing instance may contain stable ID, transform/bounds, proxy/archetype key, material/style key, selected tier, and compact visual-state flags, but must not reference WorldBuilder `StructureIntent` or Game composition types.
  - Preserve assembly direction: Game/WorldBuilder adapts semantic records **into** the VoxelEngine rendering API; VoxelEngine Rendering must not depend on Game/WorldBuilder.

- [ ] **T010 — Add a composition adapter from semantic records to render-ready instances.**
  - **Add:** a Showcase composition class under `Assets/Game/Composition/Showcase/SceneRuntime/` (for example `ShowcaseFarStructureSource.cs`).
  - Query `IWorldVisibilitySource` around the camera, apply configured visibility policy, and convert selected `StructureFarPresentation` values into `FarStructureInstance` values.
  - This adapter is the boundary for scene/game policy. It must not request full voxel regions merely to render a distant structure.
  - Keep structure IDs stable across queries so renderer caches and handoffs do not churn as the camera moves.

- [ ] **T011 — Implement cached low-poly semantic structure proxies.**
  - **Add:** runtime implementation under `Assets/VoxelEngine/Rendering/Runtime/FarWorld/` (for example `ProceduralFarStructureRenderer.cs`).
  - Build/cache proxy archetype meshes by semantic proxy key rather than creating a Unity `GameObject` per distant building.
  - Provide progressively cheaper exterior massing tiers: a recognizable mid proxy, a coarse far proxy, and a horizon/landmark silhouette where policy requests one.
  - Houses must retain wall/roof massing; castles/large landmarks must retain major wall, keep, tower, and roof silhouette masses. Interiors, collision, physics, gameplay scripts, and voxel bricks are excluded.
  - Batch compatible proxies with GPU instancing or the repository's existing low-overhead draw pattern. Stable camera motion must not rebuild immutable proxy meshes each frame.
  - **Regression:** new `Assets/Tests/EditMode/FarStructureVisibilityTests.cs` verifies deterministic tier/archetype selection, stable batching keys, and absence of per-instance persistent GameObjects.

## Phase 4 — Select HLOD by projected significance and make handoffs stable

- [ ] **T012 — Add a configurable screen-space/semantic visibility policy.**
  - **Add:** a rendering-policy API/config type (for example `FarWorldVisibilityPolicy`) and instantiate/configure it in Game composition.
  - Compute projected significance from camera/FOV/viewport and world bounds; use semantic visibility class to set minimum/maximum allowed representation.
  - Add separate enter/exit thresholds (hysteresis) so a house/cluster/landmark does not oscillate tiers at a boundary.
  - Keep concrete distance caps and quality thresholds in composition/config, not hard-coded in the shared renderer.
  - A large castle may remain visible near the horizon while an ordinary sub-pixel house may cull.
  - **Regression:** boundary tests move a camera back/forth around thresholds and prove stable tier selection.

- [ ] **T013 — Add readiness-aware near-voxel/proxy handoff.**
  - **Modify:** `Assets/Game/Composition/Showcase/SceneRuntime/VoxelShowcase.cs` and the narrowest existing rendering-composition/surface-scheduler API needed to expose whether the near voxel surface covering a semantic structure's footprint is ready.
  - **Only if necessary, minimally modify:** `Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/VoxelSurfaceScheduler.cs` to expose readiness; do **not** change its source-step/ring LOD algorithm as part of this feature.
  - Keep the far proxy visible while near voxel regions are resident but their meshes are still pending.
  - Hide the proxy only when all required intersecting near surfaces are ready; on unload/eviction, re-enable the proxy before the near representation disappears.
  - Use hysteresis/readiness to avoid visible holes and prolonged double-rendering.
  - **Regression:** approach and retreat across the resident-world boundary without a missing castle/house frame.

## Phase 5 — Stop using terrain vertices as the primary semantic-building representation

- [ ] **T014 — Split `FarFieldStructureStore` into semantic-independent fallback channels.**
  - **Modify:** `Assets/Game/Composition/Showcase/FarFieldStructureStore.cs`.
  - Preserve its useful responsibilities: authored terrain lowering/material overrides and anonymous/arbitrary voxel silhouette fallback that cannot be reconstructed from a semantic plan.
  - Add an explicit way to exclude/suppress known semantic structures from the built-silhouette channel once those structures have independent proxies (for example capture mode, semantic exclusion bounds/keys, or separate internal channels).
  - Do not delete the store: arbitrary sculpts/player-created or legacy voxel forms still need a coarse far fallback.
  - Preserve deterministic `HeightAt`/surface-deviation behavior for terrain alterations.

- [ ] **T015 — Make `VoxelFarTerrain` consume only terrain/surface fallback, not semantic structure identity.**
  - **Modify:** `Assets/Game/Composition/Showcase/SceneRuntime/VoxelFarTerrain.cs`, `VoxelShowcase.cs`, and `ShowcaseWorld.cs`.
  - Replace/rename the current generic `Structures` dependency with a surface-deviation/fallback contract whose meaning excludes semantic buildings handled by the proxy renderer.
  - The terrain clipmap may still incorporate lowered/raised nonsemantic voxel surface deviations, but a castle/house must not depend on a 12.8–204.8 m terrain vertex landing inside its footprint to exist visually.
  - **Regression:** a ~100 m castle remains represented at ~10 km even when no outer clipmap sample vertex falls inside its footprint; terrain sculpts still affect far terrain.

- [ ] **T016 — Remove double representation for semantic castle/Kentridge structures only after proxy parity.**
  - **Modify:** `ShowcaseWorld.cs` far-field capture calls and semantic capture/exclusion bookkeeping.
  - Known planned buildings use semantic proxy rendering; do not also raise the far-terrain heightfield for the same structure.
  - Continue capturing nonsemantic terrain edits/anonymous voxel structures.
  - **Regression:** castle/house renders once, while an anonymous authored voxel tower and an authored terrain cut still retain far fallback behavior.

## Phase 6 — Add settlement HLOD so distant towns do not render every house independently

- [ ] **T017 — Build deterministic settlement/neighborhood cluster descriptors from existing structure records.**
  - **Add:** a WorldBuilder generation/runtime cluster builder adjacent to the visibility manifest.
  - Cluster by existing settlement/neighborhood membership and deterministic spatial sectors; assign stable cluster IDs independent of camera position.
  - Produce aggregate bounds/massing/style information sufficient for a coarse village/town silhouette.
  - Keep semantically important members such as churches, towers, keeps, or other `SettlementAnchor`/`Landmark` structures independently addressable so clustering does not erase them.
  - **Regression:** repeated generation creates identical cluster membership/IDs; a structure crossing a sector boundary is not duplicated.

- [ ] **T018 — Switch between individual structure proxies and cluster HLOD in the far source/renderer.**
  - **Modify:** the T010 far-structure source, T011 renderer, and T012 visibility policy.
  - Mid range: draw selected individual house/building proxies.
  - Far range: draw cluster massing plus independently significant anchors/landmarks.
  - Add readiness/hysteresis so cluster and members do not flicker or double-render during the handoff.
  - **Regression:** a dense hillside settlement has a bounded far draw/instance count while preserving settlement density, roofline, and major landmark silhouettes.

## Phase 7 — Add far tiers around the existing vegetation truth/rendering systems

- [ ] **T019 — Add deterministic spatial queries for existing vegetation/tree instances.**
  - **Modify/add around:** `Assets/VoxelEngine/Vegetation/Api/VegetationPlacement.cs`, existing tree read-source APIs in `Assets/VoxelEngine/Vegetation/Runtime/TreeWorldState.cs`, and Showcase population composition.
  - Add/query deterministic sector membership for render visibility without creating a second tree or plant ownership model.
  - Existing tree IDs/skeleton/damage/sever state remain authoritative; lightweight vegetation remains deterministic placement data.
  - Queries return stable IDs/order and do not force voxel region residency.
  - **Regression:** same seed/sector returns same vegetation membership/order; moved camera changes queried sectors without mutating world truth.

- [ ] **T020 — Make `ProceduralVegetationBatchRenderer` consume visible/tiered subsets rather than one whole-world flat list.**
  - **Modify:** `Assets/VoxelEngine/Rendering/Runtime/Vegetation/ProceduralVegetationBatchRenderer.cs` and the `IVegetationBatchRenderer` API only as needed.
  - Preserve existing mesh/material cache and GPU-instanced batching.
  - Feed it camera-selected sector/tier batches (or an equivalent visible-instance update) so it does not blindly maintain/draw every non-grass world instance at all distances.
  - Keep semantic placement rules outside the renderer.
  - **Regression:** invisible sectors produce no draw instances; batch keys/material reuse remain stable when sectors enter/leave visibility.

- [ ] **T021 — Add simplified tree proxy tiers on top of `ProceduralTreeRenderer`/tree read state.**
  - **Modify:** `Assets/VoxelEngine/Rendering/Runtime/Vegetation/ProceduralTreeRenderer.cs` and its composition adapter.
  - Near: retain the existing full procedural tree presentation.
  - Mid: use simplified trunk/crown silhouette derived from the same stable tree identity/species parameters.
  - Far: allow the tree to participate in deterministic forest-canopy clusters instead of drawing full individual geometry.
  - Severed/destroyed trees must disappear/change through the existing `TreeWorldState`; do not add a duplicate persistent tree-status table.
  - Promote semantically exceptional/giant trees to landmark visibility rather than forcing all trees to the same max distance.

- [ ] **T022 — Add deterministic forest-canopy HLOD clusters.**
  - **Add:** a vegetation cluster builder and lightweight canopy proxy path using the same sector scheme as T019.
  - Build stable canopy/treeline massing from member trees; do not regenerate cluster identity based on camera location.
  - Exclude/promote landmark trees so they can retain an independent silhouette.
  - Invalidate/rebuild only affected cluster presentation when persistent tree state materially changes.
  - **Regression:** a dense far forest uses bounded cluster proxies, preserves deterministic skyline/density, and does not pop between unrelated cluster layouts as the camera moves.

- [ ] **T023 — Route boulders/other natural scatter through the same deterministic sector/visibility pattern.**
  - **Modify an existing deterministic boulder/scatter placement source if present at implementation time; otherwise add a renderer-neutral scatter descriptor/index under WorldBuilder rather than making distant boulders voxel-resident.**
  - Reuse the far visibility policy and instanced proxy renderer pattern; do not create a separate camera-specific world truth.
  - Promote exceptional rock spires/megafeatures to landmark records; ordinary sub-pixel boulders cull/cluster according to policy.
  - **Regression:** natural scatter is deterministic by world sector and does not require distant voxel region generation.

## Phase 8 — Persist coarse visual state without retaining full distant voxels

- [ ] **T024 — Add lightweight semantic structure visual-state persistence keyed by existing stable structure ID.**
  - **Add:** a Game/WorldBuilder-side state source for coarse far presentation state (at minimum intact vs removed/ruined; add finer damaged states only where authoritative gameplay already supplies them).
  - Update it from authoritative CPU/world events when nearby voxel destruction changes a semantic structure enough to alter its far silhouette. GPU/render output must never be the source of truth.
  - Feed the state through the T010 adapter into the far renderer so a destroyed landmark does not reappear when viewed from distance.
  - Do not store the destroyed structure's full voxel volume solely for far rendering.
  - **Regression:** destroy/remove a semantic landmark, unload its voxel regions, query/render it from far away, and observe the persisted coarse state.

- [ ] **T025 — Reuse existing tree state for far proxy invalidation.**
  - **Modify:** the T019/T021/T022 query/adapters only.
  - Read existing tree damage/sever/removal state from `TreeWorldState`; invalidate the affected individual/cluster proxy when that state changes.
  - Explicitly do **not** add a second tree persistence system.

## Phase 9 — Wire the complete system into composition

- [ ] **T026 — Integrate far structures into `VoxelShowcase`.**
  - **Modify:** `Assets/Game/Composition/Showcase/SceneRuntime/VoxelShowcase.cs`.
  - Instantiate/receive the visibility manifest/source, far-structure source/adapter, renderer, and visibility policy.
  - Update visible far structures from the scene camera without generating distant voxel regions.
  - Keep `VoxelFarTerrain` responsible for terrain/surface fallback only.
  - Dispose renderer/cache resources through the existing scene lifetime path.
  - Add debug toggles/metrics through existing Showcase diagnostics rather than scene-specific ad-hoc GameObjects.

- [ ] **T027 — Integrate the same contracts into Kentridge/macro-world composition.**
  - **Modify:** `KentridgeCampaignWorldRealization.cs` and the composition code that consumes its plan.
  - Use the same visibility manifest, render adapter contract, and policy types as Showcase; do not create a Kentridge-only far renderer.
  - Planned settlement structures must be far-visible before physical voxel regions are realized.
  - **Reuse proof:** a second consumer/fixture outside the original Showcase castle path renders/query-selects semantic proxies from the shared contracts.

- [ ] **T028 — Integrate vegetation/scatter far visibility into Showcase composition.**
  - **Modify:** `Assets/Game/Composition/Showcase/SceneRuntime/ShowcaseTreePopulation.cs`, `VegetationRenderingShowcase.cs`, and composition wiring.
  - Replace the single all-world flat render submission with sector/tier-aware visible submissions while leaving deterministic generation/tree state ownership unchanged.
  - Feed giant-tree/natural-landmark records into the same semantic visibility policy where applicable.

## Phase 10 — Behavioral, visual, and budget validation

- [ ] **T029 — Add the complete EditMode behavioral regression suite.**
  - **Add/extend:**
    - `Assets/Tests/EditMode/VoxelFarTerrainCoverageTests.cs`
    - `Assets/Tests/EditMode/WorldVisibilityManifestTests.cs`
    - `Assets/Tests/EditMode/FarStructureVisibilityTests.cs`
    - `Assets/Tests/EditMode/FarVegetationVisibilityTests.cs`
  - Required cases:
    1. 12 km guaranteed terrain coverage across camera snap phases.
    2. Fallback retirement cannot reduce coverage.
    3. Castle proxy selection at 8 km, 10 km, and 12 km.
    4. Never-visited castle exists in the visibility manifest without voxel generation.
    5. A structure narrower than the outer terrain grid spacing is not lost.
    6. Near/proxy approach and retreat handoff has no representation hole.
    7. Dense settlement deterministically switches between members and cluster HLOD.
    8. Manifest/cluster/vegetation query order and hashes are deterministic.
    9. Terrain sculpts and anonymous voxel fallback still work after semantic separation.
    10. Existing tree sever/removal state propagates into individual/canopy far presentation.
  - Use behavioral assertions; do not use source-string assertions as proof.

- [ ] **T030 — Add built-player visibility fixtures/evidence for the actual perceptual requirements.**
  - Create/extend SceneIssue validation scenes using the canonical `SceneIssues/README.md` workflow rather than treating unit tests as visual acceptance.
  - Required fixtures/captures: castle at 8/10/12 km; approach/retreat through proxy-to-voxel handoff; hillside settlement with many houses; forest valley; exceptional giant tree/rock landmark; persisted destroyed landmark.
  - Move/rotate the camera through clipmap snap boundaries and HLOD thresholds; inspect for popping, holes, double images, terrain/building intersections, silhouette loss, and unstable cluster layouts.
  - Rendered evidence must meet the repository's production-quality bar.

- [ ] **T031 — Validate CPU/GPU/memory cost against the authoritative device matrix.**
  - Instrument/query existing metrics for: far-terrain ring builds, structure records queried, individual/cluster proxies selected, instanced batches/draw calls, vegetation sectors/instances/clusters, cache sizes, and handoff counts.
  - Profile representative dense settlement + forest views on supported tiers against `specs/001-destructible-voxel-engine/device-matrix.md`.
  - Device tiers may reduce presentation complexity/thresholds only; they must not change deterministic world truth, gameplay interest radius, collision, or authoritative simulation.

## Phase 11 — Remove obsolete semantic-heightfield behavior after parity is proven

- [ ] **T032 — Remove the legacy requirement that known semantic buildings be captured into `FarFieldStructureStore`.**
  - **Modify:** `ShowcaseWorld.cs` and `FarFieldStructureStore.cs` after T029–T031 pass.
  - Stop castle/Kentridge semantic visibility from depending on post-build `CaptureRegion()`.
  - Retain capture for terrain deviation and anonymous/arbitrary voxel forms.
  - Remove `ApplyGuaranteedSentinelOverlay()` only if its validation purpose is replaced by deterministic regressions; otherwise keep it isolated as validation-only behavior.

- [ ] **T033 — Update the far-world architecture docs to match final code boundaries and measured limits.**
  - **Modify:** `specs/004-far-world-visibility/plan.md` and `architecture-proposal.md` only where implementation evidence changes the proposal.
  - Record final policy/config ownership, actual guaranteed far-terrain coverage, proxy/cluster handoff rules, persistent-state ownership, and measured budget results.
  - Do not leave documentation claiming that the old five-ring heuristic guarantees 12 km if the final math/config differs.

## Dependency / implementation order

1. **T001–T003** can land first and independently: they fix a current far-terrain coverage defect and provide diagnostics.
2. **T004–T008** establish semantic data before residency and the shared spatial query source.
3. **T009–T016** provide independent structure rendering, stable near/far handoff, and remove semantic dependence on terrain sampling.
4. **T017–T018** add settlement HLOD after individual semantic proxies work.
5. **T019–T023** add vegetation/natural scatter tiers by extending existing state/rendering systems.
6. **T024–T025** connect authoritative persistent coarse state.
7. **T026–T028** complete composition integration and prove reuse outside the original castle path.
8. **T029–T031** are required behavioral, built-player, and budget gates.
9. **T032–T033** are cleanup/documentation only after parity and validation are proven.

## Explicit non-tasks / prohibited shortcuts

- Do **not** increase the resident voxel load radius to 10–12 km.
- Do **not** keep full houses, castles, interiors, colliders, NPCs, or voxel bricks loaded merely because they are visible at distance.
- Do **not** make a semantic castle/house depend on sparse outer `VoxelFarTerrain` vertex sampling.
- Do **not** create a second structure truth alongside existing WorldBuilder planning outputs.
- Do **not** create a second persistent tree state alongside `TreeWorldState`.
- Do **not** put Unity rendering objects into WorldBuilder generation contracts.
- Do **not** derive persistent/deterministic world state from GPU output or camera-dependent HLOD results.
- Do **not** hard-code a ring count to repair 12 km coverage; compute guaranteed coverage from actual clipmap geometry and snapping.
- Do **not** delete `FarFieldStructureStore` until terrain-sculpt and anonymous/arbitrary voxel fallback behavior is proven elsewhere.
- Do **not** change `VoxelSurfaceScheduler`'s near source-step/ring LOD algorithm unless an independent demonstrated defect requires it; this feature needs, at most, a readiness handoff.
- Do **not** encode Showcase/Kentridge coordinates or one-off scene policy in shared engine APIs.
- Do **not** weaken the device-matrix budgets or deterministic-authority rules to make the new presentation system pass.
