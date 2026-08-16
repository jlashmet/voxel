from pathlib import Path


def once(text, old, new, label):
    n = text.count(old)
    if n != 1:
        raise SystemExit(f'{label}: expected one match, found {n}')
    return text.replace(old, new, 1)


# Arena utilization + allocation failure telemetry.
arena_path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/SurfaceGeometryArena.cs')
a = arena_path.read_text()
a = once(a,
'''        public int UsedArgsRecords => _argsRanges.Used / ArgsWordsPerDraw;
        public long CommittedGpuBytes =>''',
'''        public int UsedArgsRecords => _argsRanges.Used / ArgsWordsPerDraw;
        public ulong AllocationFailureCount { get; private set; }
        public long UsedGpuBytes =>
            (long)UsedVertices * SmoothSurfaceVertex.Stride
            + (long)UsedIndices * sizeof(uint)
            + (long)UsedArgsRecords * ArgsWordsPerDraw * sizeof(uint);
        public long CommittedGpuBytes =>''', 'arena metrics')
start = a.index('        public bool TryAcquire(int vertexCount, int indexCount, out SurfaceGeometryLease lease)')
end = a.index('        public void Release(in SurfaceGeometryLease lease)', start)
new_try = '''        public bool TryAcquire(int vertexCount, int indexCount, out SurfaceGeometryLease lease)
        {
            lease = default;
            if (_disposed) return false;

            int vertices = Align(math.max(1, vertexCount), VertexAlignment);
            int indices = Align(math.max(1, indexCount), IndexAlignment);
            if (!_vertexRanges.TryAllocate(vertices, out int vertexStart))
            {
                AllocationFailureCount++;
                return false;
            }
            if (!_indexRanges.TryAllocate(indices, out int indexStart))
            {
                _vertexRanges.Release(vertexStart, vertices);
                AllocationFailureCount++;
                return false;
            }
            if (!_argsRanges.TryAllocate(ArgsWordsPerDraw, out int argsStart))
            {
                _indexRanges.Release(indexStart, indices);
                _vertexRanges.Release(vertexStart, vertices);
                AllocationFailureCount++;
                return false;
            }

            lease = new SurfaceGeometryLease(vertexStart, vertices, indexStart, indices, argsStart);
            return true;
        }

'''
a = a[:start] + new_try + a[end:]
arena_path.write_text(a)


# Worker can report arena wait and release exactly one safe offscreen victim under pressure.
cache_path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs')
s = cache_path.read_text()
s = once(s,
'            private readonly uint[] _indirectArgs = new uint[4];\n',
'            private readonly uint[] _indirectArgs = new uint[4];\n            internal bool WaitingForArena { get; private set; }\n',
'entry arena wait flag')
start = s.index('            private bool EnsureUploadStaging(int vertexCount, int indexCount)')
end = s.index('            internal void CancelUpload()', start)
new_ensure = '''            private bool EnsureUploadStaging(int vertexCount, int indexCount)
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

'''
s = s[:start] + new_ensure + s[end:]
s = once(s,
'''                _arena.Release(in _stagingLease);
                _stagingLease = default;
                _stagingVertexCursor = 0;''',
'''                _arena.Release(in _stagingLease);
                _stagingLease = default;
                WaitingForArena = false;
                _stagingVertexCursor = 0;''', 'cancel arena wait')
pressure_method = '''        internal bool TryEvictOneForArenaPressure(Camera camera, float voxelSize)
        {
            if (_entries.Count == 0) return false;

            int3 victim = default;
            float farthest = -1f;
            Vector3 cameraPosition = camera != null ? camera.transform.position : Vector3.zero;
            float chunkMetres = VoxelsPerAxis * voxelSize;
            if (camera != null) GeometryUtility.CalculateFrustumPlanes(camera, _frustumPlanes);

            foreach (var pair in _entries)
            {
                // Never retire the old geometry for the replacement that is currently waiting.
                if (_build.Active && pair.Key.Equals(_build.Coordinate)) continue;
                Bounds bounds = ChunkWorldBounds(pair.Key, voxelSize);
                if (camera != null && GeometryUtility.TestPlanesAABB(_frustumPlanes, bounds))
                    continue;

                Vector3 centre = (new Vector3(pair.Key.x, pair.Key.y, pair.Key.z)
                                + Vector3.one * 0.5f) * chunkMetres;
                float distance = (centre - cameraPosition).sqrMagnitude;
                if (distance <= farthest) continue;
                farthest = distance;
                victim = pair.Key;
            }

            if (farthest < 0f) return false;
            if (_entries.TryGetValue(victim, out Entry entry)) entry.Dispose();
            _entries.Remove(victim);
            _dirty.Add(victim);
            return true;
        }

'''
s = once(s, '        private void EnforceCapacity(Camera camera, float voxelSize)\n',
         pressure_method + '        private void EnforceCapacity(Camera camera, float voxelSize)\n',
         'pressure eviction method')
cache_path.write_text(s)


