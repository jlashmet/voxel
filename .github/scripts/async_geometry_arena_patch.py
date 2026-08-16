from pathlib import Path


def once(text, old, new, label):
    n = text.count(old)
    if n != 1:
        raise SystemExit(f'{label}: expected one match, found {n}')
    return text.replace(old, new, 1)


# Shared-buffer shader addressing.
shader_path = Path('Assets/VoxelEngine/Rendering/Runtime/Shaders/SmoothSurface.shader')
shader = shader_path.read_text()
shader = once(shader,
    '            uint _SurfaceIndexBase;\n',
    '            uint _SurfaceIndexBase;\n            uint _SurfaceVertexBase;\n',
    'shader vertex base declaration')
shader = once(shader,
    '                SurfaceVertex vertex = _SurfaceVertices[_SurfaceIndices[_SurfaceIndexBase + vertexID]];',
    '                SurfaceVertex vertex = _SurfaceVertices[_SurfaceVertexBase + _SurfaceIndices[_SurfaceIndexBase + vertexID]];',
    'shader vertex base use')
shader_path.write_text(shader)


cache_path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs')
s = cache_path.read_text()
s = once(s,
    '        private static readonly int s_SurfaceIndexBase = Shader.PropertyToID("_SurfaceIndexBase");\n',
    '        private static readonly int s_SurfaceIndexBase = Shader.PropertyToID("_SurfaceIndexBase");\n        private static readonly int s_SurfaceVertexBase = Shader.PropertyToID("_SurfaceVertexBase");\n',
    'shader id')

# Replace Entry GPU ownership with arena leases while preserving bounds/draw code below.
entry_start = s.index('        public sealed class Entry : IDisposable')
bounds_start = s.index('            public Bounds WorldBounds(float voxelSize)', entry_start)
entry_prefix = '''        public sealed class Entry : IDisposable
        {
            public readonly int3 Coordinate;
            /// <summary>Voxels this chunk spans per axis — ring-dependent, so bounds and
            /// any consumer's world-space reasoning must use it rather than a constant.</summary>
            public readonly int VoxelsPerAxis;
            /// <summary>Voxels between adjacent samples in the ring that produced this entry.</summary>
            public readonly int SourceStep;
            private readonly SurfaceGeometryArena _arena;
            private SurfaceGeometryLease _liveLease;
            private SurfaceGeometryLease _stagingLease;
            public ComputeBuffer Vertices => _arena.Vertices;
            public ComputeBuffer Indices => _arena.Indices;
            public ComputeBuffer Args => _arena.Args;
            public bool Ready;
            public int IndexCount;
            public int LastUsedFrame;
            public long GpuBytes { get; private set; }
            public int VertexCapacity { get; private set; }
            public int IndexCapacity { get; private set; }
            public ulong SourceVersion { get; internal set; }
            public uint MaterialPaletteVersion { get; internal set; }
            public uint SurfaceCatalogueVersion { get; internal set; }
            public ulong SurfaceCatalogueHash { get; internal set; }
            public uint CoatingCatalogueVersion { get; internal set; }
            public ulong CoatingCatalogueHash { get; internal set; }

            internal Entry(int3 coordinate, int voxelsPerAxis, int sourceStep,
                           SurfaceGeometryArena arena)
            {
                Coordinate = coordinate;
                VoxelsPerAxis = voxelsPerAxis;
                SourceStep = sourceStep;
                _arena = arena ?? throw new ArgumentNullException(nameof(arena));
            }

            private int _stagingVertexCursor;
            private int _stagingIndexCursor;
            private readonly uint[] _indirectArgs = new uint[4];

            internal int RemainingUploadBytes(int vertexCount, int indexCount)
            {
                int verticesRemaining = math.max(0, vertexCount - _stagingVertexCursor);
                int indicesRemaining = math.max(0, indexCount - _stagingIndexCursor);
                return verticesRemaining * SmoothSurfaceVertex.Stride
                     + indicesRemaining * sizeof(uint)
                     + SurfaceGeometryArena.ArgsWordsPerDraw * sizeof(uint);
            }

            internal bool AdvanceUpload(List<SmoothSurfaceVertex> vertices,
                                        List<uint> indices,
                                        int byteBudget,
                                        out int uploadedBytes)
            {
                uploadedBytes = 0;
                if (byteBudget <= 0 || !EnsureUploadStaging(vertices.Count, indices.Count))
                    return false;

                int remainingBudget = byteBudget;
                int vertexRemaining = vertices.Count - _stagingVertexCursor;
                if (vertexRemaining > 0 && remainingBudget >= SmoothSurfaceVertex.Stride)
                {
                    int count = math.min(vertexRemaining,
                        remainingBudget / SmoothSurfaceVertex.Stride);
                    _arena.UploadVertices(vertices, _stagingVertexCursor, in _stagingLease, count);
                    int bytes = count * SmoothSurfaceVertex.Stride;
                    _stagingVertexCursor += count;
                    remainingBudget -= bytes;
                    uploadedBytes += bytes;
                }

                int indexRemaining = indices.Count - _stagingIndexCursor;
                if (_stagingVertexCursor == vertices.Count && indexRemaining > 0
                    && remainingBudget >= sizeof(uint))
                {
                    int count = math.min(indexRemaining, remainingBudget / sizeof(uint));
                    _arena.UploadIndices(indices, _stagingIndexCursor, in _stagingLease, count);
                    int bytes = count * sizeof(uint);
                    _stagingIndexCursor += count;
                    remainingBudget -= bytes;
                    uploadedBytes += bytes;
                }

                const int argsBytes = SurfaceGeometryArena.ArgsWordsPerDraw * sizeof(uint);
                if (_stagingVertexCursor != vertices.Count
                    || _stagingIndexCursor != indices.Count
                    || remainingBudget < argsBytes)
                    return false;

                _indirectArgs[0] = (uint)indices.Count;
                _indirectArgs[1] = 1u;
                _indirectArgs[2] = 0u;
                _indirectArgs[3] = 0u;
                _arena.UploadArgs(_indirectArgs, in _stagingLease);
                uploadedBytes += argsBytes;

                SurfaceGeometryLease previous = _liveLease;
                _liveLease = _stagingLease;
                _stagingLease = default;
                _stagingVertexCursor = 0;
                _stagingIndexCursor = 0;
                IndexCount = indices.Count;
                VertexCapacity = _liveLease.VertexCapacity;
                IndexCapacity = _liveLease.IndexCapacity;
                GpuBytes = _arena.ReservedBytes(in _liveLease);
                Ready = true;
                _arena.Release(in previous);
                return true;
            }

            private bool EnsureUploadStaging(int vertexCount, int indexCount)
            {
                if (_stagingLease.IsValid) return true;
                if (!_arena.TryAcquire(vertexCount, indexCount, out _stagingLease)) return false;
                _stagingVertexCursor = 0;
                _stagingIndexCursor = 0;
                return true;
            }

            internal void CancelUpload()
            {
                _arena.Release(in _stagingLease);
                _stagingLease = default;
                _stagingVertexCursor = 0;
                _stagingIndexCursor = 0;
            }

'''
s = s[:entry_start] + entry_prefix + s[bounds_start:]

