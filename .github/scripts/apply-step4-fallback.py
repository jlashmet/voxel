from pathlib import Path


def replace_once(path: str, old: str, new: str) -> None:
    p = Path(path)
    text = p.read_text()
    count = text.count(old)
    if count != 1:
        raise RuntimeError(f"{path}: expected one match, found {count}: {old[:120]!r}")
    p.write_text(text.replace(old, new, 1))


cache = "Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs"
workspace = "Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/TransvoxelBuildWorkspace.cs"

replace_once(
    cache,
    """        public bool UsesBlockHlod => SourceStep == VoxelReadGrid.BlockEdge;\n\n        /// <summary>Chunk geometry of the base ring (SourceStep 1). Authoring and capture tools\n""",
    """        public bool UsesBlockHlod => SourceStep == VoxelReadGrid.BlockEdge;\n        private const int FeaturePreservingFallbackStep = VoxelReadGrid.BlockEdge / 2;\n        private bool SupportsFeaturePreservingFallback =>\n            SourceStep == FeaturePreservingFallbackStep;\n\n        /// <summary>Chunk geometry of the base ring (SourceStep 1). Authoring and capture tools\n""",
)

replace_once(
    cache,
    """            public bool HasOwnedSolid;\n            public bool RequiresContinuousTopology;\n            public double BuildStartSeconds;\n""",
    """            public bool HasOwnedSolid;\n            public bool RequiresContinuousTopology;\n            public bool UsedFeaturePreservingFallback;\n            public double BuildStartSeconds;\n""",
)

replace_once(
    cache,
    """            _workspace = new TransvoxelBuildWorkspace(\n                GridSampleCount, BrickCacheCount, SamplesFromMips, UsesBlockHlod,\n                BricksPerAxis, CellsPerAxis, FaceSamplesPerAxis);\n""",
    """            _workspace = new TransvoxelBuildWorkspace(\n                GridSampleCount, BrickCacheCount, SamplesFromMips, UsesBlockHlod,\n                SupportsFeaturePreservingFallback, BricksPerAxis, CellsPerAxis,\n                FaceSamplesPerAxis);\n""",
)

replace_once(
    cache,
    """                    _facetedMaskJobScheduled = false;\n                    _facetedMergeJobScheduled = false;\n                    BeginCompletedResultAppend(includeTopology: true);\n                    _build.Phase = 6;\n""",
    """                    _facetedMaskJobScheduled = false;\n                    _facetedMergeJobScheduled = false;\n                    if (RequiresFeaturePreservingFallback(\n                            SourceStep, _build.HasOwnedSolid,\n                            _compactedTopologyVertices.Length + _facetedVertices.Length,\n                            _compactedTopologyIndices.Length + _facetedIndices.Length))\n                    {\n                        if (_topologyOutput.IsCreated)\n                        {\n                            _topologyOutput.Dispose();\n                            _topologyOutput = default;\n                        }\n                        ScheduleFeaturePreservingHlod(voxelSize);\n                        _build.UsedFeaturePreservingFallback = true;\n                        _build.Phase = 7;\n                        continue;\n                    }\n                    BeginCompletedResultAppend(includeTopology: true);\n                    _build.Phase = 6;\n""",
)

replace_once(
    cache,
    """                    _facetedTurnaroundTiming.Add(ElapsedMs(_build.FacetedScheduledSeconds));\n                    _facetedMaskJobScheduled = false;\n                    _facetedMergeJobScheduled = false;\n                    BeginCompletedResultAppend(includeTopology: false);\n                    _build.Phase = 6;\n""",
    """                    _facetedTurnaroundTiming.Add(ElapsedMs(_build.FacetedScheduledSeconds));\n                    _facetedMaskJobScheduled = false;\n                    _facetedMergeJobScheduled = false;\n                    if (RequiresFeaturePreservingFallback(\n                            SourceStep, _build.HasOwnedSolid,\n                            _facetedVertices.Length, _facetedIndices.Length))\n                    {\n                        ScheduleFeaturePreservingHlod(voxelSize);\n                        _build.UsedFeaturePreservingFallback = true;\n                        _build.Phase = 7;\n                        continue;\n                    }\n                    BeginCompletedResultAppend(includeTopology: false);\n                    _build.Phase = 6;\n""",
)

