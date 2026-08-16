from pathlib import Path


def once(text, old, new, label):
    n = text.count(old)
    if n != 1:
        raise SystemExit(f'{label}: expected one match, found {n}')
    return text.replace(old, new, 1)


arena_path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/SurfaceGeometryArena.cs')
a = arena_path.read_text()
a = once(a,
    '        public int UsedArgsRecords => _argsRanges.Used / ArgsWordsPerDraw;\n        public long CommittedGpuBytes =>',
    '        public int UsedArgsRecords => _argsRanges.Used / ArgsWordsPerDraw;\n        public ulong AllocationFailureCount { get; private set; }\n        public long UsedGpuBytes =>\n            (long)UsedVertices * SmoothSurfaceVertex.Stride\n            + (long)UsedIndices * sizeof(uint)\n            + (long)UsedArgsRecords * ArgsWordsPerDraw * sizeof(uint);\n        public long CommittedGpuBytes =>',
    'arena metrics')
a = once(a,
    '            if (!_vertexRanges.TryAllocate(vertices, out int vertexStart)) return false;',
    '            if (!_vertexRanges.TryAllocate(vertices, out int vertexStart))\n            {\n                AllocationFailureCount++;\n                return false;\n            }',
    'vertex allocation failure')
a = once(a,
    '                _vertexRanges.Release(vertexStart, vertices);\n                return false;',
    '                _vertexRanges.Release(vertexStart, vertices);\n                AllocationFailureCount++;\n                return false;',
    'index allocation failure')
a = once(a,
    '                _indexRanges.Release(indexStart, indices);\n                _vertexRanges.Release(vertexStart, vertices);\n                return false;',
    '                _indexRanges.Release(indexStart, indices);\n                _vertexRanges.Release(vertexStart, vertices);\n                AllocationFailureCount++;\n                return false;',
    'args allocation failure')
arena_path.write_text(a)


cache_path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs')
s = cache_path.read_text()
s = once(s,
    '            private readonly uint[] _indirectArgs = new uint[4];\n',
    '            private readonly uint[] _indirectArgs = new uint[4];\n            internal bool WaitingForArena { get; private set; }\n',
    'entry arena wait flag')
s = once(s,
    '                if (_stagingLease.IsValid) return true;\n                if (!_arena.TryAcquire(vertexCount, indexCount, out _stagingLease)) return false;\n                _stagingVertexCursor = 0;',
    '                if (_stagingLease.IsValid) return true;\n                if (!_arena.TryAcquire(vertexCount, indexCount, out _stagingLease))\n                {\n                    WaitingForArena = true;\n                    return false;\n                }\n                WaitingForArena = false;\n                _stagingVertexCursor = 0;',
    'entry arena acquisition')
s = once(s,
    '                _stagingLease = default;\n                _stagingVertexCursor = 0;\n                _stagingIndexCursor = 0;\n            }\n\n            public Bounds WorldBounds',
    '                _stagingLease = default;\n                WaitingForArena = false;\n                _stagingVertexCursor = 0;\n                _stagingIndexCursor = 0;\n            }\n\n            public Bounds WorldBounds',
    'cancel arena wait')

# Bounded emergency reclamation: only off-frustum, never the entry being replaced.
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
         'arena pressure eviction method')
cache_path.write_text(s)


scheduler_path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/VoxelSurfaceScheduler.cs')
q = scheduler_path.read_text()
q = once(q,
    '        public readonly int LastFrameSolidUploadCompletions;\n        public readonly double LastSolidSnapshotMs;',
    '        public readonly int LastFrameSolidUploadCompletions;\n        public readonly long SolidArenaCommittedBytes;\n        public readonly long SolidArenaUsedBytes;\n        public readonly ulong SolidArenaAllocationFailures;\n        public readonly ulong SolidArenaPressureEvictions;\n        public readonly double LastSolidSnapshotMs;',
    'metric arena fields')
