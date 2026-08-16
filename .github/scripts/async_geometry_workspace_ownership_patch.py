from pathlib import Path


def once(text, old, new, label):
    n = text.count(old)
    if n != 1:
        raise SystemExit(f'{label}: expected one match, found {n}')
    return text.replace(old, new, 1)

root = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction')
workspace_path = root / 'TransvoxelBuildWorkspace.cs'
workspace_path.write_text(r'''using System;
using Unity.Collections;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction
{
    /// <summary>
    /// Reusable native scratch owned by one solid geometry build worker.
    ///
    /// Render residency (chunk identities, slot generations and published arena leases) lives
    /// outside this object. This workspace owns only temporary snapshot/extraction/output memory
    /// reused from build to build, so residency can scale independently from the expensive native
    /// working set. CpuTransvoxelChunkCache may keep borrowed copies of these NativeContainer
    /// handles for compact job setup, but this object is the sole lifecycle/disposal owner.
    /// </summary>
    internal sealed class TransvoxelBuildWorkspace : IDisposable
    {
        internal readonly NativeArray<float> Density;
        internal readonly NativeArray<byte> Materials;
        internal readonly NativeArray<uint> SurfaceSemantics;
        internal readonly NativeArray<byte> BoundarySamples;
        internal readonly NativeArray<TransvoxelDensityBrick> DensityBricks;
        internal readonly NativeArray<byte> MipSampleOccupancy;
        internal readonly NativeArray<byte> MipSampleMaterials;
        internal readonly NativeList<byte> DensityMixedVoxels;
        internal readonly NativeList<ushort> DensityMixedSurfaceSemantics;
        internal readonly NativeList<byte> DensityMixedBoundarySamples;

        internal readonly NativeArray<byte> TopologyCellClass;
        internal readonly NativeArray<byte> TopologyGeometryCounts;
        internal readonly NativeArray<byte> TopologyCellVertexIndices;
        internal readonly NativeArray<ushort> TopologyEdgeCodes;
        internal readonly NativeList<SmoothSurfaceVertex> CompactedTopologyVertices;
        internal readonly NativeList<uint> CompactedTopologyIndices;
        internal readonly NativeArray<int> TopologyOverflowCell;

        internal readonly NativeArray<uint> FacetedMasks;
        internal readonly NativeList<SmoothSurfaceVertex> FacetedVertices;
        internal readonly NativeList<uint> FacetedIndices;

        internal readonly NativeList<SmoothSurfaceVertex> Vertices;
        internal readonly NativeList<uint> Indices;

        internal TransvoxelBuildWorkspace(int gridSampleCount, int brickCacheCount,
                                          bool samplesFromMips, int cellsPerAxis)
        {
            Density = new NativeArray<float>(gridSampleCount, Allocator.Persistent,
                                             NativeArrayOptions.UninitializedMemory);
            Materials = new NativeArray<byte>(gridSampleCount, Allocator.Persistent,
                                              NativeArrayOptions.UninitializedMemory);
            SurfaceSemantics = new NativeArray<uint>(gridSampleCount, Allocator.Persistent,
                                                     NativeArrayOptions.UninitializedMemory);
            BoundarySamples = new NativeArray<byte>(gridSampleCount, Allocator.Persistent,
                                                    NativeArrayOptions.UninitializedMemory);

            if (samplesFromMips)
            {
                MipSampleOccupancy = new NativeArray<byte>(
                    gridSampleCount, Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory);
                MipSampleMaterials = new NativeArray<byte>(
                    gridSampleCount, Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory);
                DensityBricks = default;
            }
            else
            {
                DensityBricks = new NativeArray<TransvoxelDensityBrick>(
                    brickCacheCount, Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory);
                MipSampleOccupancy = default;
                MipSampleMaterials = default;
            }

            DensityMixedVoxels = new NativeList<byte>(64 * 1024, Allocator.Persistent);
            DensityMixedSurfaceSemantics = new NativeList<ushort>(64 * 1024,
                                                                  Allocator.Persistent);
            DensityMixedBoundarySamples = new NativeList<byte>(64 * 1024,
                                                               Allocator.Persistent);

            CompactedTopologyVertices = new NativeList<SmoothSurfaceVertex>(
                16_384, Allocator.Persistent);
            CompactedTopologyIndices = new NativeList<uint>(24_576, Allocator.Persistent);
            TopologyOverflowCell = new NativeArray<int>(1, Allocator.Persistent);
            TopologyCellClass = new NativeArray<byte>(
                TransvoxelRegularTables.CellClass.Length, Allocator.Persistent);
            TopologyGeometryCounts = new NativeArray<byte>(
                TransvoxelRegularTables.CellData.Length, Allocator.Persistent);
            TopologyCellVertexIndices = new NativeArray<byte>(
                TransvoxelRegularTables.CellData.Length * TransvoxelTopologyJob.MaxIndicesPerCell,
                Allocator.Persistent);
            TopologyEdgeCodes = new NativeArray<ushort>(
                TransvoxelRegularTables.VertexData.Length * 12, Allocator.Persistent);

            FacetedMasks = new NativeArray<uint>(
                6 * cellsPerAxis * cellsPerAxis * cellsPerAxis,
                Allocator.Persistent);
            FacetedVertices = new NativeList<SmoothSurfaceVertex>(16_384, Allocator.Persistent);
            FacetedIndices = new NativeList<uint>(24_576, Allocator.Persistent);

            Vertices = new NativeList<SmoothSurfaceVertex>(32_768, Allocator.Persistent);
            Indices = new NativeList<uint>(49_152, Allocator.Persistent);
        }

        public void Dispose()
        {
            if (Density.IsCreated) Density.Dispose();
            if (Materials.IsCreated) Materials.Dispose();
            if (SurfaceSemantics.IsCreated) SurfaceSemantics.Dispose();
            if (BoundarySamples.IsCreated) BoundarySamples.Dispose();
            if (DensityBricks.IsCreated) DensityBricks.Dispose();
            if (MipSampleOccupancy.IsCreated) MipSampleOccupancy.Dispose();
            if (MipSampleMaterials.IsCreated) MipSampleMaterials.Dispose();
            if (DensityMixedVoxels.IsCreated) DensityMixedVoxels.Dispose();
            if (DensityMixedSurfaceSemantics.IsCreated) DensityMixedSurfaceSemantics.Dispose();
            if (DensityMixedBoundarySamples.IsCreated) DensityMixedBoundarySamples.Dispose();
            if (TopologyCellClass.IsCreated) TopologyCellClass.Dispose();
            if (TopologyGeometryCounts.IsCreated) TopologyGeometryCounts.Dispose();
            if (TopologyCellVertexIndices.IsCreated) TopologyCellVertexIndices.Dispose();
            if (TopologyEdgeCodes.IsCreated) TopologyEdgeCodes.Dispose();
            if (CompactedTopologyVertices.IsCreated) CompactedTopologyVertices.Dispose();
            if (CompactedTopologyIndices.IsCreated) CompactedTopologyIndices.Dispose();
            if (TopologyOverflowCell.IsCreated) TopologyOverflowCell.Dispose();
            if (FacetedMasks.IsCreated) FacetedMasks.Dispose();
            if (FacetedVertices.IsCreated) FacetedVertices.Dispose();
            if (FacetedIndices.IsCreated) FacetedIndices.Dispose();
            if (Vertices.IsCreated) Vertices.Dispose();
            if (Indices.IsCreated) Indices.Dispose();
        }
    }
}
''')
(root / 'TransvoxelBuildWorkspace.cs.meta').write_text(
    'fileFormatVersion: 2\nguid: 2b996acd3ad14b2bbf20d69309554b4c\n')

