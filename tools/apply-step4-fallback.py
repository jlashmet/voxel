#!/usr/bin/env python3
from pathlib import Path


def replace_one(path: Path, old: str, new: str) -> None:
    text = path.read_text()
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{path}: expected exactly one match, found {count}: {old[:100]!r}")
    path.write_text(text.replace(old, new, 1))


root = Path(__file__).resolve().parents[1]
cache = root / "Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs"
workspace = root / "Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/TransvoxelBuildWorkspace.cs"
plan = root / ".claude/plans/voxel-showcase-rendering-repair-v2.md"

replace_one(
    workspace,
    """        internal TransvoxelBuildWorkspace(int gridSampleCount, int brickCacheCount,\n                                          bool samplesFromMips, bool usesBlockHlod,\n                                          int hlodCoreBrickEdge, int cellsPerAxis,\n""",
    """        internal TransvoxelBuildWorkspace(int gridSampleCount, int brickCacheCount,\n                                          bool samplesFromMips, bool usesBlockHlod,\n                                          bool supportsFeaturePreservingFallback,\n                                          int hlodCoreBrickEdge, int cellsPerAxis,\n""",
)

replace_one(
    workspace,
    """            if (usesBlockHlod)\n            {\n                HlodSummaries = new NativeArray<SurfaceBlockHlodSummary>(\n""",
    """            if (usesBlockHlod || supportsFeaturePreservingFallback)\n            {\n                HlodSummaries = new NativeArray<SurfaceBlockHlodSummary>(\n""",
)

replace_one(
    workspace,
    """            int finalVertexCapacity = usesBlockHlod ? HlodVertexCapacity : 32_768;\n            int finalIndexCapacity = usesBlockHlod ? HlodIndexCapacity : 49_152;\n""",
    """            // A step-4 false-empty fallback covers 256 voxels at two-voxel subcells: a\n            // 128^3 subcell chunk, exactly the resolution the original fixed HLOD budget covered.\n            // Keep that smaller baseline budget on step-4 workers; only the always-HLOD step-8\n            // workers need the four-times-larger 256^3 output capacity.\n            int finalVertexCapacity = usesBlockHlod\n                ? HlodVertexCapacity\n                : supportsFeaturePreservingFallback ? BaselineHlodVertexCapacity : 32_768;\n            int finalIndexCapacity = usesBlockHlod\n                ? HlodIndexCapacity\n                : supportsFeaturePreservingFallback ? BaselineHlodIndexCapacity : 49_152;\n""",
)

replace_one(
    cache,
    """        public bool UsesBlockHlod => SourceStep == VoxelReadGrid.BlockEdge;\n\n        /// <summary>Chunk geometry of the base ring (SourceStep 1). Authoring and capture tools\n""",
    """        public bool UsesBlockHlod => SourceStep == VoxelReadGrid.BlockEdge;\n\n        /// <summary>\n        /// Step 4 normally keeps its existing Transvoxel/faceted path, but exact classification\n        /// can prove that thin owned solids exist between the four-voxel lattice samples. Those\n        /// rare false-empty results use the already feature-preserving two-voxel block summary\n        /// before authoritative empty publication.\n        /// </summary>\n        private bool SupportsFeaturePreservingFallback => SourceStep == 4;\n\n        private static bool RequiresFeaturePreservingFallback(\n            int sourceStep, bool hasOwnedSolid, int topologyIndexCount, int facetedIndexCount) =>\n            sourceStep == 4 && hasOwnedSolid\n            && topologyIndexCount == 0 && facetedIndexCount == 0;\n\n        /// <summary>Chunk geometry of the base ring (SourceStep 1). Authoring and capture tools\n""",
)

replace_one(
    cache,
    """            _workspace = new TransvoxelBuildWorkspace(\n                GridSampleCount, BrickCacheCount, SamplesFromMips, UsesBlockHlod,\n                BricksPerAxis, CellsPerAxis, FaceSamplesPerAxis);\n""",
    """            _workspace = new TransvoxelBuildWorkspace(\n                GridSampleCount, BrickCacheCount, SamplesFromMips, UsesBlockHlod,\n                SupportsFeaturePreservingFallback, BricksPerAxis, CellsPerAxis,\n                FaceSamplesPerAxis);\n""",
)

replace_one(
    cache,
    """                    _facetedMaskJobScheduled = false;\n                    _facetedMergeJobScheduled = false;\n                    BeginCompletedResultAppend(includeTopology: true);\n                    _build.Phase = 6;\n                    continue;\n""",
    """                    _facetedMaskJobScheduled = false;\n                    _facetedMergeJobScheduled = false;\n                    if (_buildProfileBlocks.Length == 0\n                        && RequiresFeaturePreservingFallback(\n                            SourceStep, _build.HasOwnedSolid,\n                            _compactedTopologyIndices.Length, _facetedIndices.Length))\n                    {\n                        ScheduleFeaturePreservingHlod(voxelSize);\n                        _build.Phase = 7;\n                        continue;\n                    }\n                    BeginCompletedResultAppend(includeTopology: true);\n                    _build.Phase = 6;\n                    continue;\n""",
)

