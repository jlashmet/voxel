from pathlib import Path


def once(text, old, new, label):
    n = text.count(old)
    if n != 1:
        raise SystemExit(f'{label}: expected one match, found {n}')
    return text.replace(old, new, 1)


water_path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuWaterSurfaceChunkCache.cs')
s = water_path.read_text()

s = once(s,
'''        private const int BricksPerSlice = 256;
        private const uint FullyLitOcclusion = 0x0000FF00u;''',
'''        private const int BricksPerSlice = 8;
        private const int ArenaVertexCapacity = 256 * 1024;
        private const int ArenaIndexCapacity = 768 * 1024;
        private const int ArenaDrawCapacity = 2048;
        private const uint FullyLitOcclusion = 0x0000FF00u;''', 'water bounded constants')

s = once(s,
'''        private static readonly int s_SurfaceIndexBase = Shader.PropertyToID("_SurfaceIndexBase");
        private static readonly int[] s_Strides = { 1, E, E * E };''',
'''        private static readonly int s_SurfaceIndexBase = Shader.PropertyToID("_SurfaceIndexBase");
        private static readonly int s_SurfaceVertexBase = Shader.PropertyToID("_SurfaceVertexBase");
        private static readonly int[] s_Strides = { 1, E, E * E };''', 'water vertex base shader id')

entry_start = s.index('        public sealed class Entry : IDisposable')
entry_end = s.index('        private struct BuildState', entry_start)
entry = r'''        public sealed class Entry : IDisposable
        {
            public readonly int3 Coordinate;
            private readonly SurfaceGeometryArena _arena;
            private SurfaceGeometryLease _liveLease;
            private SurfaceGeometryLease _stagingLease;
            private int _stagingVertexCursor;
            private int _stagingIndexCursor;
            public ComputeBuffer Vertices => _arena.Vertices;
            public ComputeBuffer Indices => _arena.Indices;
            public ComputeBuffer Args => _arena.Args;
            public bool Ready;
            public int IndexCount;
            public long GpuBytes { get; private set; }
            public ulong SourceVersion { get; internal set; }
            internal bool WaitingForArena { get; private set; }

            internal Entry(int3 coordinate, SurfaceGeometryArena arena)
            {
                Coordinate = coordinate;
                _arena = arena ?? throw new ArgumentNullException(nameof(arena));
            }

            internal int RemainingUploadBytes(int vertexCount, int indexCount)
            {
                int verticesRemaining = math.max(0, vertexCount - _stagingVertexCursor);
                int indicesRemaining = math.max(0, indexCount - _stagingIndexCursor);
                return verticesRemaining * SmoothSurfaceVertex.Stride
                     + indicesRemaining * sizeof(uint)
                     + SurfaceGeometryArena.ArgsWordsPerDraw * sizeof(uint);
            }

            internal bool AdvanceUpload(NativeList<SmoothSurfaceVertex> vertices,
                                        NativeList<uint> indices,
                                        int byteBudget,
                                        out int uploadedBytes)
            {
                uploadedBytes = 0;
                if (byteBudget <= 0 || !EnsureUploadStaging(vertices.Length, indices.Length))
                    return false;

                int remainingBudget = byteBudget;
                int vertexRemaining = vertices.Length - _stagingVertexCursor;
                if (vertexRemaining > 0 && remainingBudget >= SmoothSurfaceVertex.Stride)
                {
                    int count = math.min(vertexRemaining,
                        remainingBudget / SmoothSurfaceVertex.Stride);
                    _arena.UploadVertices(vertices.AsArray(), _stagingVertexCursor,
                                          in _stagingLease, count);
                    int bytes = count * SmoothSurfaceVertex.Stride;
                    _stagingVertexCursor += count;
                    remainingBudget -= bytes;
                    uploadedBytes += bytes;
                }

                int indexRemaining = indices.Length - _stagingIndexCursor;
                if (_stagingVertexCursor == vertices.Length && indexRemaining > 0
                    && remainingBudget >= sizeof(uint))
                {
                    int count = math.min(indexRemaining, remainingBudget / sizeof(uint));
                    _arena.UploadIndices(indices.AsArray(), _stagingIndexCursor,
                                         in _stagingLease, count);
                    int bytes = count * sizeof(uint);
                    _stagingIndexCursor += count;
                    remainingBudget -= bytes;
                    uploadedBytes += bytes;
                }

                const int argsBytes = SurfaceGeometryArena.ArgsWordsPerDraw * sizeof(uint);
                if (_stagingVertexCursor != vertices.Length
                    || _stagingIndexCursor != indices.Length
                    || remainingBudget < argsBytes)
                    return false;

                _arena.UploadArgs((uint)indices.Length, in _stagingLease);
                uploadedBytes += argsBytes;

                SurfaceGeometryLease previous = _liveLease;
                _liveLease = _stagingLease;
                _stagingLease = default;
                _stagingVertexCursor = 0;
                _stagingIndexCursor = 0;
                IndexCount = indices.Length;
                GpuBytes = _arena.ReservedBytes(in _liveLease);
                Ready = true;
                WaitingForArena = false;
                _arena.Release(in previous);
                return true;
            }

            private bool EnsureUploadStaging(int vertexCount, int indexCount)
            {
                if (_stagingLease.IsValid) return true;
                if (!_arena.TryAcquire(vertexCount, indexCount, out _stagingLease))
                {
                    WaitingForArena = true;
                    return false;
                }
                WaitingForArena = false;
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
                WaitingForArena = false;
            }

            public Bounds WorldBounds(float voxelSize)
            {
                float size = VoxelsPerAxis * voxelSize;
                Vector3 min = new Vector3(Coordinate.x, Coordinate.y, Coordinate.z) * size;
                return new Bounds(min + Vector3.one * (size * 0.5f), Vector3.one * size);
            }

            public void Draw(CommandBuffer commandBuffer, Material material,
                             MaterialPropertyBlock properties)
            {
                if (!Ready || IndexCount == 0) return;

                properties.SetBuffer(s_SurfaceVertices, _arena.Vertices);
                properties.SetBuffer(s_SurfaceIndices, _arena.Indices);
                properties.SetInt(s_SurfaceVertexBase, _liveLease.VertexStart);
                properties.SetInt(s_SurfaceIndexBase, _liveLease.IndexStart);
                commandBuffer.DrawProceduralIndirect(Matrix4x4.identity, material, 0,
                    MeshTopology.Triangles, _arena.Args,
                    _liveLease.ArgsWordStart * sizeof(uint), properties);
            }

            public void Dispose()
            {
                CancelUpload();
                _arena.Release(in _liveLease);
                _liveLease = default;
                Ready = false;
                IndexCount = 0;
                GpuBytes = 0;
            }
        }

'''
s = s[:entry_start] + entry + s[entry_end:]