cache_path = root / 'CpuTransvoxelChunkCache.cs'
s = cache_path.read_text()

s = once(s,
'''        private readonly NativeArray<float> _density;
        private readonly NativeArray<byte> _materials;
        private readonly NativeArray<uint> _surfaceSemantics;
        private readonly NativeArray<byte> _boundarySamples;''',
'''        // Heavy persistent native memory is lifecycle-owned by the reusable build workspace.
        // These handles are borrowed aliases kept only to avoid obscuring the job setup below.
        private readonly TransvoxelBuildWorkspace _workspace;
        private readonly NativeArray<float> _density;
        private readonly NativeArray<byte> _materials;
        private readonly NativeArray<uint> _surfaceSemantics;
        private readonly NativeArray<byte> _boundarySamples;''', 'workspace field')

old_alloc = '''            _density = new NativeArray<float>(GridSampleCount, Allocator.Persistent,
                                              NativeArrayOptions.UninitializedMemory);
            _materials = new NativeArray<byte>(GridSampleCount, Allocator.Persistent,
                                               NativeArrayOptions.UninitializedMemory);
            _surfaceSemantics = new NativeArray<uint>(GridSampleCount, Allocator.Persistent,
                                                      NativeArrayOptions.UninitializedMemory);
            _boundarySamples = new NativeArray<byte>(GridSampleCount, Allocator.Persistent,
                                                     NativeArrayOptions.UninitializedMemory);
            if (SamplesFromMips)
            {
                _mipSampleOccupancy = new NativeArray<byte>(
                    GridSampleCount, Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory);
                _mipSampleMaterials = new NativeArray<byte>(
                    GridSampleCount, Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory);
            }
            else
            {
                _densityBricks = new NativeArray<TransvoxelDensityBrick>(
                    BrickCacheCount, Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory);
            }
            _densityMixedVoxels = new NativeList<byte>(64 * 1024, Allocator.Persistent);
            _densityMixedSurfaceSemantics = new NativeList<ushort>(64 * 1024, Allocator.Persistent);
            _densityMixedBoundarySamples = new NativeList<byte>(64 * 1024, Allocator.Persistent);
            _compactedTopologyVertices = new NativeList<SmoothSurfaceVertex>(16_384,
                                                                              Allocator.Persistent);
            _compactedTopologyIndices = new NativeList<uint>(24_576, Allocator.Persistent);
            _topologyOverflowCell = new NativeArray<int>(1, Allocator.Persistent);
            int cellCount = CellsPerAxis * CellsPerAxis * CellsPerAxis;
            _topologyCellClass = new NativeArray<byte>(256, Allocator.Persistent);
            _topologyGeometryCounts = new NativeArray<byte>(
                TransvoxelRegularTables.CellData.Length, Allocator.Persistent);
            _topologyCellVertexIndices = new NativeArray<byte>(
                TransvoxelRegularTables.CellData.Length *
                TransvoxelTopologyJob.MaxIndicesPerCell, Allocator.Persistent);
            _topologyEdgeCodes = new NativeArray<ushort>(256 * 12, Allocator.Persistent);
            _facetedMasks = new NativeArray<uint>(
                6 * CellsPerAxis * CellsPerAxis * CellsPerAxis, Allocator.Persistent);
            _facetedVertices = new NativeList<SmoothSurfaceVertex>(16_384, Allocator.Persistent);
            _facetedIndices = new NativeList<uint>(24_576, Allocator.Persistent);
            _vertices = new NativeList<SmoothSurfaceVertex>(32_768, Allocator.Persistent);
            _indices = new NativeList<uint>(49_152, Allocator.Persistent);'''
