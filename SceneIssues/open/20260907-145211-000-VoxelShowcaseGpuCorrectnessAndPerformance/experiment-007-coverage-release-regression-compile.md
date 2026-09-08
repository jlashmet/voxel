# Experiment 007 — coverage-release regression compile repair

## Hypothesis

The exact request after the coordinator-coverage lifetime repair should compile and run the focused ownership regression plus automatically required Rendering validation. If it fails before execution, classify the exact compiler error before changing source or retrying CI.

## Exact source and request

- Feature source: `cfb9ac7f2bf081b1b73bf111e7023fae04430d3a`
- Targeted-CI request: `723c0d7ae0780888b2f0b12aa1b31d4b9be4305b`
- Workflow run/job: `34177505326` / `101909745712`
- Requested test: `VoxelEngine.Rendering.Tests.EditMode.GpuPersistentSourceDemandOwnershipTests`
- Platform: `EditMode`
- SceneIssue replay: 180 seconds
- Artifact: `single-test-34177505326`, id `10039563331`, digest `sha256:7369db9e535264b991e25343b32e4749d45d216be5f993e52d5e54e29562fa7f`

## Evidence

The exact preserved request was left untouched while queued. When terminal, both automatic Rendering module validation and the standalone VoxelShowcase build failed before test/player execution because Unity reported script compiler errors.

The exact error in both `ModuleValidation/Results/Tests/Persistent/persistent.log` and `SceneIssue/player-build.log` is:

`Assets/VoxelEngine/Rendering/Tests/EditMode/GpuPersistentSourceDemandOwnershipTests.cs(124,30): error CS0103: The name 'GpuSolidChunkCache' does not exist in the current context`

The new regression `ReleaseDropsCoordinatorCoverageWithoutAdmissionOwnership` references `GpuSolidChunkCache.CellsPerAxis`. `GpuSolidChunkCache` lives in `VoxelEngine.Rendering.Runtime.SurfaceExtraction`, but the test file imported only the GPU-voxel runtime namespace. This is a deterministic product/test compile defect on the feature source, not infrastructure.

## Repair

Commit `7700c66253aeac96ceb9303adb3eac157e0c3ce1` adds only the missing `using VoxelEngine.Rendering.Runtime.SurfaceExtraction;` import to `GpuPersistentSourceDemandOwnershipTests.cs`. No renderer behavior, budgets, content, distance, or CI limits changed.

## Verdict and next step

Request `723c0d7...` is a terminal product failure and must not be retried unchanged. Issue a new exact-SHA targeted request only from the feature head containing the compile repair and this evidence, then inspect its exact module/player results before advancing acceptance.