s = once(s,
'''        private struct BuildState
        {
            public bool Active;
            public int3 Coordinate;
            public int Cursor;
            public ulong SourceVersion;
        }''',
'''        private struct BuildState
        {
            public bool Active;
            public int3 Coordinate;
            public int Cursor;
            public ulong SourceVersion;
            public bool PendingPublication;
        }''', 'water pending publication build state')

s = once(s,
'''        private readonly List<Entry> _visible = new();
        private readonly Plane[] _frustumPlanes = new Plane[6];
        private readonly List<SmoothSurfaceVertex> _vertices = new(4096);
        private readonly List<uint> _indices = new(6144);
        private readonly NativeArray<byte> _brickMaterials =''',
'''        private readonly List<Entry> _visible = new();
        private readonly Plane[] _frustumPlanes = new Plane[6];
        private readonly SurfaceGeometryArena _geometryArena;
        private NativeList<SmoothSurfaceVertex> _vertices;
        private NativeList<uint> _indices;
        private readonly NativeArray<byte> _brickMaterials =''', 'water native output fields')

s = once(s,
'''        private readonly byte[] _mask = new byte[E * E];
        private BuildState _build;

        public int ResidentCount => _entries.Count;''',
'''        private readonly byte[] _mask = new byte[E * E];
        private BuildState _build;

        public CpuWaterSurfaceChunkCache()
        {
            _geometryArena = new SurfaceGeometryArena(
                ArenaVertexCapacity, ArenaIndexCapacity, ArenaDrawCapacity);
            _vertices = new NativeList<SmoothSurfaceVertex>(4096, Allocator.Persistent);
            _indices = new NativeList<uint>(6144, Allocator.Persistent);
        }

        public int ResidentCount => _entries.Count;''', 'water constructor and arena')

