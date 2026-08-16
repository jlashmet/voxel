from pathlib import Path


def once(text, old, new, label):
    n = text.count(old)
    if n != 1:
        raise SystemExit(f'{label}: expected one match, found {n}')
    return text.replace(old, new, 1)


arena_path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/SurfaceGeometryArena.cs')
a = arena_path.read_text()
a = once(a,
'''using System.Collections.Generic;
using Unity.Mathematics;''',
'''using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;''', 'arena native collections import')
a = once(a,
'''        public void UploadVertices(List<SmoothSurfaceVertex> source, int sourceStart,
                                   in SurfaceGeometryLease lease, int count)
        {
            Vertices.SetData(source, sourceStart, lease.VertexStart + sourceStart, count);
        }

        public void UploadIndices(List<uint> source, int sourceStart,
                                  in SurfaceGeometryLease lease, int count)
        {
            Indices.SetData(source, sourceStart, lease.IndexStart + sourceStart, count);
        }''',
'''        public void UploadVertices(NativeArray<SmoothSurfaceVertex> source, int sourceStart,
                                   in SurfaceGeometryLease lease, int count)
        {
            Vertices.SetData(source, sourceStart, lease.VertexStart + sourceStart, count);
        }

        public void UploadIndices(NativeArray<uint> source, int sourceStart,
                                  in SurfaceGeometryLease lease, int count)
        {
            Indices.SetData(source, sourceStart, lease.IndexStart + sourceStart, count);
        }''', 'arena native upload sources')
arena_path.write_text(a)


cache_path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs')
s = cache_path.read_text()
s = once(s,
'''            internal bool AdvanceUpload(List<SmoothSurfaceVertex> vertices,
                                        List<uint> indices,
                                        int byteBudget,
                                        out int uploadedBytes)''',
'''            internal bool AdvanceUpload(NativeList<SmoothSurfaceVertex> vertices,
                                        NativeList<uint> indices,
                                        int byteBudget,
                                        out int uploadedBytes)''', 'native upload signature')
s = s.replace('vertices.Count', 'vertices.Length')
s = s.replace('indices.Count', 'indices.Length')
s = once(s,
'''                    _arena.UploadVertices(vertices, _stagingVertexCursor, in _stagingLease, count);''',
'''                    _arena.UploadVertices(vertices.AsArray(), _stagingVertexCursor,
                                          in _stagingLease, count);''', 'native vertex upload')
s = once(s,
'''                    _arena.UploadIndices(indices, _stagingIndexCursor, in _stagingLease, count);''',
'''                    _arena.UploadIndices(indices.AsArray(), _stagingIndexCursor,
                                         in _stagingLease, count);''', 'native index upload')
s = once(s,
'''        private readonly List<SmoothSurfaceVertex> _vertices = new(16_384);
        private readonly List<uint> _indices = new(24_576);''',
'''        // Final build output stays in persistent native memory from Burst completion through
        // bounded arena upload. Streaming must not grow managed geometry Lists on the frame path.
        private NativeList<SmoothSurfaceVertex> _vertices;
        private NativeList<uint> _indices;''', 'native final build buffers')
# These are the worker's final output counts. NativeList uses Length rather than List.Count.
s = s.replace('_vertices.Count', '_vertices.Length')
s = s.replace('_indices.Count', '_indices.Length')
s = once(s,
'''            _facetedVertices = new NativeList<SmoothSurfaceVertex>(16_384, Allocator.Persistent);
            _facetedIndices = new NativeList<uint>(24_576, Allocator.Persistent);
            InitialiseTopologyTables();''',
'''            _facetedVertices = new NativeList<SmoothSurfaceVertex>(16_384, Allocator.Persistent);
            _facetedIndices = new NativeList<uint>(24_576, Allocator.Persistent);
            _vertices = new NativeList<SmoothSurfaceVertex>(32_768, Allocator.Persistent);
            _indices = new NativeList<uint>(49_152, Allocator.Persistent);
            InitialiseTopologyTables();''', 'native final buffer allocation')
s = once(s,
'''            if (_facetedVertices.IsCreated) _facetedVertices.Dispose();
            if (_facetedIndices.IsCreated) _facetedIndices.Dispose();
            _build = default;''',
'''            if (_facetedVertices.IsCreated) _facetedVertices.Dispose();
            if (_facetedIndices.IsCreated) _facetedIndices.Dispose();
            if (_vertices.IsCreated) _vertices.Dispose();
            if (_indices.IsCreated) _indices.Dispose();
            _build = default;''', 'native final buffer disposal')
cache_path.write_text(s)


test_path = Path('Assets/Tests/EditMode/GeometryPipelineArchitectureTests.cs')
t = test_path.read_text()
insert = '''

        [Test]
        public void SolidBuildOutputStaysNativeThroughArenaUpload()
        {
            string cache = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "CpuTransvoxelChunkCache.cs"));
            string arena = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "SurfaceGeometryArena.cs"));
            StringAssert.Contains("private NativeList<SmoothSurfaceVertex> _vertices;", cache);
            StringAssert.Contains("private NativeList<uint> _indices;", cache);
            StringAssert.DoesNotContain("List<SmoothSurfaceVertex> _vertices", cache);
            StringAssert.DoesNotContain("List<uint> _indices", cache);
            StringAssert.Contains("NativeArray<SmoothSurfaceVertex> source", arena);
            StringAssert.Contains("NativeArray<uint> source", arena);
        }
'''
marker = '\n    }\n}'
pos = t.rfind(marker)
if pos < 0:
    raise SystemExit('architecture test closing marker missing')
t = t[:pos] + insert + t[pos:]
test_path.write_text(t)

assert 'private NativeList<SmoothSurfaceVertex> _vertices;' in cache_path.read_text()
assert 'private NativeList<uint> _indices;' in cache_path.read_text()
assert 'List<SmoothSurfaceVertex> _vertices' not in cache_path.read_text()
assert 'List<uint> _indices' not in cache_path.read_text()
assert '_vertices.Count' not in cache_path.read_text()
assert '_indices.Count' not in cache_path.read_text()
assert 'UploadVertices(NativeArray<SmoothSurfaceVertex>' in arena_path.read_text()
