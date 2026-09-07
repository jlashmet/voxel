Incremental demand checkpoint: camera-only GPU feedback no longer rebuilds all host demand.
Only classification deltas and pending ranks apply when live metadata is stable; topology,
settings/readiness changes retain full generation checks.527tests/module pass. Two Showcases
332/282 and332/285FPS; sampled nonzero feedback cost~.12–.13ms versus prior~.6ms. Final repeat
reports zero unqueued demand in164samples and zero allocation failures/evictions, but350missing
versus parent272 and fewer resident chunks remain a coverage/service-latency concern. Do not
claim400FPS or a clean equivalent-workload gain. CPU far replacement, submission costs and
helper retirement remain. Exact evidence is in tasks.md.

Resident far-instance checkpoint: removed per-frame CPU matrix rebuilding and instanced matrix
submission. Transforms/inverses persist on GPU; compute compacts source indices and writes
indirect per-submesh counts. Source updates rebuild batches; unchanged producer queries reuse.
CPU conservative replacement still provides flags and avoids known-empty submissions; this is
an intermediate boundary, not completed GPU handoff.523tests and production module pass;
Showcase322/271FPS,272missing,zero allocation failures/evictions. Stationary did not improve
versus332FPS; walking improved versus256FPS in one run.400FPS remains unmet. Exact evidence and
remaining visual/loading defects are recorded in tasks.md.

GPU demand/reclamation checkpoint: production band/frustum build demand and background distance
ranking now come from bounded asynchronous GPU classification. CPU keeps live source-generation
checks, queue admission and age stamps from GPU tags; cached camera queries avoid repeated feedback.
Removed production CPU candidate bounds classification and per-camera demand scans. Far handoff and
pressure eviction still have CPU presentation decisions; helper/oracle retirement remains.
The migration exposed a pre-existing vertex-page reclamation leak (52 retired pages restored only
one). Explicit free-counter accumulation fixes it.518 tests/module pass; Showcase332/256FPS,
331missing,zero allocation failures/evictions,3913publications. Stationary is slower than350FPS
at the prior checkpoint; walking improves from219FPS. No400FPS/full-visual-acceptance claim.

Far replacement checkpoint: native/standalone timing found1.642ms of CPU far consumer preparation
outside scheduler timing. Replaced repeated fine-cell ancestor walks with bounded coarse-first
proof, retaining the same coverage rule.503 tests/module pass; Showcase350/219FPS,CPU2.71/4.10ms,
363 missing/12 arena failures. This optimization does not retire GPU demand or far handoff host
work; those and400FPS remain incomplete. Temporary profiling removed. Evidence is in tasks.md.

GPU band checkpoint: production render-band and frustum classification now execute together on
GPU. CPU candidate lists persist across camera/projection movement; hierarchy edges persist across
readiness/handle changes. CPU still refreshes cached missing-build urgency and resident ages, and
rebuilds topology for membership changes.502 owned Rendering tests and production module pass;
Showcase231/201FPS,walking traversal .977ms,414 missing/55 arena failures. This is an intermediate
migration, not full CPU retirement or400FPS/visual acceptance. Evidence is in tasks.md.

# CPU rendering backend removal ledger

**SceneIssue:** `20260902-171853-000-GpuRendererProductionRestoration`  
**Purpose:** G14/G15/G16/G18 dependency inventory for the user-authorized GPU-only renderer migration.  
**Starting audit source:** `6532462cfe023657dbcd0764388829ec2b813202` (continue reconciling later branch changes before deletion).

This is a removal ledger, not permission to delete everything whose name contains `Cpu`. Authoritative voxel storage, deterministic generation, collision, simulation, canonical semantic data, and CPU host orchestration required to submit/validate GPU work remain. A file is deleted only after every required rendering responsibility has a GPU/shared replacement and repository references/tests are migrated.

2026-09-06 fine-source checkpoint: production steps1/2/4 now resolve canonical source readiness
and dense-cache entries on GPU; CPU Covers has no production callers. Fine mixed-slot demand and
per-submission readers remain host lifetime responsibilities.488 Rendering tests, module and
Showcase validated;224/184 FPS,zero CPU coverage polls/directory/geometry allocation failures,
but mixed-slot pressure, missing geometry and slower coarse completion remain. See tasks.md.

2026-09-06 GPU source-readiness migration: the production step8 lane no longer calls CPU
CoversSourceRange or expands per-brick request coordinates. The summary shader resolves explicit
canonical block-reference slices and pending/ready GPU keys, generates coordinates, and reports
bounded upload-request indices. GPU decodes air/uniform/water directly from Storage metadata;
only mixed sources need directory payloads. Fine coverage is the next migration target. CPU retains region residency/version approval and canonical source uploads. Fine steps
still use CPU Covers and are the next source-preparation migration target. Mirror capacity and
single-submission queue bounds remain unchanged. See tasks.md for final validation and timings.

2026-09-06 checkpoint: the mixed cache no longer schedules or executes CPU geometry phases or
allocates `TransvoxelBuildWorkspace`; roughly2,600 implementation lines and the startup CPU
fallback policy are removed. All469 rendering EditMode tests and the48s GPU module pass. Entry
upload helpers, scheduler contiguous arena/draw route, standalone CPU job/workspace/oracle files
and the old class name still require removal. The retained-profile predicate currently remains
only for a legacy test; migrate direct GPU profile coverage before deleting it. See tasks.md for
exact artifacts and the19 pre-existing buffer-finalizer warnings remaining under G11.

2026-09-06 subsequent checkpoint: host renamed to `GpuSolidChunkCache` with GUID preserved;
Entry CPU uploads/draws and scheduler CPU arena allocation/queues are removed. Production shader
and render pass are paged-only. Old CPU mesh reconstruction editor capture utilities are deleted.
468 rendering EditMode tests, one shader diagnostic and both standalone module/Showcase pass.
CPU arena allocation is now0; no significant FPS gain. Standalone CPU workspace/jobs/oracles and
GpuSurfaceArenaBridge still exist for legacy regressions and are next to retire. The GPU host's
last retained-profile CPU predicate is test-only and still needs a direct GPU replacement test.

## A. Mixed owner: split first, then delete the retired CPU rendering portions

| Path | Current responsibility | Required disposition |
| --- | --- | --- |
| `Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/GpuSolidChunkCache.cs` (renamed) | GPU admission, dirty/version tracking, candidate visibility and publication host. CPU meshing/workspace/upload/draw phases are removed. A retained-profile predicate remains for one legacy test. | Preserve required GPU host orchestration. Replace the last profile predicate oracle with direct GPU regression coverage; remove remaining obsolete telemetry/helpers as callers migrate. |
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
| `Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/TransvoxelBuildWorkspace.cs` (+ `.meta`) | **Deleted.** No production callers remained after worker retirement. | Removed obsolete workspace sizing/container tests; retained CPU summary/meshing behavioral tests pending their GPU migration. |
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