s = once(s,
'''        public IReadOnlyList<Entry> Visible => _visible;
''',
'''        public IReadOnlyList<Entry> Visible => _visible;
        public int PendingUploadCount => _build.Active && _build.PendingPublication ? 1 : 0;
        public int PendingUploadBytes
        {
            get
            {
                if (!_build.Active || !_build.PendingPublication) return 0;
                if (!_entries.TryGetValue(_build.Coordinate, out Entry entry))
                    return _vertices.Length * SmoothSurfaceVertex.Stride
                         + _indices.Length * sizeof(uint)
                         + SurfaceGeometryArena.ArgsWordsPerDraw * sizeof(uint);
                return entry.RemainingUploadBytes(_vertices.Length, _indices.Length);
            }
        }
        public ulong ArenaAllocationFailures => _geometryArena.AllocationFailureCount;
''', 'water publication diagnostics')

old_prepare = '''        public void Prepare(IRegionReadSource storage, Camera camera,
                            float voxelSize, double budgetMs = 0.15)
        {
            if (storage == null) return;
            DropNoLongerResident(storage);
            if (camera == null || (_dirty.Count == 0 && !_build.Active)) return;

            double deadline = Time.realtimeSinceStartupAsDouble
                            + math.max(0.0, budgetMs) * 0.001;
            do
            {
                if (!_build.Active && !BeginNearestBuild(camera.transform.position, voxelSize)) break;
                if (StepBuild(storage, voxelSize)) FinishBuild();
            }
            while (Time.realtimeSinceStartupAsDouble < deadline);
        }'''
new_prepare = '''        public void Prepare(IRegionReadSource storage, Camera camera,
                            float voxelSize, double budgetMs = 0.15)
        {
            if (storage == null) return;
            DropNoLongerResident(storage);
            if (camera == null || _build.PendingPublication
                || (_dirty.Count == 0 && !_build.Active)) return;

            double deadline = Time.realtimeSinceStartupAsDouble
                            + math.max(0.0, budgetMs) * 0.001;
            while (Time.realtimeSinceStartupAsDouble < deadline)
            {
                if (!_build.Active && !BeginNearestBuild(camera.transform.position, voxelSize)) break;
                if (!StepBuild(storage, voxelSize, deadline)) break;
                FinishCpuBuild();
                if (_build.PendingPublication) break;
            }
        }

        public bool TryPublishPending(int byteBudget, out int uploadedBytes)
        {
            uploadedBytes = 0;
            if (!_build.Active || !_build.PendingPublication || byteBudget <= 0) return false;

            if (_desiredVersions.TryGetValue(_build.Coordinate, out ulong desired)
                && desired > _build.SourceVersion)
            {
                StaleBuildCount++;
                if (_entries.TryGetValue(_build.Coordinate, out Entry stale)) stale.CancelUpload();
                ResetBuildOutput();
                return false;
            }

            if (_indices.Length == 0)
            {
                if (_entries.TryGetValue(_build.Coordinate, out Entry empty)) empty.Dispose();
                _entries.Remove(_build.Coordinate);
                _desiredVersions.Remove(_build.Coordinate);
                CompletedBuildCount++;
                ResetBuildOutput();
                return true;
            }

            if (!_entries.TryGetValue(_build.Coordinate, out Entry entry))
            {
                entry = new Entry(_build.Coordinate, _geometryArena);
                _entries.Add(_build.Coordinate, entry);
            }

            bool complete = entry.AdvanceUpload(_vertices, _indices, byteBudget, out uploadedBytes);
            UploadedGeometryBytes += (ulong)uploadedBytes;
            if (!complete) return false;

            entry.SourceVersion = _build.SourceVersion;
            _desiredVersions.Remove(_build.Coordinate);
            CompletedBuildCount++;
            ResetBuildOutput();
            return true;
        }'''
