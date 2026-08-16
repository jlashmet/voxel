from pathlib import Path


def once(text, old, new, label):
    n = text.count(old)
    if n != 1:
        raise SystemExit(f'{label}: expected one match, found {n}')
    return text.replace(old, new, 1)

root = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction')

# -----------------------------------------------------------------------------
# One immutable native table bundle shared by every solid worker in a scheduler.
# -----------------------------------------------------------------------------
tables_path = root / 'TransvoxelLookupTables.cs'
tables_path.write_text(r'''using System;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction
{
    /// <summary>
    /// Immutable Burst-friendly Transvoxel lookup data. A scheduler owns exactly one bundle and
    /// all of its build workspaces borrow these arrays read-only. Standalone cache instances used
    /// by focused tests own a private bundle. No worker duplicates these immutable tables.
    /// </summary>
    internal sealed class TransvoxelLookupTables : IDisposable
    {
        internal readonly NativeArray<byte> RegularCellClass;
        internal readonly NativeArray<byte> RegularGeometryCounts;
        internal readonly NativeArray<byte> RegularCellVertexIndices;
        internal readonly NativeArray<ushort> RegularEdgeCodes;

        internal readonly NativeArray<byte> TransitionCellClass;
        internal readonly NativeArray<byte> TransitionGeometryCounts;
        internal readonly NativeArray<byte> TransitionCellIndices;
        internal readonly NativeArray<ushort> TransitionVertexData;
        internal readonly int TransitionVertexStride;
        internal readonly int TransitionIndexStride;

        internal TransvoxelLookupTables()
        {
            RegularCellClass = new NativeArray<byte>(
                TransvoxelRegularTables.CellClass.Length, Allocator.Persistent);
            RegularCellClass.CopyFrom(TransvoxelRegularTables.CellClass);

            RegularGeometryCounts = new NativeArray<byte>(
                TransvoxelRegularTables.CellData.Length, Allocator.Persistent);
            RegularCellVertexIndices = new NativeArray<byte>(
                TransvoxelRegularTables.CellData.Length * TransvoxelTopologyJob.MaxIndicesPerCell,
                Allocator.Persistent);
            for (int cellClass = 0; cellClass < TransvoxelRegularTables.CellData.Length;
                 cellClass++)
            {
                RegularCellData data = TransvoxelRegularTables.CellData[cellClass];
                RegularGeometryCounts[cellClass] = data.GeometryCounts;
                int length = math.min(data.VertexIndices.Length,
                                      TransvoxelTopologyJob.MaxIndicesPerCell);
                for (int i = 0; i < length; i++)
                    RegularCellVertexIndices[
                        cellClass * TransvoxelTopologyJob.MaxIndicesPerCell + i] =
                        data.VertexIndices[i];
            }

            RegularEdgeCodes = new NativeArray<ushort>(
                TransvoxelRegularTables.VertexData.Length * 12, Allocator.Persistent);
            for (int cell = 0; cell < TransvoxelRegularTables.VertexData.Length; cell++)
            {
                ushort[] edges = TransvoxelRegularTables.VertexData[cell];
                int length = math.min(edges.Length, 12);
                for (int i = 0; i < length; i++) RegularEdgeCodes[cell * 12 + i] = edges[i];
            }

            byte[] transitionClasses = TransvoxelTransitionTables.CellClass;
            RegularCellData[] transitionData = TransvoxelTransitionTables.CellData;
            ushort[][] transitionVertices = TransvoxelTransitionTables.VertexData;

            int vertexStride = 0;
            for (int i = 0; i < transitionVertices.Length; i++)
                vertexStride = math.max(vertexStride, transitionVertices[i].Length);
            int indexStride = 0;
            for (int i = 0; i < transitionData.Length; i++)
                indexStride = math.max(indexStride, transitionData[i].VertexIndices.Length);
            TransitionVertexStride = vertexStride;
            TransitionIndexStride = indexStride;

            TransitionCellClass = new NativeArray<byte>(
                transitionClasses.Length, Allocator.Persistent);
            for (int i = 0; i < transitionClasses.Length; i++)
                TransitionCellClass[i] = transitionClasses[i];

            TransitionGeometryCounts = new NativeArray<byte>(
                transitionData.Length, Allocator.Persistent);
            TransitionCellIndices = new NativeArray<byte>(
                transitionData.Length * math.max(1, indexStride), Allocator.Persistent);
            for (int i = 0; i < transitionData.Length; i++)
            {
                TransitionGeometryCounts[i] = transitionData[i].GeometryCounts;
                byte[] indices = transitionData[i].VertexIndices;
                for (int j = 0; j < indices.Length; j++)
                    TransitionCellIndices[i * indexStride + j] = indices[j];
            }

            TransitionVertexData = new NativeArray<ushort>(
                transitionVertices.Length * math.max(1, vertexStride), Allocator.Persistent);
            for (int i = 0; i < transitionVertices.Length; i++)
            {
                ushort[] row = transitionVertices[i];
                for (int j = 0; j < row.Length; j++)
                    TransitionVertexData[i * vertexStride + j] = row[j];
            }
        }

        public void Dispose()
        {
            if (RegularCellClass.IsCreated) RegularCellClass.Dispose();
            if (RegularGeometryCounts.IsCreated) RegularGeometryCounts.Dispose();
            if (RegularCellVertexIndices.IsCreated) RegularCellVertexIndices.Dispose();
            if (RegularEdgeCodes.IsCreated) RegularEdgeCodes.Dispose();
            if (TransitionCellClass.IsCreated) TransitionCellClass.Dispose();
            if (TransitionGeometryCounts.IsCreated) TransitionGeometryCounts.Dispose();
            if (TransitionCellIndices.IsCreated) TransitionCellIndices.Dispose();
            if (TransitionVertexData.IsCreated) TransitionVertexData.Dispose();
        }
    }
}
''')
(root / 'TransvoxelLookupTables.cs.meta').write_text(
    'fileFormatVersion: 2\nguid: b3d12c4b03714fd3bd46d420f8a33a97\n')

