from pathlib import Path


def replace_once(path: str, old: str, new: str) -> None:
    p = Path(path)
    text = p.read_text()
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{path}: expected one match, found {count}: {old[:120]!r}")
    p.write_text(text.replace(old, new, 1))


workspace = "Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/TransvoxelBuildWorkspace.cs"
cache = "Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs"
plan = ".claude/plans/voxel-showcase-rendering-repair-v2.md"

replace_once(
    workspace,
    """        internal TransvoxelBuildWorkspace(int gridSampleCount, int brickCacheCount,\n                                          bool samplesFromMips, bool usesBlockHlod,\n                                          int hlodCoreBrickEdge, int cellsPerAxis,\n""",
    """        internal TransvoxelBuildWorkspace(int gridSampleCount, int brickCacheCount,\n                                          bool samplesFromMips, bool usesBlockHlod,\n                                          bool supportsFeaturePreservingFallback,\n                                          int hlodCoreBrickEdge, int cellsPerAxis,\n""",
)
replace_once(
    workspace,
    """            if (usesBlockHlod)\n            {\n                HlodSummaries = new NativeArray<SurfaceBlockHlodSummary>(\n""",
    """            if (usesBlockHlod || supportsFeaturePreservingFallback)\n            {\n                HlodSummaries = new NativeArray<SurfaceBlockHlodSummary>(\n""",
)
replace_once(
    workspace,
    """            int finalVertexCapacity = usesBlockHlod ? HlodVertexCapacity : 32_768;\n            int finalIndexCapacity = usesBlockHlod ? HlodIndexCapacity : 49_152;\n""",
    """            // A step-4 false-empty fallback resolves a 128^3 two-voxel subcell grid,\n            // exactly the resolution the original baseline HLOD capacity was sized for. Reserve\n            // that fixed output only on step-4 workers; normal finer workers keep the compact\n            // 32k/49k lists and step 8 keeps its 4x feature-preserving capacity.\n            int finalVertexCapacity = usesBlockHlod ? HlodVertexCapacity\n                : supportsFeaturePreservingFallback ? BaselineHlodVertexCapacity : 32_768;\n            int finalIndexCapacity = usesBlockHlod ? HlodIndexCapacity\n                : supportsFeaturePreservingFallback ? BaselineHlodIndexCapacity : 49_152;\n""",
)

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
    """                    _densityJobScheduled = false;\n                    _topologyJobScheduled = false;\n                    _topologyCompactJobScheduled = false;\n                    _facetedMaskJobScheduled = false;\n                    _facetedMergeJobScheduled = false;\n                    BeginCompletedResultAppend(includeTopology: true);\n                    _build.Phase = 6;\n                    continue;\n""",
    """                    _densityJobScheduled = false;\n                    _topologyJobScheduled = false;\n                    _topologyCompactJobScheduled = false;\n                    _facetedMaskJobScheduled = false;\n                    _facetedMergeJobScheduled = false;\n                    if (RequiresFeaturePreservingFallback(\n                            SourceStep, _build.HasOwnedSolid,\n                            _compactedTopologyVertices.Length + _facetedVertices.Length,\n                            _compactedTopologyIndices.Length + _facetedIndices.Length))\n                    {\n                        ScheduleFeaturePreservingHlod(voxelSize);\n                        _build.UsedFeaturePreservingFallback = true;\n                        _build.Phase = 7;\n                        continue;\n                    }\n                    BeginCompletedResultAppend(includeTopology: true);\n                    _build.Phase = 6;\n                    continue;\n""",
)
replace_once(
    cache,
    """                    _facetedTurnaroundTiming.Add(ElapsedMs(_build.FacetedScheduledSeconds));\n                    _facetedMaskJobScheduled = false;\n                    _facetedMergeJobScheduled = false;\n                    BeginCompletedResultAppend(includeTopology: false);\n                    _build.Phase = 6;\n                    continue;\n""",
    """                    _facetedTurnaroundTiming.Add(ElapsedMs(_build.FacetedScheduledSeconds));\n                    _facetedMaskJobScheduled = false;\n                    _facetedMergeJobScheduled = false;\n                    if (RequiresFeaturePreservingFallback(\n                            SourceStep, _build.HasOwnedSolid,\n                            _facetedVertices.Length, _facetedIndices.Length))\n                    {\n                        ScheduleFeaturePreservingHlod(voxelSize);\n                        _build.UsedFeaturePreservingFallback = true;\n                        _build.Phase = 7;\n                        continue;\n                    }\n                    BeginCompletedResultAppend(includeTopology: false);\n                    _build.Phase = 6;\n                    continue;\n""",
)
replace_once(
    cache,
    """                    if (UsesBlockHlod)\n                    {\n                        FinishBuild(frame);\n""",
    """                    if (UsesBlockHlod || _build.UsedFeaturePreservingFallback)\n                    {\n                        FinishBuild(frame);\n""",
)
replace_once(
    cache,
    """            if (UsesBlockHlod)\n            {\n                _hlodOverflow[0] = 0;\n                JobHandle summaryHandle = new SurfaceBlockHlodSummaryJob\n                {\n                    Bricks = _densityBricks,\n                    MixedVoxels = PinnedMixedVoxelsOrFallback(),\n                    Summaries = _hlodSummaries,\n                }.Schedule(BrickCacheCount, 256);\n                _hlodJobHandle = new SurfaceBlockHlodMeshJob\n                {\n                    Summaries = _hlodSummaries,\n                    SummaryGridEdge = BrickCacheEdge,\n                    PaddingBricks = BrickCachePadding,\n                    CoreBrickEdge = BricksPerAxis,\n                    CoreOriginVoxel = chunkOriginVoxel,\n                    VoxelSize = voxelSize,\n                    MaskScratch = _hlodMaskScratch,\n                    Vertices = _vertices,\n                    Indices = _indices,\n                    Overflow = _hlodOverflow,\n                }.Schedule(summaryHandle);\n                _hlodJobScheduled = true;\n                _build.HasOwnedSolid = true; // resolved from final HLOD output on completion\n""",
    """            if (UsesBlockHlod)\n            {\n                ScheduleFeaturePreservingHlod(voxelSize);\n                _build.HasOwnedSolid = true; // resolved from final HLOD output on completion\n""",
)
replace_once(
    cache,
    """        private void ScheduleTopologyJob(float voxelSize, JobHandle dependency = default)\n        {\n""",
    """        private static bool RequiresFeaturePreservingFallback(\n            int sourceStep, bool hasOwnedSolid, int vertexCount, int indexCount) =>\n            sourceStep == FeaturePreservingFallbackStep\n            && hasOwnedSolid\n            && vertexCount == 0\n            && indexCount == 0;\n\n        private void ScheduleFeaturePreservingHlod(float voxelSize)\n        {\n            if (!_hlodSummaries.IsCreated || !_hlodMaskScratch.IsCreated || !_hlodOverflow.IsCreated)\n                throw new InvalidOperationException(\n                    $\"Feature-preserving scratch was not allocated for source step {SourceStep}.\");\n\n            _vertices.Clear();\n            _indices.Clear();\n            _hlodOverflow[0] = 0;\n            JobHandle summaryHandle = new SurfaceBlockHlodSummaryJob\n            {\n                Bricks = _densityBricks,\n                MixedVoxels = PinnedMixedVoxelsOrFallback(),\n                Summaries = _hlodSummaries,\n            }.Schedule(BrickCacheCount, 256);\n            _hlodJobHandle = new SurfaceBlockHlodMeshJob\n            {\n                Summaries = _hlodSummaries,\n                SummaryGridEdge = BrickCacheEdge,\n                PaddingBricks = BrickCachePadding,\n                CoreBrickEdge = BricksPerAxis,\n                CoreOriginVoxel = _build.Coordinate * VoxelsPerAxis,\n                VoxelSize = voxelSize,\n                MaskScratch = _hlodMaskScratch,\n                Vertices = _vertices,\n                Indices = _indices,\n                Overflow = _hlodOverflow,\n            }.Schedule(summaryHandle);\n            _hlodJobScheduled = true;\n        }\n\n        private void ScheduleTopologyJob(float voxelSize, JobHandle dependency = default)\n        {\n""",
)