replace_one(
    cache,
    """                    _facetedTurnaroundTiming.Add(ElapsedMs(_build.FacetedScheduledSeconds));\n                    _facetedMaskJobScheduled = false;\n                    _facetedMergeJobScheduled = false;\n                    BeginCompletedResultAppend(includeTopology: false);\n                    _build.Phase = 6;\n                    continue;\n""",
    """                    _facetedTurnaroundTiming.Add(ElapsedMs(_build.FacetedScheduledSeconds));\n                    _facetedMaskJobScheduled = false;\n                    _facetedMergeJobScheduled = false;\n                    if (_buildProfileBlocks.Length == 0\n                        && RequiresFeaturePreservingFallback(\n                            SourceStep, _build.HasOwnedSolid, 0, _facetedIndices.Length))\n                    {\n                        ScheduleFeaturePreservingHlod(voxelSize);\n                        _build.Phase = 7;\n                        continue;\n                    }\n                    BeginCompletedResultAppend(includeTopology: false);\n                    _build.Phase = 6;\n                    continue;\n""",
)

old_hlod = """            if (UsesBlockHlod)\n            {\n                _hlodOverflow[0] = 0;\n                JobHandle summaryHandle = new SurfaceBlockHlodSummaryJob\n                {\n                    Bricks = _densityBricks,\n                    MixedVoxels = PinnedMixedVoxelsOrFallback(),\n                    Summaries = _hlodSummaries,\n                }.Schedule(BrickCacheCount, 256);\n                _hlodJobHandle = new SurfaceBlockHlodMeshJob\n                {\n                    Summaries = _hlodSummaries,\n                    SummaryGridEdge = BrickCacheEdge,\n                    PaddingBricks = BrickCachePadding,\n                    CoreBrickEdge = BricksPerAxis,\n                    CoreOriginVoxel = chunkOriginVoxel,\n                    VoxelSize = voxelSize,\n                    MaskScratch = _hlodMaskScratch,\n                    Vertices = _vertices,\n                    Indices = _indices,\n                    Overflow = _hlodOverflow,\n                }.Schedule(summaryHandle);\n                _hlodJobScheduled = true;\n                _build.HasOwnedSolid = true; // resolved from final HLOD output on completion\n"""
new_hlod = """            if (UsesBlockHlod)\n            {\n                ScheduleFeaturePreservingHlod(voxelSize);\n                _build.HasOwnedSolid = true; // resolved from final HLOD output on completion\n"""
replace_one(cache, old_hlod, new_hlod)

replace_one(
    cache,
    """        private void ScheduleExactMetadataSnapshot(IRegionReadSource source, int3 cacheOrigin)\n        {\n""",
    """        private void ScheduleFeaturePreservingHlod(float voxelSize)\n        {\n            if (!_hlodSummaries.IsCreated || !_hlodMaskScratch.IsCreated || !_hlodOverflow.IsCreated)\n                throw new InvalidOperationException(\n                    $\"Feature-preserving HLOD scratch was not allocated for source step {SourceStep}.\");\n\n            _hlodOverflow[0] = 0;\n            JobHandle summaryHandle = new SurfaceBlockHlodSummaryJob\n            {\n                Bricks = _densityBricks,\n                MixedVoxels = PinnedMixedVoxelsOrFallback(),\n                Summaries = _hlodSummaries,\n            }.Schedule(BrickCacheCount, 256);\n            _hlodJobHandle = new SurfaceBlockHlodMeshJob\n            {\n                Summaries = _hlodSummaries,\n                SummaryGridEdge = BrickCacheEdge,\n                PaddingBricks = BrickCachePadding,\n                CoreBrickEdge = BricksPerAxis,\n                CoreOriginVoxel = _build.Coordinate * VoxelsPerAxis,\n                VoxelSize = voxelSize,\n                MaskScratch = _hlodMaskScratch,\n                Vertices = _vertices,\n                Indices = _indices,\n                Overflow = _hlodOverflow,\n            }.Schedule(summaryHandle);\n            _hlodJobScheduled = true;\n        }\n\n        private void ScheduleExactMetadataSnapshot(IRegionReadSource source, int3 cacheOrigin)\n        {\n""",
)

replace_one(
    plan,
    """- [ ] Add a focused step-4 false-empty regression proving an exact owned solid that falls between four-voxel lattice samples cannot be published as an authoritative empty chunk.\n- [ ] Repair the proven step-4 false-empty path with feature-preserving subcell geometry without changing LOD distances, global frame budgets, or fidelity thresholds.\n""",
    """- [x] Add a focused step-4 false-empty regression proving an exact owned solid that falls between four-voxel lattice samples cannot be published as an authoritative empty chunk. PR run 32024887037 (`e53f14ce`) fails only because the production fallback policy is absent, proving the fixture reproduces the intended red contract.\n- [x] Repair the proven step-4 false-empty path with feature-preserving two-voxel subcell geometry only when exact classification owns solid content and ordinary step-4 topology/faceted output is zero; retain the unchanged normal step-4 path, LOD distances, global frame budgets, and fidelity thresholds.\n- [ ] Validate the focused step-4 false-empty regression in EditMode and remeasure production step-4/coarse coverage.\n""",
)

print("step-4 fallback patch applied")