new_alloc = '''            _workspace = new TransvoxelBuildWorkspace(
                GridSampleCount, BrickCacheCount, SamplesFromMips, CellsPerAxis);
            _density = _workspace.Density;
            _materials = _workspace.Materials;
            _surfaceSemantics = _workspace.SurfaceSemantics;
            _boundarySamples = _workspace.BoundarySamples;
            _densityBricks = _workspace.DensityBricks;
            _mipSampleOccupancy = _workspace.MipSampleOccupancy;
            _mipSampleMaterials = _workspace.MipSampleMaterials;
            _densityMixedVoxels = _workspace.DensityMixedVoxels;
            _densityMixedSurfaceSemantics = _workspace.DensityMixedSurfaceSemantics;
            _densityMixedBoundarySamples = _workspace.DensityMixedBoundarySamples;
            _compactedTopologyVertices = _workspace.CompactedTopologyVertices;
            _compactedTopologyIndices = _workspace.CompactedTopologyIndices;
            _topologyOverflowCell = _workspace.TopologyOverflowCell;
            _topologyCellClass = _workspace.TopologyCellClass;
            _topologyGeometryCounts = _workspace.TopologyGeometryCounts;
            _topologyCellVertexIndices = _workspace.TopologyCellVertexIndices;
            _topologyEdgeCodes = _workspace.TopologyEdgeCodes;
            _facetedMasks = _workspace.FacetedMasks;
            _facetedVertices = _workspace.FacetedVertices;
            _facetedIndices = _workspace.FacetedIndices;
            _vertices = _workspace.Vertices;
            _indices = _workspace.Indices;'''
s = once(s, old_alloc, new_alloc, 'workspace allocation transfer')