s = once(s,
    '''                properties.SetBuffer(s_SurfaceVertices, Vertices);
                properties.SetBuffer(s_SurfaceIndices, Indices);
                properties.SetInt(s_SurfaceIndexBase, 0);
                commandBuffer.DrawProceduralIndirect(Matrix4x4.identity, material, 0,
                    MeshTopology.Triangles, Args, 0, properties);''',
    '''                properties.SetBuffer(s_SurfaceVertices, _arena.Vertices);
                properties.SetBuffer(s_SurfaceIndices, _arena.Indices);
                properties.SetInt(s_SurfaceVertexBase, _liveLease.VertexStart);
                properties.SetInt(s_SurfaceIndexBase, _liveLease.IndexStart);
                commandBuffer.DrawProceduralIndirect(Matrix4x4.identity, material, 0,
                    MeshTopology.Triangles, _arena.Args,
                    _liveLease.ArgsWordStart * sizeof(uint), properties);''',
    'arena draw binding')

old_entry_dispose = '''            public void Dispose()
            {
                CancelUpload();
                Vertices?.Release();
                Indices?.Release();
                Args?.Release();
                Vertices = null;
                Indices = null;
                Args = null;
                Ready = false;
                IndexCount = 0;
                GpuBytes = 0;
                VertexCapacity = 0;
                IndexCapacity = 0;
            }'''
new_entry_dispose = '''            public void Dispose()
            {
                CancelUpload();
                _arena.Release(in _liveLease);
                _liveLease = default;
                Ready = false;
                IndexCount = 0;
                GpuBytes = 0;
                VertexCapacity = 0;
                IndexCapacity = 0;
            }'''
s = once(s, old_entry_dispose, new_entry_dispose, 'entry dispose')

s = once(s,
    '        private BuildState _build;\n        private bool _pendingUpload;\n',
    '        private BuildState _build;\n        private bool _pendingUpload;\n        private SurfaceGeometryArena _geometryArena;\n        private readonly bool _ownsGeometryArena;\n',
    'cache arena fields')

s = once(s,
    '        public CpuTransvoxelChunkCache(int sourceStep = 1)\n        {\n',
    '''        public CpuTransvoxelChunkCache(int sourceStep = 1)
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
            _ownsGeometryArena = ownsGeometryArena;
''',
    'cache constructors')