replace_once(
    plan,
    "- [ ] Add a focused step-4 false-empty regression proving an exact owned solid that falls between four-voxel lattice samples cannot be published as an authoritative empty chunk.",
    "- [x] Add a focused step-4 false-empty regression proving an exact owned solid that falls between four-voxel lattice samples cannot be published as an authoritative empty chunk. PR run 32024887037 (`e53f14ce`) executes the focused EditMode gate and fails exactly because production has no `RequiresFeaturePreservingFallback` guard, proving the regression targets the measured false-empty path.",
)
replace_once(
    plan,
    "- [ ] Repair the proven step-4 false-empty path with feature-preserving subcell geometry without changing LOD distances, global frame budgets, or fidelity thresholds.",
    "- [x] Implement the proven step-4 false-empty repair: when exact classification owns solid content but ordinary step-4 topology/faceted output is empty, reuse the existing exact 2-voxel subcell summary/greedy HLOD path before publication. Normal step-4 geometry, LOD distances, the 0.50 ms global build budget and fidelity thresholds remain unchanged.\n- [ ] Validate the step-4 false-empty fallback in EditMode and remeasure production step-4/coarse visible coverage.",
)

Path('.github/workflows/apply-step4-fallback.yml').unlink(missing_ok=True)
Path('.github/scripts/apply-step4-fallback.py').unlink(missing_ok=True)
