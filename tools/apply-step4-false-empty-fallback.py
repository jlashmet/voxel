#!/usr/bin/env python3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def replace_once(path: Path, old: str, new: str) -> None:
    text = path.read_text()
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{path}: expected one match, found {count}\n--- old ---\n{old}")
    path.write_text(text.replace(old, new, 1))


cache = ROOT / "Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs"
workspace = ROOT / "Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/TransvoxelBuildWorkspace.cs"
plan = ROOT / ".claude/plans/voxel-showcase-rendering-repair-v2.md"

replace_once(
    cache,
    """            public bool HasOwnedSolid;\n            public bool RequiresContinuousTopology;\n            public double BuildStartSeconds;\n""",
    """            public bool HasOwnedSolid;\n            public bool RequiresContinuousTopology;\n            public bool UsingFeaturePreservingFallback;\n            public double BuildStartSeconds;\n""",
)

replace_once(
    cache,
    """            _workspace = new TransvoxelBuildWorkspace(\n                GridSampleCount, BrickCacheCount, SamplesFromMips, UsesBlockHlod,\n                BricksPerAxis, CellsPerAxis, FaceSamplesPerAxis);\n""",
    """            _workspace = new TransvoxelBuildWorkspace(\n                GridSampleCount, BrickCacheCount, SamplesFromMips, UsesBlockHlod,\n                SourceStep == 4, BricksPerAxis, CellsPerAxis, FaceSamplesPerAxis);\n""",
)

replace_once(
    cache,
    """                    _densityJobScheduled = false;\n                    _topologyJobScheduled = false;\n                    _topologyCompactJobScheduled = false;\n                    _facetedMaskJobScheduled = false;\n                    _facetedMergeJobScheduled = false;\n                    BeginCompletedResultAppend(includeTopology: true);\n                    _build.Phase = 6;\n                    continue;\n""",
    """                    _densityJobScheduled = false;\n                    _topologyJobScheduled = false;\n                    _topologyCompactJobScheduled = false;\n                    _facetedMaskJobScheduled = false;\n                    _facetedMergeJobScheduled = false;\n                    if (TryScheduleFeaturePreservingFallback(\n                            voxelSize,\n                            _compactedTopologyVertices.Length + _facetedVertices.Length,\n                            _compactedTopologyIndices.Length + _facetedIndices.Length))\n                    {\n                        _build.Phase = 7;\n                        continue;\n                    }\n                    BeginCompletedResultAppend(includeTopology: true);\n                    _build.Phase = 6;\n                    continue;\n""",
)

replace_once(
    cache,
    """                    _facetedTurnaroundTiming.Add(ElapsedMs(_build.FacetedScheduledSeconds));\n                    _facetedMaskJobScheduled = false;\n                    _facetedMergeJobScheduled = false;\n                    BeginCompletedResultAppend(includeTopology: false);\n                    _build.Phase = 6;\n                    continue;\n""",
    """                    _facetedTurnaroundTiming.Add(ElapsedMs(_build.FacetedScheduledSeconds));\n                    _facetedMaskJobScheduled = false;\n                    _facetedMergeJobScheduled = false;\n                    if (TryScheduleFeaturePreservingFallback(\n                            voxelSize, _facetedVertices.Length, _facetedIndices.Length))\n                    {\n                        _build.Phase = 7;\n                        continue;\n                    }\n                    BeginCompletedResultAppend(includeTopology: false);\n                    _build.Phase = 6;\n                    continue;\n""",
)