replace_once(
    cache,
    """                    if (UsesBlockHlod)\n                    {\n                        FinishBuild(frame);\n""",
    """                    if (UsesBlockHlod || _build.UsedFeaturePreservingFallback)\n                    {\n                        FinishBuild(frame);\n""",
)

replace_once(
    cache,
    """        private void ScheduleExactMetadataSnapshot(IRegionReadSource source, int3 cacheOrigin)\n""",
    """        private static bool RequiresFeaturePreservingFallback(\n            int sourceStep, bool hasOwnedSolid, int vertexCount, int indexCount) =>\n            sourceStep == FeaturePreservingFallbackStep\n            && hasOwnedSolid\n            && vertexCount == 0\n            && indexCount == 0;\n\n        private void ScheduleFeaturePreservingHlod(float voxelSize)\n        {\n            if (!_hlodSummaries.IsCreated || !_hlodMaskScratch.IsCreated\n                || !_hlodOverflow.IsCreated)\n                throw new InvalidOperationException(\n                    $"Feature-preserving fallback scratch is missing for step {SourceStep}.");\n\n            _hlodOverflow[0] = 0;\n            _vertices.Clear();\n            _indices.Clear();\n            JobHandle summaryHandle = new SurfaceBlockHlodSummaryJob\n            {\n                Bricks = _densityBricks,\n                MixedVoxels = PinnedMixedVoxelsOrFallback(),\n                Summaries = _hlodSummaries,\n            }.Schedule(BrickCacheCount, 256);\n            _hlodJobHandle = new SurfaceBlockHlodMeshJob\n            {\n                Summaries = _hlodSummaries,\n                SummaryGridEdge = BrickCacheEdge,\n                PaddingBricks = BrickCachePadding,\n                CoreBrickEdge = BricksPerAxis,\n                CoreOriginVoxel = _build.Coordinate * VoxelsPerAxis,\n                VoxelSize = voxelSize,\n                MaskScratch = _hlodMaskScratch,\n                Vertices = _vertices,\n                Indices = _indices,\n                Overflow = _hlodOverflow,\n            }.Schedule(summaryHandle);\n            _hlodJobScheduled = true;\n        }\n\n        private void ScheduleExactMetadataSnapshot(IRegionReadSource source, int3 cacheOrigin)\n""",
)