# Cache no longer owns disposal for workspace-backed handles.
for line in [
    '            if (_density.IsCreated) _density.Dispose();\n',
    '            if (_materials.IsCreated) _materials.Dispose();\n',
    '            if (_surfaceSemantics.IsCreated) _surfaceSemantics.Dispose();\n',
    '            if (_boundarySamples.IsCreated) _boundarySamples.Dispose();\n',
    '            if (_densityBricks.IsCreated) _densityBricks.Dispose();\n',
    '            if (_mipSampleOccupancy.IsCreated) _mipSampleOccupancy.Dispose();\n',
    '            if (_mipSampleMaterials.IsCreated) _mipSampleMaterials.Dispose();\n',
    '            if (_densityMixedVoxels.IsCreated) _densityMixedVoxels.Dispose();\n',
    '            if (_densityMixedSurfaceSemantics.IsCreated) _densityMixedSurfaceSemantics.Dispose();\n',
    '            if (_densityMixedBoundarySamples.IsCreated) _densityMixedBoundarySamples.Dispose();\n',
    '            if (_topologyCellClass.IsCreated) _topologyCellClass.Dispose();\n',
    '            if (_topologyGeometryCounts.IsCreated) _topologyGeometryCounts.Dispose();\n',
    '            if (_topologyCellVertexIndices.IsCreated) _topologyCellVertexIndices.Dispose();\n',
    '            if (_topologyEdgeCodes.IsCreated) _topologyEdgeCodes.Dispose();\n',
    '            if (_compactedTopologyVertices.IsCreated) _compactedTopologyVertices.Dispose();\n',
    '            if (_compactedTopologyIndices.IsCreated) _compactedTopologyIndices.Dispose();\n',
    '            if (_topologyOverflowCell.IsCreated) _topologyOverflowCell.Dispose();\n',
    '            if (_facetedMasks.IsCreated) _facetedMasks.Dispose();\n',
    '            if (_facetedVertices.IsCreated) _facetedVertices.Dispose();\n',
    '            if (_facetedIndices.IsCreated) _facetedIndices.Dispose();\n',
    '            if (_vertices.IsCreated) _vertices.Dispose();\n',
    '            if (_indices.IsCreated) _indices.Dispose();\n',
]:
    if s.count(line) != 1:
        raise SystemExit(f'dispose ownership line expected once: {line.strip()} count={s.count(line)}')
    s = s.replace(line, '', 1)

s = once(s,
'''            if (_transitionIndices.IsCreated) _transitionIndices.Dispose();
            _build = default;''',
'''            if (_transitionIndices.IsCreated) _transitionIndices.Dispose();
            _workspace.Dispose();
            _build = default;''', 'workspace disposal')
cache_path.write_text(s)

# Architecture guard.
test_path = Path('Assets/Tests/EditMode/GeometryPipelineArchitectureTests.cs')
t = test_path.read_text()
if 'SolidResidencyAndHeavyBuildScratchHaveSeparateOwners' not in t:
    insert = r'''

        [Test]
        public void SolidResidencyAndHeavyBuildScratchHaveSeparateOwners()
        {
            string cache = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "CpuTransvoxelChunkCache.cs"));
            string workspace = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "TransvoxelBuildWorkspace.cs"));
            StringAssert.Contains("private readonly TransvoxelBuildWorkspace _workspace", cache);
            StringAssert.Contains("new TransvoxelBuildWorkspace(", cache);
            StringAssert.Contains("_workspace.Dispose()", cache);
            StringAssert.DoesNotContain("if (_density.IsCreated) _density.Dispose()", cache);
            StringAssert.DoesNotContain("if (_facetedMasks.IsCreated) _facetedMasks.Dispose()", cache);
            StringAssert.Contains("internal readonly NativeArray<TransvoxelDensityBrick> DensityBricks", workspace);
            StringAssert.Contains("internal readonly NativeArray<uint> FacetedMasks", workspace);
            StringAssert.Contains("internal readonly NativeList<SmoothSurfaceVertex> Vertices", workspace);
            StringAssert.Contains("DensityBricks.Dispose()", workspace);
            StringAssert.Contains("FacetedMasks.Dispose()", workspace);
        }
'''
    marker = '\n    }\n}'
    pos = t.rfind(marker)
    if pos < 0:
        raise SystemExit('architecture test closing marker missing')
    t = t[:pos] + insert + t[pos:]
test_path.write_text(t)

# Progress doc.
doc_path = Path('docs/ASYNC_GEOMETRY_PIPELINE.md')
d = doc_path.read_text()
d = d.replace('- [ ] Split persistent surface chunk/slot state from reusable geometry build workspaces.\n',
              '- [x] Split persistent surface chunk/slot state from reusable geometry build workspaces.\n', 1)
d = d.replace('1. Split persistent chunk slots from reusable build scratch/workspaces.\n2. Split persistent chunk slots from reusable build scratch/workspaces.\n',
              '1. Finish moving transition scratch/tables into the reusable build workspace and deduplicate immutable lookup tables across workers.\n2. Move authoritative snapshot publication toward immutable/COW Storage pages so worker-side snapshotting can become truly off-thread.\n', 1)
# Handle current doc if only one copy of the prior first item remains.
d = d.replace('1. Split persistent chunk slots from reusable build scratch/workspaces.\n',
              '1. Finish moving transition scratch/tables into the reusable build workspace and deduplicate immutable lookup tables across workers.\n', 1)
doc_path.write_text(d)

cache = cache_path.read_text()
assert 'private readonly TransvoxelBuildWorkspace _workspace;' in cache
assert 'new NativeArray<TransvoxelDensityBrick>' not in cache
assert 'if (_density.IsCreated) _density.Dispose()' not in cache
assert '_workspace.Dispose();' in cache