# -----------------------------------------------------------------------------
# Workspace owns remaining mutable transition-face scratch, not immutable tables.
# -----------------------------------------------------------------------------
workspace_path = root / 'TransvoxelBuildWorkspace.cs'
w = workspace_path.read_text()
w = once(w,
'''        internal readonly NativeArray<byte> TopologyCellClass;
        internal readonly NativeArray<byte> TopologyGeometryCounts;
        internal readonly NativeArray<byte> TopologyCellVertexIndices;
        internal readonly NativeArray<ushort> TopologyEdgeCodes;
        internal readonly NativeList<SmoothSurfaceVertex> CompactedTopologyVertices;''',
'''        internal readonly NativeList<SmoothSurfaceVertex> CompactedTopologyVertices;''',
'immutable regular tables leave workspace')
w = once(w,
'''        internal readonly NativeArray<uint> FacetedMasks;
        internal readonly NativeList<SmoothSurfaceVertex> FacetedVertices;
        internal readonly NativeList<uint> FacetedIndices;

        internal readonly NativeList<SmoothSurfaceVertex> Vertices;''',
'''        internal readonly NativeArray<uint> FacetedMasks;
        internal readonly NativeList<SmoothSurfaceVertex> FacetedVertices;
        internal readonly NativeList<uint> FacetedIndices;

        internal readonly NativeArray<float> FaceDensity;
        internal readonly NativeArray<byte> FaceMaterials;
        internal readonly NativeArray<uint> FaceSurfaces;
        internal readonly NativeList<SmoothSurfaceVertex> TransitionVertices;
        internal readonly NativeList<uint> TransitionIndices;

        internal readonly NativeList<SmoothSurfaceVertex> Vertices;''',
'mutable transition scratch enters workspace')
w = once(w,
'''        internal TransvoxelBuildWorkspace(int gridSampleCount, int brickCacheCount,
                                          bool samplesFromMips, int cellsPerAxis)''',
'''        internal TransvoxelBuildWorkspace(int gridSampleCount, int brickCacheCount,
                                          bool samplesFromMips, int cellsPerAxis,
                                          int faceSamplesPerAxis)''',
'workspace face sample parameter')
old_regular_alloc = '''            TopologyCellClass = new NativeArray<byte>(
                TransvoxelRegularTables.CellClass.Length, Allocator.Persistent);
            TopologyGeometryCounts = new NativeArray<byte>(
                TransvoxelRegularTables.CellData.Length, Allocator.Persistent);
            TopologyCellVertexIndices = new NativeArray<byte>(
                TransvoxelRegularTables.CellData.Length * TransvoxelTopologyJob.MaxIndicesPerCell,
                Allocator.Persistent);
            TopologyEdgeCodes = new NativeArray<ushort>(
                TransvoxelRegularTables.VertexData.Length * 12, Allocator.Persistent);

'''
w = once(w, old_regular_alloc, '', 'remove immutable regular allocation')
w = once(w,
'''            FacetedVertices = new NativeList<SmoothSurfaceVertex>(16_384, Allocator.Persistent);
            FacetedIndices = new NativeList<uint>(24_576, Allocator.Persistent);

            Vertices = new NativeList<SmoothSurfaceVertex>(32_768, Allocator.Persistent);''',
'''            FacetedVertices = new NativeList<SmoothSurfaceVertex>(16_384, Allocator.Persistent);
            FacetedIndices = new NativeList<uint>(24_576, Allocator.Persistent);

            int faceSamples = faceSamplesPerAxis * faceSamplesPerAxis;
            FaceDensity = new NativeArray<float>(faceSamples, Allocator.Persistent);
            FaceMaterials = new NativeArray<byte>(faceSamples, Allocator.Persistent);
            FaceSurfaces = new NativeArray<uint>(faceSamples, Allocator.Persistent);
            TransitionVertices = new NativeList<SmoothSurfaceVertex>(2048, Allocator.Persistent);
            TransitionIndices = new NativeList<uint>(3072, Allocator.Persistent);

            Vertices = new NativeList<SmoothSurfaceVertex>(32_768, Allocator.Persistent);''',
'allocate transition scratch in workspace')
for line in [
    '            if (TopologyCellClass.IsCreated) TopologyCellClass.Dispose();\n',
    '            if (TopologyGeometryCounts.IsCreated) TopologyGeometryCounts.Dispose();\n',
    '            if (TopologyCellVertexIndices.IsCreated) TopologyCellVertexIndices.Dispose();\n',
    '            if (TopologyEdgeCodes.IsCreated) TopologyEdgeCodes.Dispose();\n',
]:
    if w.count(line) != 1:
        raise SystemExit(f'workspace immutable dispose line missing: {line.strip()}')
    w = w.replace(line, '', 1)
