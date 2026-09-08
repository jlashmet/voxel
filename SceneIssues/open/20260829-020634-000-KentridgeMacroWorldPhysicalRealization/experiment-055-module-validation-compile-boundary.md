# Experiment 055 — module validation compile boundary

## Exact failure
Feature source `fa1eec8cbcca99202a5524ac3f9d2b11ece68767`, CI transport `e495d939238684e0aeb225e305a5bfba208acfd4`, run `34209624757`, artifact `10050245987` failed before player execution while compiling the newly required module-local validation surfaces.

`MacroPhysicalWorldValidation` resolved `FeatureRegionBuild` to an inaccessible type because it imported `VoxelEngine.Structures.Api` but omitted the Runtime namespace that owns the public production region-build implementation. `GpuSurfaceMirrorRelocationValidation` omitted both `VoxelEngine.Structures.Api` and `VoxelEngine.Structures.Runtime`, so feature catalogue/presentation types were unresolved and `FeatureRegionBuild` likewise resolved incorrectly.

## Discriminator
The existing repository-owned `CoarseGpuProductionValidation` already exercises the same production pattern successfully: real WorldBuilder catalogue, `FeatureRegionBuild`, real voxel storage, and `RenderingComposition`, with both `VoxelEngine.Structures.Api` and `VoxelEngine.Structures.Runtime` imported. The relevant validation asmdefs already reference those assemblies, so no visibility or production API change is required.

## Correction
Add only the missing Structures namespace imports to the two new validation fixtures. Do not expose internals, change production generation, alter renderer behavior, widen residency, raise budgets, or weaken acceptance. Re-run exact-SHA CI on the corrected feature head and let the module players determine the next product gate.
