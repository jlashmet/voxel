# Render Visibility Architecture Plan

**Status:** Proposed investigation and architecture direction

## Observed behavior

The current renderer has multiple submission/visibility models:

- Voxel terrain and authored voxel structures share the extracted-surface path. `VoxelSurfaceScheduler` already performs camera-frustum selection and LOD ownership before `VoxelRenderPass` stages procedural draws.
- Small semantic vegetation is grouped by kind and submitted with `Graphics.DrawMeshInstanced` in batches of up to 1023 instances. There is no engine-owned per-instance visibility compaction before those calls.
- Healthy procedural trees are combined into 32 m spatial batches and use ordinary `MeshRenderer` + `LODGroup` presentation. Damaged trees can materialize individually.
- Conventional Unity renderers may therefore use Unity visibility facilities while custom populations use separate engine logic.

The Voxel Showcase camera enables Unity occlusion culling, but the scene has no baked occlusion data. No custom Hi-Z/depth-pyramid occlusion stage has been identified in the voxel renderer.

## Acceptance criteria

1. Every high-volume render population has measurable `candidate -> frustum -> occlusion -> submitted` counts.
2. Hidden geometry can be rejected without GPU-to-CPU visibility readback.
3. Terrain/castle, vegetation, trees, and conventional meshes may keep different render backends.
4. Visibility remains presentation-only and never feeds authoritative simulation, collision, replication, or world state.
5. Added visibility work has explicit frame-time budgets before implementation, per Constitution VI.

## Competing hypotheses

**H1 — Universal renderer:** routing all renderables through one custom rendering API produces the best GPU-driven visibility and batching.

**H2 — Shared visibility, multiple backends:** centralize bounds/LOD/occlusion registration and GPU visibility results while letting voxel, vegetation/tree, and Unity-native renderers retain specialized submission paths.

**Next discriminating experiment:** capture Voxel Showcase source-level and GPU-profiler counters by population, then prototype GPU frustum + Hi-Z compaction for vegetation while leaving ordinary Unity renderers unchanged. Compare CPU submission cost, GPU vertex/triangle work, draw count, and total frame time against the current path.

## Selected direction

Proceed with **H2 unless measurement disproves it**. Define a presentation-only visibility service with stable render handles, bounds, LOD metadata, material/batch identity, and occluder/occludee flags. Custom GPU-driven populations consume compacted visible-ID/indirect-argument buffers directly. Conventional compatible renderers should use Unity GPU Resident Drawer / GPU Occlusion Culling where available rather than reimplementing Unity rendering features.

Prioritize:

1. Correct telemetry for generated/resident versus actually submitted triangles.
2. GPU per-instance culling for dense vegetation.
3. Hi-Z occlusion for custom voxel chunks and other custom populations.
4. Evaluate tree batch granularity and Unity GPU-resident rendering before replacing the tree backend.

## Validation gates

- Establish numeric CPU/GPU budgets before optimization work.
- Preserve deterministic CPU-authoritative world state.
- Verify no visibility readback is required on the frame path.
- A/B Voxel Showcase with identical camera/content and report per-population visibility funnels plus CPU/GPU frame cost.
- Do not migrate characters, particles, transparency, water, decals, or other specialized rendering until profiling shows a concrete benefit.