# Surface metrics + bounded one-victim-per-frame reclamation after a fresh arena failure.
scheduler_path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/VoxelSurfaceScheduler.cs')
q = scheduler_path.read_text()
q = once(q,
'''        public readonly int LastFrameSolidUploadCompletions;
        public readonly double LastSolidSnapshotMs;''',
'''        public readonly int LastFrameSolidUploadCompletions;
        public readonly long SolidArenaCommittedBytes;
        public readonly long SolidArenaUsedBytes;
        public readonly ulong SolidArenaAllocationFailures;
        public readonly ulong SolidArenaPressureEvictions;
        public readonly double LastSolidSnapshotMs;''', 'metric fields')
q = once(q,
'''            LastFrameSolidUploadCompletions = 0;
            LastSolidSnapshotMs = solids.LastSnapshotMs;''',
'''            LastFrameSolidUploadCompletions = 0;
            SolidArenaCommittedBytes = 0;
            SolidArenaUsedBytes = 0;
            SolidArenaAllocationFailures = 0;
            SolidArenaPressureEvictions = 0;
            LastSolidSnapshotMs = solids.LastSnapshotMs;''', 'single metrics')
q = once(q,
'''                                     int lastFrameSolidUploadCompletions,
                                     in VoxelTimingSummary schedulerPrepare,''',
'''                                     int lastFrameSolidUploadCompletions,
                                     long solidArenaCommittedBytes,
                                     long solidArenaUsedBytes,
                                     ulong solidArenaAllocationFailures,
                                     ulong solidArenaPressureEvictions,
                                     in VoxelTimingSummary schedulerPrepare,''', 'aggregate signature')
q = once(q,
'''            LastFrameSolidUploadCompletions = lastFrameSolidUploadCompletions;
            CompletedSolidBuilds = completed;''',
'''            LastFrameSolidUploadCompletions = lastFrameSolidUploadCompletions;
            SolidArenaCommittedBytes = solidArenaCommittedBytes;
            SolidArenaUsedBytes = solidArenaUsedBytes;
            SolidArenaAllocationFailures = solidArenaAllocationFailures;
            SolidArenaPressureEvictions = solidArenaPressureEvictions;
            CompletedSolidBuilds = completed;''', 'aggregate assignments')
q = once(q,
'''        private int _lastFrameSolidUploadCompletions;
        private int _lastAdvancedFrame = -1;''',
'''        private int _lastFrameSolidUploadCompletions;
        private int _arenaPressureCursor;
        private ulong _observedArenaAllocationFailures;
        private ulong _arenaPressureEvictions;
        private int _lastAdvancedFrame = -1;''', 'pressure state')
q = once(q,
'''            _visibleSolids.Count, SolidUploadBudgetBytes, _lastFrameSolidUploadedBytes,
            _lastFrameSolidUploadCompletions, _prepareTiming.Snapshot(),''',
'''            _visibleSolids.Count, SolidUploadBudgetBytes, _lastFrameSolidUploadedBytes,
            _lastFrameSolidUploadCompletions, _geometryArena.CommittedGpuBytes,
            _geometryArena.UsedGpuBytes, _geometryArena.AllocationFailureCount,
            _arenaPressureEvictions, _prepareTiming.Snapshot(),''', 'metric values')
needle = '''            if (workerCount > 0)
                _uploadAdmissionCursor = (_uploadAdmissionCursor
                                         + Math.Max(1, uploadScanAdvance)) % workerCount;

            _water.InvalidateSurfaceBricks(storage, _discoveredSurfaceBricks);'''
replacement = '''            if (workerCount > 0)
                _uploadAdmissionCursor = (_uploadAdmissionCursor
                                         + Math.Max(1, uploadScanAdvance)) % workerCount;

            // Allocation pressure never creates a new GPU buffer. Reclaim at most one
            // offscreen lease per frame, then let the pending replacement retry next frame.
            ulong arenaFailures = _geometryArena.AllocationFailureCount;
            if (arenaFailures > _observedArenaAllocationFailures && workerCount > 0)
            {
                _observedArenaAllocationFailures = arenaFailures;
                for (int offset = 0; offset < workerCount; offset++)
                {
                    int index = (_arenaPressureCursor + offset) % workerCount;
                    if (!_allWorkers[index].TryEvictOneForArenaPressure(camera, voxelSize))
                        continue;
                    _arenaPressureCursor = (index + 1) % workerCount;
                    _arenaPressureEvictions++;
                    break;
                }
            }

            _water.InvalidateSurfaceBricks(storage, _discoveredSurfaceBricks);'''
q = once(q, needle, replacement, 'pressure policy')
scheduler_path.write_text(q)

assert 'AllocationFailureCount++' in arena_path.read_text()
assert 'new ComputeBuffer' not in cache_path.read_text()
assert 'TryEvictOneForArenaPressure' in cache_path.read_text()
assert '_arenaPressureEvictions++' in scheduler_path.read_text()
assert 'SolidArenaUsedBytes' in scheduler_path.read_text()