replace_once(
    cache,
    """                    // The step-8 HLOD grid and the step-4 inner ring both resolve geometry on a\n                    // four-voxel lattice. Do not feed faceted HLOD through Transvoxel transition\n                    // cells; finish directly and let the visual LOD regression police the aligned\n                    // boundary. If that test exposes a seam, add a dedicated HLOD boundary pass.\n                    if (UsesBlockHlod)\n                    {\n                        FinishBuild(frame);\n                        if (_pendingUpload) break;\n                        continue;\n                    }\n""",
    """                    // Feature-preserving HLOD output is already an explicit faceted subcell\n                    // representation. Step 8 uses it for the whole ring; step 4 uses it only when\n                    // exact classification proved owned solid but the ordinary four-voxel lattice\n                    // emitted nothing. Do not feed either representation through Transvoxel\n                    // transition cells; the production LOD visual gate polices the boundary.\n                    if (UsesBlockHlod || _build.UsingFeaturePreservingFallback)\n                    {\n                        FinishBuild(frame);\n                        if (_pendingUpload) break;\n                        continue;\n                    }\n""",
)

marker = """        private void ScheduleTopologyJob(float voxelSize, JobHandle dependency = default)\n"""
helper = """        private static bool RequiresFeaturePreservingFallback(\n            int sourceStep, bool hasOwnedSolid, int vertexCount, int indexCount)\n        {\n            return sourceStep == 4 && hasOwnedSolid\n                && vertexCount == 0 && indexCount == 0;\n        }\n\n        private bool TryScheduleFeaturePreservingFallback(\n            float voxelSize, int ordinaryVertexCount, int ordinaryIndexCount)\n        {\n            if (!RequiresFeaturePreservingFallback(\n                    SourceStep, _build.HasOwnedSolid,\n                    ordinaryVertexCount, ordinaryIndexCount))\n                return false;\n\n            if (!_hlodSummaries.IsCreated || !_hlodMaskScratch.IsCreated\n                || !_hlodOverflow.IsCreated)\n                throw new InvalidOperationException(\n                    \"Step-4 feature-preserving fallback scratch was not allocated.\");\n\n            // The exact snapshot still owns its mixed-brick COW pins here. Reuse the same\n            // 2-voxel summary representation as the proven step-8 HLOD path, but only for the\n            // pathological false-empty result. Ordinary step-4 chunks keep the cheaper normal\n            // extraction path.\n            _vertices.Clear();\n            _indices.Clear();\n            _hlodOverflow[0] = 0;\n            JobHandle summaryHandle = new SurfaceBlockHlodSummaryJob\n            {\n                Bricks = _densityBricks,\n                MixedVoxels = PinnedMixedVoxelsOrFallback(),\n                Summaries = _hlodSummaries,\n            }.Schedule(BrickCacheCount, 256);\n            _hlodJobHandle = new SurfaceBlockHlodMeshJob\n            {\n                Summaries = _hlodSummaries,\n                SummaryGridEdge = BrickCacheEdge,\n                PaddingBricks = BrickCachePadding,\n                CoreBrickEdge = BricksPerAxis,\n                CoreOriginVoxel = _build.Coordinate * VoxelsPerAxis,\n                VoxelSize = voxelSize,\n                MaskScratch = _hlodMaskScratch,\n                Vertices = _vertices,\n                Indices = _indices,\n                Overflow = _hlodOverflow,\n            }.Schedule(summaryHandle);\n            _hlodJobScheduled = true;\n            _build.UsingFeaturePreservingFallback = true;\n            _build.RequiresContinuousTopology = false;\n            return true;\n        }\n\n"""
replace_once(cache, marker, helper + marker)

replace_once(
    workspace,
    """        // Step-8 feature-preserving HLOD scratch. These arrays exist only on the outer exact\n        // ring; finer Transvoxel workers pay no memory cost for the coarse representation.\n""",
    """        // Feature-preserving HLOD scratch. Step 8 always uses it; step 4 allocates the\n        // smaller 128^3 fallback workspace so an exact-owned thin feature cannot become an\n        // authoritative empty chunk when the ordinary four-voxel lattice misses it.\n""",
)

replace_once(
    workspace,
    """        internal static int HlodIndexCapacity =>\n            BaselineHlodIndexCapacity * HlodSurfaceCapacityScale;\n""",
    """        internal static int HlodIndexCapacity =>\n            BaselineHlodIndexCapacity * HlodSurfaceCapacityScale;\n        internal static int Step4FallbackVertexCapacity => BaselineHlodVertexCapacity;\n        internal static int Step4FallbackIndexCapacity => BaselineHlodIndexCapacity;\n""",
)