w = once(w,
'''            if (FacetedIndices.IsCreated) FacetedIndices.Dispose();
            if (Vertices.IsCreated) Vertices.Dispose();''',
'''            if (FacetedIndices.IsCreated) FacetedIndices.Dispose();
            if (FaceDensity.IsCreated) FaceDensity.Dispose();
            if (FaceMaterials.IsCreated) FaceMaterials.Dispose();
            if (FaceSurfaces.IsCreated) FaceSurfaces.Dispose();
            if (TransitionVertices.IsCreated) TransitionVertices.Dispose();
            if (TransitionIndices.IsCreated) TransitionIndices.Dispose();
            if (Vertices.IsCreated) Vertices.Dispose();''',
'dispose transition scratch in workspace')
workspace_path.write_text(w)

# -----------------------------------------------------------------------------
# Cache borrows one immutable table bundle; standalone instances own one privately.
# -----------------------------------------------------------------------------
cache_path = root / 'CpuTransvoxelChunkCache.cs'
s = cache_path.read_text()
s = once(s,
'''        private SurfaceGeometryArena _geometryArena;
        private readonly bool _ownsGeometryArena;
        private SurfaceCatalogueView _surfaceCatalogue;''',
'''        private SurfaceGeometryArena _geometryArena;
        private readonly bool _ownsGeometryArena;
        private TransvoxelLookupTables _lookupTables;
        private readonly bool _ownsLookupTables;
        private SurfaceCatalogueView _surfaceCatalogue;''',
'cache lookup ownership fields')

old_ctors = '''        public CpuTransvoxelChunkCache(int sourceStep = 1)
            : this(sourceStep, null, true)
        {
        }

        internal CpuTransvoxelChunkCache(int sourceStep, SurfaceGeometryArena geometryArena)
            : this(sourceStep, geometryArena, false)
        {
        }

        private CpuTransvoxelChunkCache(int sourceStep, SurfaceGeometryArena geometryArena,
                                         bool ownsGeometryArena)
        {
            _geometryArena = geometryArena;
            _ownsGeometryArena = ownsGeometryArena;'''
