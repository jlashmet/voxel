# CPU rendering backend removal ledger

**SceneIssue:** `20260902-171853-000-GpuRendererProductionRestoration`  
**Purpose:** G14/G15/G16/G18 dependency inventory for the user-authorized GPU-only renderer migration.  
**Starting audit source:** `6532462cfe023657dbcd0764388829ec2b813202` (continue reconciling later branch changes before deletion).

This is a removal ledger, not permission to delete everything whose name contains `Cpu`. Authoritative voxel storage, deterministic generation, collision, simulation, canonical semantic data, and CPU host orchestration required to submit/validate GPU work remain. A file is deleted only after every required rendering responsibility has a GPU/shared replacement and repository references/tests are migrated.

2026-09-06 checkpoint: the mixed cache no longer schedules or executes CPU geometry phases or
allocates `TransvoxelBuildWorkspace`; roughly2,600 implementation lines and the startup CPU
fallback policy are removed. All469 rendering EditMode tests and the48s GPU module pass. Entry
upload helpers, scheduler contiguous arena/draw route, standalone CPU job/workspace/oracle files
and the old class name still require removal. The retained-profile predicate currently remains
only for a legacy test; migrate direct GPU profile coverage before deleting it. See tasks.md for
exact artifacts and the19 pre-existing buffer-finalizer warnings remaining under G11.

## A. Mixed owner: split first, then delete the retired CPU rendering portions

| Path | Current responsibility | Required disposition |
| --- | --- | --- |
| `Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs` | Owns legacy CPU Transvoxel extraction **and** current chunk admission, dirty/version tracking, visibility, GPU-stage orchestration, publication handoff and metrics. GPU eligibility now covers source steps 1/2/4/8; conditional step4 HLOD and step8 have local real-player module proof. | **Do not delete as-is.** Move scheduler/admission/version/visibility/GPU-publication responsibilities into renderer-neutral/GPU-owned components; finish all-ring integration/pressure proof; then delete the CPU meshing/workspace/upload portions and finally the file if no shared responsibility remains. |
| `Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/SurfaceGeometryArena.cs` | Legacy contiguous CPU-upload arena and some transitional GPU/range tests. | Keep only while a production or independent regression consumer needs it. Final GPU-paged path should not retain it solely to support the retired CPU uploader. |
| `Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/GeometryFrameJobCompletionGuard.cs` | Guards completion of CPU geometry jobs. | Delete if reference audit proves it serves only retired CPU surface/water jobs; retain if another non-retired rendering job still uses it. |
| `Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/NearRingExactSnapshotScheduling.cs` | CPU-side exact snapshot scheduling policy. | Determine whether GPU mirror/admission still consumes the policy. Delete if it exists only to feed CPU extraction; otherwise move the minimal shared scheduling contract out of the retired cache. |

## B. Direct migration/delete candidates: production CPU geometry generation

| Path / family | Why it is a deletion target | Migration prerequisite |
| --- | --- | --- |
| `Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuWaterSurfaceChunkCache.cs` and `WaterBrickMeshBatchJob.cs` (+ `.meta`) | **Deleted.** Production water uses `GpuWaterSurfaceChunkCache`; contiguous water-shader input is also removed. | GPU cache/semantic/draw tests replace CPU fixtures. Five fixed vertex digests preserve verified oracle output without CPU extraction code. Water visual acceptance still needs work. |
| `Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/SurfaceBlockHlodMeshJob.cs` (+ `.meta`) | CPU coarse step-8 block HLOD mesh generation. | G07 GPU coarse-LOD equivalent with real mixed-LOD/frontier proof. |
| `Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/Transvoxel/TransvoxelDensityJob.cs` | CPU density reconstruction for surface meshing. | GPU semantic/density coverage plus independent canonical expectations. |
| `.../Transvoxel/TransvoxelTopologyJob.cs` | CPU regular topology emission. | GPU regular/faceted topology proof and independent expected geometry. |
| `.../Transvoxel/TransvoxelCompactJob.cs` | CPU topology compaction. | GPU count/prefix/write path proven equivalent. |
| `.../Transvoxel/FacetedMaskJob.cs`, `FacetedMergeJob.cs`, `SnapshotFacetedMaskJob.cs` | CPU faceted-surface reconstruction/merge. | G06 GPU faceted semantics and mixed-material proof. |
| `.../Transvoxel/TransitionMeshJob.cs` | CPU transition-face meshing. | G07 GPU transition-face/negative-shell ownership proof across real LOD boundaries. |
| `.../Transvoxel/MipDensityJob.cs` | CPU coarse mip density generation for rendering. | Confirm Storage mip data remains authoritative input; move only rendering reconstruction to GPU. |
| `.../Transvoxel/SurfaceBlockHlodSummaryJob.cs` | CPU step-8 rendering summary/HLOD preparation. | Replace the rendering-only summary path on GPU; preserve any canonical world/storage data contract separately. |
| `Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/TransvoxelBuildWorkspace.cs` (+ `.meta`) | Large persistent NativeArray/List workspace dedicated to CPU meshing phases. | Delete after all CPU geometry jobs and transitional CPU-oracle consumers are removed. |
| `Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/Step4FalseEmptyDiagnostics.cs` (+ `.meta`) | Diagnostics tied to the CPU step-4 fallback investigation. | Remove after G07 GPU step-4 coverage and any useful invariant is moved into GPU-facing tests/metrics. |