s = once(s, old_prepare, new_prepare, 'water bounded prepare/publication')

old_step = '''        private bool StepBuild(IRegionReadSource storage, float voxelSize)
        {
            int end = math.min(_buildBricks.Count, _build.Cursor + BricksPerSlice);
            RegionReadView cachedRegion = default;
            for (int i = _build.Cursor; i < end; i++)
            {
                int3 worldBrick = _buildBricks[i];
                if (!TryLoadBrickMaterials(storage, worldBrick, ref cachedRegion)
                    || !LoadedBrickContainsWater())
                    continue;

                EmitBrick(storage, worldBrick * E, voxelSize);
            }

            _build.Cursor = end;
            return end >= _buildBricks.Count;
        }

        private void FinishBuild()
        {
            if (_desiredVersions.TryGetValue(_build.Coordinate, out ulong desired)
                && desired > _build.SourceVersion)
            {
                StaleBuildCount++;
                _build = default;
                _buildBricks.Clear();
                _vertices.Clear();
                _indices.Clear();
                return;
            }

            if (!_entries.TryGetValue(_build.Coordinate, out Entry entry))
            {
                entry = new Entry(_build.Coordinate);
                _entries.Add(_build.Coordinate, entry);
            }

            entry.Upload(_vertices, _indices);
            CompletedBuildCount++;
            UploadedGeometryBytes += (ulong)entry.GpuBytes;
            entry.SourceVersion = _build.SourceVersion;
            _desiredVersions.Remove(_build.Coordinate);
            _build = default;
            _buildBricks.Clear();
            _vertices.Clear();
            _indices.Clear();
        }'''
new_step = '''        private bool StepBuild(IRegionReadSource storage, float voxelSize, double deadline)
        {
            int end = math.min(_buildBricks.Count, _build.Cursor + BricksPerSlice);
            RegionReadView cachedRegion = default;
            for (int i = _build.Cursor; i < end; i++)
            {
                int3 worldBrick = _buildBricks[i];
                if (TryLoadBrickMaterials(storage, worldBrick, ref cachedRegion)
                    && LoadedBrickContainsWater())
                    EmitBrick(storage, worldBrick * E, voxelSize);

                _build.Cursor = i + 1;
                if (_build.Cursor < _buildBricks.Count
                    && Time.realtimeSinceStartupAsDouble >= deadline)
                    return false;
            }
            return _build.Cursor >= _buildBricks.Count;
        }

        private void FinishCpuBuild()
        {
            if (_desiredVersions.TryGetValue(_build.Coordinate, out ulong desired)
                && desired > _build.SourceVersion)
            {
                StaleBuildCount++;
                ResetBuildOutput();
                return;
            }
            _build.PendingPublication = true;
        }

        private void ResetBuildOutput()
        {
            _build = default;
            _buildBricks.Clear();
            _vertices.Clear();
            _indices.Clear();
        }'''
s = once(s, old_step, new_step, 'water staged CPU completion')

s = s.replace('_vertices.Count', '_vertices.Length')
s = s.replace('_indices.Count', '_indices.Length')

s = once(s,
'''                    _build = default;
                    _buildBricks.Clear();
                    _vertices.Clear();
                    _indices.Clear();''',
'''                    if (_entries.TryGetValue(chunk, out Entry pending)) pending.CancelUpload();
                    ResetBuildOutput();''', 'water residency cancellation')

s = once(s,
'''            _vertices.Clear();
            _indices.Clear();
            if (_brickMaterials.IsCreated) _brickMaterials.Dispose();''',
'''            if (_vertices.IsCreated) _vertices.Dispose();
            if (_indices.IsCreated) _indices.Dispose();
            _geometryArena.Dispose();
            if (_brickMaterials.IsCreated) _brickMaterials.Dispose();''', 'water native/arena dispose')

# There must be no per-rebuild GPU allocations left in the water cache.
s = s.replace('            _vertices.Clear();\n            _indices.Clear();\n            if (_brickMaterials.IsCreated)',
              '            if (_brickMaterials.IsCreated)')

water_path.write_text(s)