new_ctors = '''        public CpuTransvoxelChunkCache(int sourceStep = 1)
            : this(sourceStep, null, true, null, true)
        {
        }

        internal CpuTransvoxelChunkCache(int sourceStep, SurfaceGeometryArena geometryArena,
                                         TransvoxelLookupTables lookupTables)
            : this(sourceStep, geometryArena, false, lookupTables, false)
        {
        }

        private CpuTransvoxelChunkCache(int sourceStep, SurfaceGeometryArena geometryArena,
                                         bool ownsGeometryArena,
                                         TransvoxelLookupTables lookupTables,
                                         bool ownsLookupTables)
        {
            _geometryArena = geometryArena;
            _ownsGeometryArena = ownsGeometryArena;
            _lookupTables = lookupTables ?? new TransvoxelLookupTables();
            _ownsLookupTables = ownsLookupTables || lookupTables == null;'''
s = once(s, old_ctors, new_ctors, 'cache shared lookup constructors')

s = once(s,
'''            _workspace = new TransvoxelBuildWorkspace(
                GridSampleCount, BrickCacheCount, SamplesFromMips, CellsPerAxis);''',
'''            _workspace = new TransvoxelBuildWorkspace(
                GridSampleCount, BrickCacheCount, SamplesFromMips,
                CellsPerAxis, FaceSamplesPerAxis);''', 'workspace transition scratch args')

s = once(s,
'''            _topologyCellClass = _workspace.TopologyCellClass;
            _topologyGeometryCounts = _workspace.TopologyGeometryCounts;
            _topologyCellVertexIndices = _workspace.TopologyCellVertexIndices;
            _topologyEdgeCodes = _workspace.TopologyEdgeCodes;''',
'''            _topologyCellClass = _lookupTables.RegularCellClass;
            _topologyGeometryCounts = _lookupTables.RegularGeometryCounts;
            _topologyCellVertexIndices = _lookupTables.RegularCellVertexIndices;
            _topologyEdgeCodes = _lookupTables.RegularEdgeCodes;''', 'cache regular lookup aliases')

s = once(s,
'''            _vertices = _workspace.Vertices;
            _indices = _workspace.Indices;
            InitialiseTopologyTables();
            InitialiseTransitionTables();
        }

        /// <summary>
        /// Flattens the jagged transition tables into Burst-friendly arrays. The jagged form is
        /// how the data is published; jobs need fixed strides.
        /// </summary>
        private void InitialiseTransitionTables()
        {
            byte[] cellClass = TransvoxelTransitionTables.CellClass;
            RegularCellData[] cellData = TransvoxelTransitionTables.CellData;
            ushort[][] vertexData = TransvoxelTransitionTables.VertexData;

            _transitionVertexStride = 0;
            for (int i = 0; i < vertexData.Length; i++)
                _transitionVertexStride = math.max(_transitionVertexStride, vertexData[i].Length);
            _transitionIndexStride = 0;
            for (int i = 0; i < cellData.Length; i++)
                _transitionIndexStride = math.max(_transitionIndexStride,
                                                  cellData[i].VertexIndices.Length);

            _transitionCellClass = new NativeArray<byte>(cellClass.Length, Allocator.Persistent);
            for (int i = 0; i < cellClass.Length; i++) _transitionCellClass[i] = cellClass[i];

            _transitionGeometryCounts = new NativeArray<byte>(cellData.Length,
                                                              Allocator.Persistent);
            _transitionCellIndices = new NativeArray<byte>(
                cellData.Length * math.max(1, _transitionIndexStride), Allocator.Persistent);
            for (int i = 0; i < cellData.Length; i++)
            {
                _transitionGeometryCounts[i] = cellData[i].GeometryCounts;
                byte[] indices = cellData[i].VertexIndices;
                for (int j = 0; j < indices.Length; j++)
                    _transitionCellIndices[i * _transitionIndexStride + j] = indices[j];
            }

            _transitionVertexData = new NativeArray<ushort>(
                vertexData.Length * math.max(1, _transitionVertexStride), Allocator.Persistent);
            for (int i = 0; i < vertexData.Length; i++)
            {
                ushort[] row = vertexData[i];
                for (int j = 0; j < row.Length; j++)
                    _transitionVertexData[i * _transitionVertexStride + j] = row[j];
            }

            int faceSamples = FaceSamplesPerAxis * FaceSamplesPerAxis;
            _faceDensity = new NativeArray<float>(faceSamples, Allocator.Persistent);
            _faceMaterials = new NativeArray<byte>(faceSamples, Allocator.Persistent);
            _faceSurfaces = new NativeArray<uint>(faceSamples, Allocator.Persistent);
            _transitionVertices = new NativeList<SmoothSurfaceVertex>(2048, Allocator.Persistent);
            _transitionIndices = new NativeList<uint>(3072, Allocator.Persistent);
        }
''',
'''            _vertices = _workspace.Vertices;
            _indices = _workspace.Indices;
            _faceDensity = _workspace.FaceDensity;
            _faceMaterials = _workspace.FaceMaterials;
            _faceSurfaces = _workspace.FaceSurfaces;
            _transitionVertices = _workspace.TransitionVertices;
            _transitionIndices = _workspace.TransitionIndices;
            _transitionCellClass = _lookupTables.TransitionCellClass;
            _transitionGeometryCounts = _lookupTables.TransitionGeometryCounts;
            _transitionCellIndices = _lookupTables.TransitionCellIndices;
            _transitionVertexData = _lookupTables.TransitionVertexData;
            _transitionVertexStride = _lookupTables.TransitionVertexStride;
            _transitionIndexStride = _lookupTables.TransitionIndexStride;
        }
''', 'remove per-cache transition table initialization')