replace_once(
    workspace,
    """        internal TransvoxelBuildWorkspace(int gridSampleCount, int brickCacheCount,\n                                          bool samplesFromMips, bool usesBlockHlod,\n                                          int hlodCoreBrickEdge, int cellsPerAxis,\n                                          int faceSamplesPerAxis)\n        {\n""",
    """        internal TransvoxelBuildWorkspace(int gridSampleCount, int brickCacheCount,\n                                          bool samplesFromMips, bool usesBlockHlod,\n                                          bool supportsFeaturePreservingFallback,\n                                          int hlodCoreBrickEdge, int cellsPerAxis,\n                                          int faceSamplesPerAxis)\n        {\n            bool needsHlodScratch = usesBlockHlod || supportsFeaturePreservingFallback;\n""",
)

replace_once(
    workspace,
    """            if (usesBlockHlod)\n            {\n                HlodSummaries = new NativeArray<SurfaceBlockHlodSummary>(\n                    brickCacheCount, Allocator.Persistent,\n                    NativeArrayOptions.UninitializedMemory);\n""",
    """            if (needsHlodScratch)\n            {\n                HlodSummaries = new NativeArray<SurfaceBlockHlodSummary>(\n                    brickCacheCount, Allocator.Persistent,\n                    NativeArrayOptions.UninitializedMemory);\n""",
)

replace_once(
    workspace,
    """            int finalVertexCapacity = usesBlockHlod ? HlodVertexCapacity : 32_768;\n            int finalIndexCapacity = usesBlockHlod ? HlodIndexCapacity : 49_152;\n""",
    """            int finalVertexCapacity = usesBlockHlod ? HlodVertexCapacity\n                : supportsFeaturePreservingFallback ? Step4FallbackVertexCapacity : 32_768;\n            int finalIndexCapacity = usesBlockHlod ? HlodIndexCapacity\n                : supportsFeaturePreservingFallback ? Step4FallbackIndexCapacity : 49_152;\n""",
)

replace_once(
    plan,
    """- [ ] Add a focused step-4 false-empty regression proving an exact owned solid that falls between four-voxel lattice samples cannot be published as an authoritative empty chunk.\n- [ ] Repair the proven step-4 false-empty path with feature-preserving subcell geometry without changing LOD distances, global frame budgets, or fidelity thresholds.\n""",
    """- [x] Add a focused step-4 false-empty regression proving an exact owned solid that falls between four-voxel lattice samples cannot be published as an authoritative empty chunk. PR run 32024887037 (`e53f14ce`) fails that focused EditMode gate only because production lacks the fallback policy, proving the regression catches the measured defect before PlayMode.\n- [x] Repair the proven step-4 false-empty path with feature-preserving subcell geometry without changing LOD distances, global frame budgets, or fidelity thresholds. Ordinary step-4 extraction remains unchanged; only an exact-owned zero-geometry result schedules the existing 2-voxel summary/greedy mesher while COW snapshot pins are still valid.\n- [ ] Validate the step-4 false-empty fallback regression in EditMode and remeasure production coarse coverage/convergence.\n""",
)

replace_once(
    plan,
    """- `2bca9841` — visible-demand selection made constant-time in the normal case; no-stutter and LOD-fidelity batchmode render targets repaired without changing acceptance thresholds.\n""",
    """- `2bca9841` — visible-demand selection made constant-time in the normal case; no-stutter and LOD-fidelity batchmode render targets repaired without changing acceptance thresholds.\n- `e53f14ce` — focused EditMode regression reproduces a step-4 exact-owned thin feature missed by the four-voxel lattice.\n""",
)

# Retire the transport mechanism in the same commit that applies the real patch.
(ROOT / ".github/workflows/apply-step4-false-empty-fallback.yml").unlink(missing_ok=True)
Path(__file__).unlink(missing_ok=True)