q = once(q,
    '            LastFrameSolidUploadCompletions = 0;\n            LastSolidSnapshotMs = solids.LastSnapshotMs;',
    '            LastFrameSolidUploadCompletions = 0;\n            SolidArenaCommittedBytes = 0;\n            SolidArenaUsedBytes = 0;\n            SolidArenaAllocationFailures = 0;\n            SolidArenaPressureEvictions = 0;\n            LastSolidSnapshotMs = solids.LastSnapshotMs;',
    'single cache arena metrics')
q = once(q,
    '                                     int lastFrameSolidUploadCompletions,\n                                     in VoxelTimingSummary schedulerPrepare,',
    '                                     int lastFrameSolidUploadCompletions,\n                                     long solidArenaCommittedBytes,\n                                     long solidArenaUsedBytes,\n                                     ulong solidArenaAllocationFailures,\n                                     ulong solidArenaPressureEvictions,\n                                     in VoxelTimingSummary schedulerPrepare,',
    'aggregate arena metric signature')
q = once(q,
    '            LastFrameSolidUploadCompletions = lastFrameSolidUploadCompletions;\n            CompletedSolidBuilds = completed;',
    '            LastFrameSolidUploadCompletions = lastFrameSolidUploadCompletions;\n            SolidArenaCommittedBytes = solidArenaCommittedBytes;\n            SolidArenaUsedBytes = solidArenaUsedBytes;\n            SolidArenaAllocationFailures = solidArenaAllocationFailures;\n            SolidArenaPressureEvictions = solidArenaPressureEvictions;\n            CompletedSolidBuilds = completed;',
    'aggregate arena metric assignments')
q = once(q,
    '        private int _lastFrameSolidUploadCompletions;\n        private int _lastAdvancedFrame = -1;',
    '        private int _lastFrameSolidUploadCompletions;\n        private int _arenaPressureCursor;\n        private ulong _observedArenaAllocationFailures;\n        private ulong _arenaPressureEvictions;\n        private int _lastAdvancedFrame = -1;',
    'scheduler pressure state')
q = once(q,
    '            _visibleSolids.Count, SolidUploadBudgetBytes, _lastFrameSolidUploadedBytes,\n            _lastFrameSolidUploadCompletions, _prepareTiming.Snapshot(),',
    '            _visibleSolids.Count, SolidUploadBudgetBytes, _lastFrameSolidUploadedBytes,\n            _lastFrameSolidUploadCompletions, _geometryArena.CommittedGpuBytes,\n            _geometryArena.UsedGpuBytes, _geometryArena.AllocationFailureCount,\n            _arenaPressureEvictions, _prepareTiming.Snapshot(),',
    'metrics arena values')

# Reclaim at most one offscreen entry in a frame after a new allocation failure. The waiting
# upload retries on a later frame, preserving the no-stall contract and old visible geometry.
needle = '''            if (workerCount > 0)
                _uploadAdmissionCursor = (_uploadAdmissionCursor
                                         + Math.Max(1, uploadScanAdvance)) % workerCount;

            _water.InvalidateSurfaceBricks(storage, _discoveredSurfaceBricks);'''
replacement = '''            if (workerCount > 0)
                _uploadAdmissionCursor = (_uploadAdmissionCursor
                                         + Math.Max(1, uploadScanAdvance)) % workerCount;

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
q = once(q, needle, replacement, 'scheduler arena pressure policy')
scheduler_path.write_text(q)


# Guards
arena = arena_path.read_text()
cache = cache_path.read_text()
scheduler = scheduler_path.read_text()
assert 'AllocationFailureCount++' in arena
assert 'public long UsedGpuBytes' in arena
assert 'TryEvictOneForArenaPressure' in cache
assert 'pair.Key.Equals(_build.Coordinate)' in cache
assert 'GeometryUtility.TestPlanesAABB' in cache
assert '_arenaPressureEvictions++' in scheduler
assert 'SolidArenaAllocationFailures' in scheduler
assert 'SolidArenaUsedBytes' in scheduler