# Remove regular table initialization method entirely.
start = s.index('        private void InitialiseTopologyTables()')
end = s.index('        public int MaxResidentChunks', start)
s = s[:start] + s[end:]

# Transition scratch is now workspace-owned; immutable transition tables are lookup-owned.
for line in [
    '            if (_faceDensity.IsCreated) _faceDensity.Dispose();\n',
    '            if (_faceMaterials.IsCreated) _faceMaterials.Dispose();\n',
    '            if (_faceSurfaces.IsCreated) _faceSurfaces.Dispose();\n',
    '            if (_transitionCellClass.IsCreated) _transitionCellClass.Dispose();\n',
    '            if (_transitionGeometryCounts.IsCreated) _transitionGeometryCounts.Dispose();\n',
    '            if (_transitionCellIndices.IsCreated) _transitionCellIndices.Dispose();\n',
    '            if (_transitionVertexData.IsCreated) _transitionVertexData.Dispose();\n',
    '            if (_transitionVertices.IsCreated) _transitionVertices.Dispose();\n',
    '            if (_transitionIndices.IsCreated) _transitionIndices.Dispose();\n',
]:
    if s.count(line) != 1:
        raise SystemExit(f'cache transition dispose line missing: {line.strip()}')
    s = s.replace(line, '', 1)

s = once(s,
'''            _workspace.Dispose();
            _build = default;
            if (_ownsGeometryArena)''',
'''            _workspace.Dispose();
            if (_ownsLookupTables)
            {
                _lookupTables?.Dispose();
                _lookupTables = null;
            }
            _build = default;
            if (_ownsGeometryArena)''', 'cache lookup disposal')
cache_path.write_text(s)

# -----------------------------------------------------------------------------
# Scheduler owns exactly one lookup bundle and passes it to every ring/worker.
# -----------------------------------------------------------------------------
scheduler_path = root / 'VoxelSurfaceScheduler.cs'
q = scheduler_path.read_text()
q = once(q,
'''            public SurfaceRing(int sourceStep, float innerRadiusMetres, float outerRadiusMetres,
                               int maxResidentChunks, SurfaceGeometryArena geometryArena)''',
'''            public SurfaceRing(int sourceStep, float innerRadiusMetres, float outerRadiusMetres,
                               int maxResidentChunks, SurfaceGeometryArena geometryArena,
                               TransvoxelLookupTables lookupTables)''', 'ring lookup parameter')
q = once(q,
'''                    Workers[i] = new CpuTransvoxelChunkCache(sourceStep, geometryArena)''',
'''                    Workers[i] = new CpuTransvoxelChunkCache(
                        sourceStep, geometryArena, lookupTables)''', 'ring shared lookup use')
q = once(q,
'''        private readonly SurfaceGeometryArena _geometryArena;
        private readonly SurfaceRing[] _rings;''',
'''        private readonly SurfaceGeometryArena _geometryArena;
        private readonly TransvoxelLookupTables _lookupTables;
        private readonly SurfaceRing[] _rings;''', 'scheduler lookup owner field')