replace_once(
    workspace,
    """        private const int BaselineHlodSubcellsPerBrickAxis = 2;\n        private const int BaselineHlodVertexCapacity = 262_144;\n        private const int BaselineHlodIndexCapacity = 393_216;\n        internal static int HlodSurfaceCapacityScale\n        {\n            get\n            {\n                int current = SurfaceBlockHlodMeshJob.SubcellsPerBrickAxis;\n                if (current < BaselineHlodSubcellsPerBrickAxis\n                    || current % BaselineHlodSubcellsPerBrickAxis != 0)\n                    throw new InvalidOperationException(\n                        $\"HLOD subcell resolution {current} is incompatible with the \"\n                      + $\"{BaselineHlodSubcellsPerBrickAxis}-subcell capacity baseline.\");\n                int linearScale = current / BaselineHlodSubcellsPerBrickAxis;\n                return linearScale * linearScale;\n            }\n        }\n        internal static int HlodVertexCapacity =>\n            BaselineHlodVertexCapacity * HlodSurfaceCapacityScale;\n        internal static int HlodIndexCapacity =>\n            BaselineHlodIndexCapacity * HlodSurfaceCapacityScale;\n""",
    """        private const int BaselineHlodSubcellsPerBrickAxis = 2;\n        private const int BaselineHlodCoreBrickEdge = 64;\n        private const int BaselineHlodVertexCapacity = 262_144;\n        private const int BaselineHlodIndexCapacity = 393_216;\n\n        internal static int HlodSurfaceCapacityScaleForCoreBrickEdge(int coreBrickEdge)\n        {\n            int currentSubcellEdge = coreBrickEdge * SurfaceBlockHlodMeshJob.SubcellsPerBrickAxis;\n            int baselineSubcellEdge = BaselineHlodCoreBrickEdge\n                                    * BaselineHlodSubcellsPerBrickAxis;\n            if (currentSubcellEdge < baselineSubcellEdge\n                || currentSubcellEdge % baselineSubcellEdge != 0)\n                throw new InvalidOperationException(\n                    $\"HLOD subcell edge {currentSubcellEdge} is incompatible with the \"\n                  + $\"{baselineSubcellEdge}-cell output-capacity baseline.\");\n            int linearScale = currentSubcellEdge / baselineSubcellEdge;\n            return linearScale * linearScale;\n        }\n\n        internal static int HlodSurfaceCapacityScale =>\n            HlodSurfaceCapacityScaleForCoreBrickEdge(BaselineHlodCoreBrickEdge);\n        internal static int HlodVertexCapacityForCoreBrickEdge(int coreBrickEdge) =>\n            BaselineHlodVertexCapacity * HlodSurfaceCapacityScaleForCoreBrickEdge(coreBrickEdge);\n        internal static int HlodIndexCapacityForCoreBrickEdge(int coreBrickEdge) =>\n            BaselineHlodIndexCapacity * HlodSurfaceCapacityScaleForCoreBrickEdge(coreBrickEdge);\n        internal static int HlodVertexCapacity =>\n            HlodVertexCapacityForCoreBrickEdge(BaselineHlodCoreBrickEdge);\n        internal static int HlodIndexCapacity =>\n            HlodIndexCapacityForCoreBrickEdge(BaselineHlodCoreBrickEdge);\n""",
)

replace_once(
    workspace,
    """        internal TransvoxelBuildWorkspace(int gridSampleCount, int brickCacheCount,\n                                          bool samplesFromMips, bool usesBlockHlod,\n                                          int hlodCoreBrickEdge, int cellsPerAxis,\n                                          int faceSamplesPerAxis)\n        {\n""",
    """        internal TransvoxelBuildWorkspace(int gridSampleCount, int brickCacheCount,\n                                          bool samplesFromMips, bool usesBlockHlod,\n                                          bool supportsFeaturePreservingFallback,\n                                          int hlodCoreBrickEdge, int cellsPerAxis,\n                                          int faceSamplesPerAxis)\n        {\n            bool needsHlodScratch = usesBlockHlod || supportsFeaturePreservingFallback;\n""",
)

replace_once(
    workspace,
    """            if (usesBlockHlod)\n            {\n                HlodSummaries = new NativeArray<SurfaceBlockHlodSummary>(\n""",
    """            if (needsHlodScratch)\n            {\n                HlodSummaries = new NativeArray<SurfaceBlockHlodSummary>(\n""",
)

replace_once(
    workspace,
    """            int finalVertexCapacity = usesBlockHlod ? HlodVertexCapacity : 32_768;\n            int finalIndexCapacity = usesBlockHlod ? HlodIndexCapacity : 49_152;\n""",
    """            int finalVertexCapacity = needsHlodScratch\n                ? HlodVertexCapacityForCoreBrickEdge(hlodCoreBrickEdge) : 32_768;\n            int finalIndexCapacity = needsHlodScratch\n                ? HlodIndexCapacityForCoreBrickEdge(hlodCoreBrickEdge) : 49_152;\n""",
)

print("step-4 feature-preserving fallback patch applied")