s = once(s,
    '                entry = new Entry(_build.Coordinate, VoxelsPerAxis, SourceStep);',
    '                entry = new Entry(_build.Coordinate, VoxelsPerAxis, SourceStep, GetGeometryArena());',
    'entry arena construction')

get_arena = '''        private SurfaceGeometryArena GetGeometryArena()
        {
            // Scheduler workers receive an eagerly allocated shared arena. Standalone caches
            // remain cheap until they actually publish their first piece of geometry.
            if (_geometryArena == null)
                _geometryArena = new SurfaceGeometryArena(256 * 1024, 768 * 1024, 512);
            return _geometryArena;
        }

'''
s = once(s, '        private void FinishBuild(int frame)\n',
         get_arena + '        private void FinishBuild(int frame)\n', 'lazy standalone arena')

s = once(s,
    '            _build = default;\n        }\n\n        private static double ElapsedMs',
    '            _build = default;\n            if (_ownsGeometryArena)\n            {\n                _geometryArena?.Dispose();\n                _geometryArena = null;\n            }\n        }\n\n        private static double ElapsedMs',
    'owned arena dispose')
cache_path.write_text(s)


scheduler_path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/VoxelSurfaceScheduler.cs')
q = scheduler_path.read_text()
q = once(q,
    '''            public SurfaceRing(int sourceStep, float innerRadiusMetres, float outerRadiusMetres,
                               int maxResidentChunks)
            {''',
    '''            public SurfaceRing(int sourceStep, float innerRadiusMetres, float outerRadiusMetres,
                               int maxResidentChunks, SurfaceGeometryArena geometryArena)
            {''',
    'ring ctor signature')
q = once(q,
    '                    Workers[i] = new CpuTransvoxelChunkCache(sourceStep)\n',
    '                    Workers[i] = new CpuTransvoxelChunkCache(sourceStep, geometryArena)\n',
    'ring worker arena')
q = once(q,
    '        public const float MaxVoxelRingRadiusMetres = 420f;\n\n        private readonly SurfaceRing[] _rings;',
    '''        public const float MaxVoxelRingRadiusMetres = 420f;
        // Allocated once with the scheduler. Runtime streaming may wait for a free range but
        // cannot grow these buffers and create a render-thread GPU allocation spike.
        private const int SurfaceArenaVertexCapacity = 2 * 1024 * 1024;
        private const int SurfaceArenaIndexCapacity = 6 * 1024 * 1024;
        private const int SurfaceArenaDrawCapacity = 16 * 1024;

        private readonly SurfaceGeometryArena _geometryArena;
        private readonly SurfaceRing[] _rings;''',
    'scheduler arena fields')
q = once(q,
    '''        public VoxelSurfaceScheduler()
        {
            _rings = new SurfaceRing[s_RingLayout.Length];''',
    '''        public VoxelSurfaceScheduler()
        {
            _geometryArena = new SurfaceGeometryArena(SurfaceArenaVertexCapacity,
                                                       SurfaceArenaIndexCapacity,
                                                       SurfaceArenaDrawCapacity);
            _rings = new SurfaceRing[s_RingLayout.Length];''',
    'scheduler arena creation')
q = once(q,
    '                SurfaceRing ring = new(layout.SourceStep, layout.Inner, layout.Outer, 4096);',
    '                SurfaceRing ring = new(layout.SourceStep, layout.Inner, layout.Outer, 4096, _geometryArena);',
    'ring arena argument')
q = once(q,
    '            for (int r = 0; r < _rings.Length; r++) _rings[r].Dispose();\n        }',
    '            for (int r = 0; r < _rings.Length; r++) _rings[r].Dispose();\n            _geometryArena.Dispose();\n        }',
    'scheduler arena disposal')
scheduler_path.write_text(q)


# Structural guards. These fail before git commit if any old per-entry GPU ownership survives.
cache = cache_path.read_text()
scheduler = scheduler_path.read_text()
shader = shader_path.read_text()
arena = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/SurfaceGeometryArena.cs').read_text()
assert '_SurfaceVertexBase' in shader
assert '_SurfaceVertexBase + _SurfaceIndices[_SurfaceIndexBase + vertexID]' in shader
assert 'new ComputeBuffer' not in cache
assert '_arena.TryAcquire' in cache
assert '_liveLease.ArgsWordStart * sizeof(uint)' in cache
assert 'new SurfaceGeometryArena(SurfaceArenaVertexCapacity' in scheduler
assert 'Workers[i] = new CpuTransvoxelChunkCache(sourceStep, geometryArena)' in scheduler
assert 'Streaming never creates a ComputeBuffer' in arena