scheduler_path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/VoxelSurfaceScheduler.cs')
q = scheduler_path.read_text()
q = once(q,
'''        public double WaterBuildBudgetMs { get; set; } = 0.15;
''',
'''        public double WaterBuildBudgetMs { get; set; } = 0.15;
        /// <summary>Maximum water geometry payload copied into its fixed arena per frame.</summary>
        public int WaterUploadBudgetBytes { get; set; } = 256 * 1024;
        /// <summary>Wall-clock gate for the single water publication slice.</summary>
        public double WaterUploadBudgetMs { get; set; } = 0.10;
        public int LastFrameWaterUploadedBytes { get; private set; }
''', 'water upload scheduler budgets')

q = once(q,
'''            _water.InvalidateSurfaceBricks(storage, _discoveredSurfaceBricks);
            _water.Prepare(storage, camera, voxelSize, WaterBuildBudgetMs);
            _workerPrepareTiming.Add(workerPrepareMs);''',
'''            _water.InvalidateSurfaceBricks(storage, _discoveredSurfaceBricks);
            _water.Prepare(storage, camera, voxelSize, WaterBuildBudgetMs);
            LastFrameWaterUploadedBytes = 0;
            double waterUploadDeadline = Time.realtimeSinceStartupAsDouble
                                       + Math.Max(0.0, WaterUploadBudgetMs) * 0.001;
            if (_water.PendingUploadCount > 0 && WaterUploadBudgetBytes > 0
                && Time.realtimeSinceStartupAsDouble < waterUploadDeadline)
            {
                _water.TryPublishPending(WaterUploadBudgetBytes,
                                         out int waterUploadedBytes);
                LastFrameWaterUploadedBytes = waterUploadedBytes;
            }
            _workerPrepareTiming.Add(workerPrepareMs);''', 'water bounded publication scheduling')
scheduler_path.write_text(q)


tests_path = Path('Assets/Tests/EditMode/GeometryPipelineArchitectureTests.cs')
t = tests_path.read_text()
if 'WaterPublicationUsesFixedArenaAndBoundedSlices' not in t:
    insert = r'''

        [Test]
        public void WaterPublicationUsesFixedArenaAndBoundedSlices()
        {
            string water = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "CpuWaterSurfaceChunkCache.cs"));
            string scheduler = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "VoxelSurfaceScheduler.cs"));
            StringAssert.Contains("SurfaceGeometryArena _geometryArena", water);
            StringAssert.Contains("NativeList<SmoothSurfaceVertex> _vertices", water);
            StringAssert.Contains("NativeList<uint> _indices", water);
            StringAssert.Contains("TryPublishPending", water);
            StringAssert.Contains("Time.realtimeSinceStartupAsDouble >= deadline", water);
            StringAssert.DoesNotContain("new ComputeBuffer", water);
            StringAssert.DoesNotContain("new uint[]", water);
            StringAssert.Contains("WaterUploadBudgetBytes", scheduler);
            StringAssert.Contains("_water.TryPublishPending", scheduler);
        }
'''
    marker = '\n    }\n}'
    pos = t.rfind(marker)
    if pos < 0:
        raise SystemExit('architecture test closing marker missing')
    t = t[:pos] + insert + t[pos:]
tests_path.write_text(t)


doc_path = Path('docs/ASYNC_GEOMETRY_PIPELINE.md')
d = doc_path.read_text()
d = d.replace('- [ ] Give water bounded GPU publication and shared/pool-backed geometry ownership.\n',
              '- [x] Give water bounded GPU publication and shared/pool-backed geometry ownership.\n', 1)
d = d.replace('1. Replace all-known visibility traversal with bounded clipmap slot ownership.\n',
              '1. Split persistent chunk slots from reusable build scratch/workspaces.\n', 1)
doc_path.write_text(d)

water = water_path.read_text()
assert 'new ComputeBuffer' not in water
assert 'new uint[]' not in water
assert 'NativeList<SmoothSurfaceVertex> _vertices' in water
assert 'TryPublishPending' in water
assert 'StepBuild(IRegionReadSource storage, float voxelSize, double deadline)' in water
assert '_water.TryPublishPending' in scheduler_path.read_text()