`ExactSnapshotMetadataJobs.cs` and `ExactSnapshotRegionCoverage.cs` are **not yet classified as delete**: they may be CPU host-side source/version/coverage machinery rather than triangle generation. Re-evaluate after splitting the mixed cache.

## C. Shared canonical data that must not be deleted merely because CPU Transvoxel used it

| Path / family | Keep/migrate rationale |
| --- | --- |
| `TransvoxelRegularTables.cs`, `TransvoxelTransitionTables.cs`, `TransvoxelTableValidator.cs` | Canonical lookup data/validation can feed GPU table packing and independent correctness tests. Preserve or move to a renderer-neutral location if needed. |
| `SmoothSurfaceVertex.cs` | Shared GPU-visible vertex layout; retain while the GPU shader/draw contract uses it. |
| `SolidMaterialClassification.cs` and material/surface/coating contracts | Semantic input shared with GPU extraction; not CPU-renderer implementation. |
| Storage/read/change-journal APIs, voxel cells, material catalogues, profile/coating data | Authoritative world input; explicitly outside the retired renderer. |

## D. Test-only CPU renderer/oracle code: temporary only

The GPU subtree currently contains `CpuDensityOracle.cs`, `CpuTopologyOracle.cs`, `CpuTransitionOracle.cs`, and `CpuVertexAttributeOracle.cs`. They are not production fallback, but the final requirement forbids retaining a hidden second renderer under `Tests`/oracle names. Under G05:

1. extract bounded canonical fixtures, frozen expected outputs, invariants, table-derived/property checks, and provenance;
2. prove the GPU path against those independent expectations;
3. delete any oracle implementation that reproduces the retired CPU mesher algorithm rather than representing compact canonical data.

G05 migration progress: `GpuSurfaceSemanticParityTests.ExactReconstructionEmitsEveryOccupiedBoundaryFace`
uses the existing repeated half-brick input (8-cell extent, source step 1, occupied local y=0..3,
Planar/Sharp/Cubic). Its independent analytic expectation is exactly the y=0/down and y=4/up
8x8 planes, with two complementary outward triangles per unit face, correct material/style,
no duplicate triangles and no off-lattice positions. This expectation comes from discrete occupancy,
not captured GPU output or the CPU mesher. The strengthened assertions passed all three styles in local Metal execution
(`Artifacts/LocalGpuShowcase/stride-fixed/allocator-classified-status.xml`);
they cover faceted geometry only and do not authorize deleting the other semantic oracles yet.

The CPU water configuration/lifetime tests were deleted after the GPU cache gained actual material-publication and deferred-disposal tests; the old source-only Burst-emission check was retired with the job. Water semantic and raster assertions were migrated and retained. Tests whose value is renderer-independent (semantic expectations, LOD ownership, table validity) should be rewritten, not discarded.

## E. Explicitly retained GPU/shared files

The `Assets/VoxelEngine/Rendering/Runtime/GpuVoxel/` production path (mirror, extractor, page arena, draw dispatcher, brick layout/cache preparation, GPU tables/catalogue packing) is the target backend. CPU host code that only submits immutable descriptors, tracks authoritative request identity, polls bounded completion status, or records metrics is allowed; CPU triangle/density/topology extraction and CPU geometry upload are not.

`GpuSurfaceProductionPolicy.cs` and `VOXEL_DISABLE_GPU_CUTOVER` are transitional controls. The final G18 audit removes the CPU-force/experimental compatibility controls after GPU-only capability handling is established; unsupported supported-device capability must fail explicitly rather than silently route to CPU.

## F. Removal acceptance checks

Before checking G16/G18 complete:

- repository search shows no production call path into CPU surface/water meshing, CPU geometry upload, or CPU fallback selection;
- source steps 1/2/4/8 and required water surface work have GPU-backed production coverage rather than disabled rings/content;
- no CPU mesher copy remains under Tests, validation, benchmark, archive, compatibility, or renamed wrappers;
- all deleted `.cs` assets have their `.meta` and serialized/asmdef references cleaned up;
- module-local GPU players, VoxelShowcase, independent production consumer, edits/streaming/restart, and affected editor/bake workflows pass on the CPU-backend-free exact SHA;
- canonical correctness expectations remain independently testable without executing the deleted CPU renderer.

## Current cutover delta

Final solid frustum/LOD selection and selected-handle compaction now execute on GPU. CPU
publication/readiness inputs are persistent and refreshed by versioned changes. CPU ring/source
demand, missing/stale-build priority, far-feature publication proof, water visibility and API
submission remain. Retired solid meshing code/workspaces/arena are still present; do not claim
G16/G18 complete. Regular GPU faceted faces are currently unmerged, unlike CPU `FacetedMergeJob`;
port this geometry-reduction behavior before claiming the planned meshing migration is complete.