q = once(q,
'''            _geometryArena = new SurfaceGeometryArena(SurfaceArenaVertexCapacity,
                                                       SurfaceArenaIndexCapacity,
                                                       SurfaceArenaDrawCapacity);
            _rings = new SurfaceRing[s_RingLayout.Length];''',
'''            _geometryArena = new SurfaceGeometryArena(SurfaceArenaVertexCapacity,
                                                       SurfaceArenaIndexCapacity,
                                                       SurfaceArenaDrawCapacity);
            _lookupTables = new TransvoxelLookupTables();
            _rings = new SurfaceRing[s_RingLayout.Length];''', 'scheduler lookup allocation')
q = once(q,
'''                SurfaceRing ring = new(layout.SourceStep, layout.Inner, layout.Outer, 4096, _geometryArena);''',
'''                SurfaceRing ring = new(layout.SourceStep, layout.Inner, layout.Outer,
                                           4096, _geometryArena, _lookupTables);''', 'scheduler ring lookup pass')
q = once(q,
'''            _water.Dispose();
            for (int r = 0; r < _rings.Length; r++) _rings[r].Dispose();
            _geometryArena.Dispose();''',
'''            _water.Dispose();
            for (int r = 0; r < _rings.Length; r++) _rings[r].Dispose();
            _lookupTables.Dispose();
            _geometryArena.Dispose();''', 'scheduler lookup disposal')
scheduler_path.write_text(q)

# -----------------------------------------------------------------------------
# Architecture guards + doc.
# -----------------------------------------------------------------------------
test_path = Path('Assets/Tests/EditMode/GeometryPipelineArchitectureTests.cs')
t = test_path.read_text()
if 'ImmutableTransvoxelTablesAreSharedAcrossSolidWorkers' not in t:
    insert = r'''

        [Test]
        public void ImmutableTransvoxelTablesAreSharedAcrossSolidWorkers()
        {
            string cache = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "CpuTransvoxelChunkCache.cs"));
            string scheduler = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "VoxelSurfaceScheduler.cs"));
            string workspace = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "TransvoxelBuildWorkspace.cs"));
            string tables = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "TransvoxelLookupTables.cs"));
            StringAssert.Contains("private readonly TransvoxelLookupTables _lookupTables", scheduler);
            StringAssert.Contains("geometryArena, lookupTables", scheduler);
            StringAssert.Contains("_lookupTables.RegularCellClass", cache);
            StringAssert.Contains("_lookupTables.TransitionCellClass", cache);
            StringAssert.DoesNotContain("InitialiseTopologyTables", cache);
            StringAssert.DoesNotContain("InitialiseTransitionTables", cache);
            StringAssert.DoesNotContain("TopologyCellClass", workspace);
            StringAssert.Contains("FaceDensity", workspace);
            StringAssert.Contains("[ReadOnly] public NativeArray<byte> TransitionCellClass", ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "Transvoxel", "TransitionMeshJob.cs")));
            StringAssert.Contains("internal sealed class TransvoxelLookupTables", tables);
        }
'''
    marker = '\n    }\n}'
    pos = t.rfind(marker)
    if pos < 0:
        raise SystemExit('architecture test closing marker missing')
    t = t[:pos] + insert + t[pos:]
test_path.write_text(t)

doc_path = Path('docs/ASYNC_GEOMETRY_PIPELINE.md')
d = doc_path.read_text()
d = d.replace('- [x] Split persistent surface chunk/slot state from reusable geometry build workspaces.\n',
'''- [x] Split persistent surface chunk/slot state from reusable geometry build workspaces.
- [x] Share immutable regular/transition Transvoxel lookup tables across all solid workers; keep writable face scratch per workspace.
''', 1)
d = d.replace('1. Finish moving transition scratch/tables into the reusable build workspace and deduplicate immutable lookup tables across workers.\n',
              '1. Move authoritative snapshot publication toward immutable/COW Storage pages so worker-side snapshotting can become truly off-thread.\n', 1)
doc_path.write_text(d)

cache = cache_path.read_text()
assert 'InitialiseTopologyTables' not in cache
assert 'InitialiseTransitionTables' not in cache
assert '_lookupTables.RegularCellClass' in cache
assert '_workspace.FaceDensity' in cache
assert 'TopologyCellClass' not in workspace_path.read_text()
assert 'private readonly TransvoxelLookupTables _lookupTables;' in scheduler_path.read_text()
