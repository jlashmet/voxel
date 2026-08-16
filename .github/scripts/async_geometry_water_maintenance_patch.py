from pathlib import Path


def once(text, old, new, label):
    n = text.count(old)
    if n != 1:
        raise SystemExit(f'{label}: expected one match, found {n}')
    return text.replace(old, new, 1)

path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuWaterSurfaceChunkCache.cs')
s = path.read_text()

s = once(s,
'''        private const int BricksPerSlice = 8;
        private const int ArenaVertexCapacity = 256 * 1024;''',
'''        private const int BricksPerSlice = 8;
        private const int BuildSelectionCandidatesPerPrepare = 32;
        private const int ResidencyChecksPerPrepare = 16;
        private const int RegionInvalidationCandidatesPerPrepare = 64;
        private const int WaterChunksPerRegion = VoxelGrid.RegionVoxelEdge / VoxelsPerAxis;
        private const int ArenaVertexCapacity = 256 * 1024;''', 'water maintenance constants')

s = once(s,
'''            public readonly int3 Coordinate;
            private readonly SurfaceGeometryArena _arena;''',
'''            public int3 Coordinate { get; private set; }
            private readonly SurfaceGeometryArena _arena;''', 'reusable water entry coordinate')
s = once(s,
'''            internal Entry(int3 coordinate, SurfaceGeometryArena arena)
            {
                Coordinate = coordinate;
                _arena = arena ?? throw new ArgumentNullException(nameof(arena));
            }
''',
'''            internal Entry(int3 coordinate, SurfaceGeometryArena arena)
            {
                Coordinate = coordinate;
                _arena = arena ?? throw new ArgumentNullException(nameof(arena));
            }

            internal void Reinitialize(int3 coordinate)
            {
                if (Ready || _liveLease.IsValid || _stagingLease.IsValid)
                    throw new InvalidOperationException(
                        "A water entry must release its arena leases before reuse.");
                Coordinate = coordinate;
                IndexCount = 0;
                GpuBytes = 0;
                SourceVersion = 0;
                WaitingForArena = false;
                _stagingVertexCursor = 0;
                _stagingIndexCursor = 0;
            }
''', 'water entry reinitialize')

s = once(s,
'''        private readonly Dictionary<int3, HashSet<int3>> _waterBricks = new();
        private readonly Dictionary<int3, Entry> _entries = new();
        private readonly HashSet<int3> _dirty = new();
        private readonly Dictionary<int3, ulong> _desiredVersions = new();
        private ulong _versionCounter;
        private readonly List<int3> _buildBricks = new(256);''',
'''        private readonly Dictionary<int3, HashSet<int3>> _waterBricks = new();
        private readonly Dictionary<int3, Entry> _entries = new();
        private readonly Stack<Entry> _entryPool = new();

        private readonly HashSet<int3> _dirty = new();
        private readonly Queue<int3> _dirtyQueue = new();
        private readonly HashSet<int3> _queuedDirty = new();
        private readonly Dictionary<int3, ulong> _desiredVersions = new();
        private ulong _versionCounter;

        private readonly Queue<int3> _residencyQueue = new();
        private readonly HashSet<int3> _queuedResidency = new();

        private readonly Queue<int3> _regionInvalidationQueue = new();
        private readonly HashSet<int3> _queuedRegionInvalidations = new();
        private readonly HashSet<int3> _rescanRegionInvalidations = new();
        private bool _hasActiveRegionInvalidation;
        private int3 _activeRegionInvalidation;
        private int3 _activeRegionMinChunk;
        private int _activeRegionCandidateCursor;
''', 'water maintenance state')

s = once(s,
'''                    if (!_waterBricks.TryGetValue(chunk, out HashSet<int3> set))
                    {
                        set = new HashSet<int3>();
                        _waterBricks.Add(chunk, set);
                    }
                    if (set.Add(worldBrick)) Invalidate(chunk);''',
'''                    if (!_waterBricks.TryGetValue(chunk, out HashSet<int3> set))
                    {
                        set = new HashSet<int3>();
                        _waterBricks.Add(chunk, set);
                        TrackResidentChunk(chunk);
                    }
                    if (set.Add(worldBrick)) Invalidate(chunk);''', 'track water chunk residency')

old_regions = '''        public void InvalidateDirtyRegions(HashSet<int3> dirtyRegions)
        {
            if (dirtyRegions == null || dirtyRegions.Count == 0 || _waterBricks.Count == 0) return;

            foreach (var pair in _waterBricks)
            {
                int3 ownerRegion = ChunkRegion(pair.Key);
                foreach (int3 dirtyRegion in dirtyRegions)
                {
                    int3 delta = math.abs(ownerRegion - dirtyRegion);
                    if (math.max(delta.x, math.max(delta.y, delta.z)) > 1) continue;
                    Invalidate(pair.Key);
                    break;
                }
            }
        }'''
new_regions = '''        public void InvalidateDirtyRegions(HashSet<int3> dirtyRegions)
        {
            if (dirtyRegions != null)
            {
                foreach (int3 region in dirtyRegions)
                {
                    if (_hasActiveRegionInvalidation && region.Equals(_activeRegionInvalidation))
                    {
                        _rescanRegionInvalidations.Add(region);
                        continue;
                    }
                    if (_queuedRegionInvalidations.Add(region))
                        _regionInvalidationQueue.Enqueue(region);
                }
            }
            StepRegionInvalidation();
        }

        private void StepRegionInvalidation()
        {
            int remaining = RegionInvalidationCandidatesPerPrepare;
            int span = WaterChunksPerRegion * 3;
            int total = span * span * span;
            while (remaining > 0)
            {
                if (!_hasActiveRegionInvalidation)
                {
                    if (_regionInvalidationQueue.Count == 0) return;
                    _activeRegionInvalidation = _regionInvalidationQueue.Dequeue();
                    _activeRegionMinChunk = (_activeRegionInvalidation - 1)
                                          * WaterChunksPerRegion;
                    _activeRegionCandidateCursor = 0;
                    _hasActiveRegionInvalidation = true;
                }

                while (remaining > 0 && _activeRegionCandidateCursor < total)
                {
                    int linear = _activeRegionCandidateCursor++;
                    int x = linear % span;
                    int y = (linear / span) % span;
                    int z = linear / (span * span);
                    int3 chunk = _activeRegionMinChunk + new int3(x, y, z);
                    remaining--;
                    if (_waterBricks.ContainsKey(chunk)) Invalidate(chunk);
                }

                if (_activeRegionCandidateCursor < total) return;
                int3 completed = _activeRegionInvalidation;
                bool rescan = _rescanRegionInvalidations.Remove(completed);
                _queuedRegionInvalidations.Remove(completed);
                _hasActiveRegionInvalidation = false;
                _activeRegionCandidateCursor = 0;
                if (rescan && _queuedRegionInvalidations.Add(completed))
                    _regionInvalidationQueue.Enqueue(completed);
            }
        }'''
s = once(s, old_regions, new_regions, 'bounded water region invalidation')

s = once(s,
'''            if (storage == null) return;
            DropNoLongerResident(storage);
            if (camera == null || _build.PendingPublication''',
'''            if (storage == null) return;
            StepResidencyPrune(storage);
            if (camera == null || _build.PendingPublication''', 'bounded water residency call')

s = once(s,
'''                if (!_build.Active && !BeginNearestBuild(camera.transform.position, voxelSize)) break;''',
'''                if (!_build.Active
                    && !BeginNearestBuild(camera.transform.position, voxelSize, deadline)) break;''', 'bounded water build select call')

# pooled entry acquisition
s = once(s,
'''            if (!_entries.TryGetValue(_build.Coordinate, out Entry entry))
            {
                entry = new Entry(_build.Coordinate, _geometryArena);
                _entries.Add(_build.Coordinate, entry);
            }''',
'''            if (!_entries.TryGetValue(_build.Coordinate, out Entry entry))
            {
                entry = AcquireEntry(_build.Coordinate);
                _entries.Add(_build.Coordinate, entry);
            }''', 'pooled water entry acquisition')

# Replace nearest-build method: bounded queue, no full HashSet snapshot copy.
start = s.index('        private bool BeginNearestBuild(')
end = s.index('        private bool StepBuild(', start)
begin = r'''        private bool BeginNearestBuild(Vector3 cameraWorldPosition, float voxelSize,
                                       double deadline)
        {
            if (_dirty.Count == 0 || _dirtyQueue.Count == 0
                || Time.realtimeSinceStartupAsDouble >= deadline)
                return false;

            int3 best = default;
            bool hasBest = false;
            float bestDistance = float.PositiveInfinity;
            float chunkMetres = VoxelsPerAxis * voxelSize;
            int candidates = math.min(BuildSelectionCandidatesPerPrepare, _dirtyQueue.Count);
            for (int i = 0; i < candidates; i++)
            {
                int3 candidate = _dirtyQueue.Dequeue();
                _queuedDirty.Remove(candidate);
                if (!_dirty.Contains(candidate)) continue;

                if (!_waterBricks.TryGetValue(candidate, out HashSet<int3> set) || set.Count == 0)
                {
                    _dirty.Remove(candidate);
                    RemoveWaterChunk(candidate);
                    continue;
                }

                Vector3 centre = (new Vector3(candidate.x, candidate.y, candidate.z)
                                + Vector3.one * 0.5f) * chunkMetres;
                float distance = (centre - cameraWorldPosition).sqrMagnitude;
                if (!hasBest || distance < bestDistance)
                {
                    if (hasBest) RequeueDirty(best);
                    best = candidate;
                    bestDistance = distance;
                    hasBest = true;
                }
                else
                {
                    RequeueDirty(candidate);
                }

                if (Time.realtimeSinceStartupAsDouble >= deadline) break;
            }

            if (!hasBest) return false;
            _dirty.Remove(best);
            _vertices.Clear();
            _indices.Clear();
            _build = new BuildState
            {
                Active = true,
                Coordinate = best,
                Cursor = 0,
                SourceVersion = _desiredVersions.TryGetValue(best, out ulong version)
                    ? version : 0
            };
            return true;
        }

'''
s = s[:start] + begin + s[end:]

# Replace StepBuild to scan deterministic 16^3 possible blocks rather than copying HashSet.
start = s.index('        private bool StepBuild(')
end = s.index('        private void FinishCpuBuild()', start)
step = r'''        private bool StepBuild(IRegionReadSource storage, float voxelSize, double deadline)
        {
            if (!_waterBricks.TryGetValue(_build.Coordinate, out HashSet<int3> set)
                || set.Count == 0)
                return true;

            const int totalBrickSlots = BricksPerAxis * BricksPerAxis * BricksPerAxis;
            int sliceEnd = math.min(totalBrickSlots, _build.Cursor + BricksPerSlice);
            RegionReadView cachedRegion = default;
            while (_build.Cursor < sliceEnd)
            {
                int linear = _build.Cursor++;
                int x = linear % BricksPerAxis;
                int y = (linear / BricksPerAxis) % BricksPerAxis;
                int z = linear / (BricksPerAxis * BricksPerAxis);
                int3 worldBrick = _build.Coordinate * BricksPerAxis + new int3(x, y, z);
                if (set.Contains(worldBrick)
                    && TryLoadBrickMaterials(storage, worldBrick, ref cachedRegion)
                    && LoadedBrickContainsWater())
                    EmitBrick(storage, worldBrick * E, voxelSize);

                if (_build.Cursor < totalBrickSlots
                    && Time.realtimeSinceStartupAsDouble >= deadline)
                    return false;
            }
            return _build.Cursor >= totalBrickSlots;
        }

'''
s = s[:start] + step + s[end:]

# Remove all buildBricks references left by ResetBuildOutput/teardown.
s = s.replace('            _buildBricks.Clear();\n', '')

# Dirty queue semantics.
s = once(s,
'''        private void Invalidate(int3 chunk)
        {
            _desiredVersions[chunk] = ++_versionCounter;
            _dirty.Add(chunk);
        }''',
'''        private void Invalidate(int3 chunk)
        {
            _desiredVersions[chunk] = ++_versionCounter;
            MarkDirty(chunk);
        }

        private void MarkDirty(int3 chunk)
        {
            _dirty.Add(chunk);
            RequeueDirty(chunk);
        }

        private void RequeueDirty(int3 chunk)
        {
            if (!_dirty.Contains(chunk) || !_queuedDirty.Add(chunk)) return;
            _dirtyQueue.Enqueue(chunk);
        }''', 'water dirty queue helpers')

# Replace all-resident scan and temp List with bounded queue helpers + entry pool.
start = s.index('        private void DropNoLongerResident(IRegionReadSource storage)')
end = s.index('        public void Dispose()', start)
maintenance = r'''        private void TrackResidentChunk(int3 chunk)
        {
            if (!_queuedResidency.Add(chunk)) return;
            _residencyQueue.Enqueue(chunk);
        }

        private void StepResidencyPrune(IRegionReadSource storage)
        {
            int checks = math.min(ResidencyChecksPerPrepare, _residencyQueue.Count);
            for (int i = 0; i < checks; i++)
            {
                int3 chunk = _residencyQueue.Dequeue();
                _queuedResidency.Remove(chunk);
                if (!_waterBricks.ContainsKey(chunk)) continue;
                if (storage.IsRegionResident(ChunkRegion(chunk)))
                {
                    TrackResidentChunk(chunk);
                    continue;
                }
                RemoveWaterChunk(chunk);
            }
        }

        private void RemoveWaterChunk(int3 chunk)
        {
            _waterBricks.Remove(chunk);
            _dirty.Remove(chunk);
            _queuedDirty.Remove(chunk);
            _queuedResidency.Remove(chunk);
            _desiredVersions.Remove(chunk);
            if (_entries.TryGetValue(chunk, out Entry entry))
            {
                _entries.Remove(chunk);
                ReleaseEntry(entry);
            }
            if (_build.Active && _build.Coordinate.Equals(chunk))
            {
                if (_entries.TryGetValue(chunk, out Entry pending)) pending.CancelUpload();
                ResetBuildOutput();
            }
        }

        private Entry AcquireEntry(int3 coordinate)
        {
            if (_entryPool.Count == 0) return new Entry(coordinate, _geometryArena);
            Entry entry = _entryPool.Pop();
            entry.Reinitialize(coordinate);
            return entry;
        }

        private void ReleaseEntry(Entry entry)
        {
            if (entry == null) return;
            entry.Dispose();
            _entryPool.Push(entry);
        }

        internal bool TryEvictOneForArenaPressure(Camera camera, float voxelSize)
        {
            if (_entries.Count == 0) return false;
            if (camera != null) GeometryUtility.CalculateFrustumPlanes(camera, _frustumPlanes);
            Vector3 cameraPosition = camera != null ? camera.transform.position : Vector3.zero;
            int3 victim = default;
            float farthest = -1f;
            float chunkMetres = VoxelsPerAxis * voxelSize;
            foreach (var pair in _entries)
            {
                if (_build.Active && pair.Key.Equals(_build.Coordinate)) continue;
                if (camera != null
                    && GeometryUtility.TestPlanesAABB(_frustumPlanes, pair.Value.WorldBounds(voxelSize)))
                    continue;
                Vector3 centre = (new Vector3(pair.Key.x, pair.Key.y, pair.Key.z)
                                + Vector3.one * 0.5f) * chunkMetres;
                float distance = (centre - cameraPosition).sqrMagnitude;
                if (distance <= farthest) continue;
                farthest = distance;
                victim = pair.Key;
            }
            if (farthest < 0f) return false;
            RemoveWaterChunk(victim);
            return true;
        }

'''
s = s[:start] + maintenance + s[end:]

s = once(s,
'''            foreach (Entry entry in _entries.Values) entry.Dispose();
            _entries.Clear();
            _waterBricks.Clear();
            _dirty.Clear();
            _desiredVersions.Clear();
            _visible.Clear();''',
'''            foreach (Entry entry in _entries.Values) entry.Dispose();
            foreach (Entry entry in _entryPool) entry.Dispose();
            _entries.Clear();
            _entryPool.Clear();
            _waterBricks.Clear();
            _dirty.Clear();
            _dirtyQueue.Clear();
            _queuedDirty.Clear();
            _residencyQueue.Clear();
            _queuedResidency.Clear();
            _regionInvalidationQueue.Clear();
            _queuedRegionInvalidations.Clear();
            _rescanRegionInvalidations.Clear();
            _desiredVersions.Clear();
            _visible.Clear();''', 'water maintenance dispose')

path.write_text(s)

# Scheduler: arena pressure is backpressure for water too.
scheduler_path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/VoxelSurfaceScheduler.cs')
q = scheduler_path.read_text()
q = once(q,
'''        public int LastFrameWaterUploadedBytes { get; private set; }
''',
'''        public int LastFrameWaterUploadedBytes { get; private set; }
        private ulong _observedWaterArenaAllocationFailures;
''', 'water arena pressure scheduler state')
q = once(q,
'''                LastFrameWaterUploadedBytes = waterUploadedBytes;
            }
            _workerPrepareTiming.Add(workerPrepareMs);''',
'''                LastFrameWaterUploadedBytes = waterUploadedBytes;
            }
            ulong waterArenaFailures = _water.ArenaAllocationFailures;
            if (waterArenaFailures > _observedWaterArenaAllocationFailures)
            {
                _observedWaterArenaAllocationFailures = waterArenaFailures;
                _water.TryEvictOneForArenaPressure(camera, voxelSize);
            }
            _workerPrepareTiming.Add(workerPrepareMs);''', 'water arena pressure policy')
scheduler_path.write_text(q)

# Architecture guard.
test_path = Path('Assets/Tests/EditMode/GeometryPipelineArchitectureTests.cs')
t = test_path.read_text()
if 'WaterMaintenanceAndBuildAdmissionAreIncremental' not in t:
    insert = r'''

        [Test]
        public void WaterMaintenanceAndBuildAdmissionAreIncremental()
        {
            string water = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "CpuWaterSurfaceChunkCache.cs"));
            StringAssert.Contains("BuildSelectionCandidatesPerPrepare", water);
            StringAssert.Contains("RegionInvalidationCandidatesPerPrepare", water);
            StringAssert.Contains("ResidencyChecksPerPrepare", water);
            StringAssert.Contains("private readonly Queue<int3> _dirtyQueue", water);
            StringAssert.Contains("private readonly Queue<int3> _residencyQueue", water);
            StringAssert.Contains("private readonly Queue<int3> _regionInvalidationQueue", water);
            StringAssert.DoesNotContain("private readonly List<int3> _buildBricks", water);
            StringAssert.DoesNotContain("foreach (int3 candidate in _dirty)", water);
            StringAssert.DoesNotContain("private void DropNoLongerResident", water);
            StringAssert.DoesNotContain("List<int3> gone", water);
        }
'''
    marker = '\n    }\n}'
    pos = t.rfind(marker)
    if pos < 0:
        raise SystemExit('architecture tests closing marker missing')
    t = t[:pos] + insert + t[pos:]
test_path.write_text(t)

# Progress doc.
doc_path = Path('docs/ASYNC_GEOMETRY_PIPELINE.md')
d = doc_path.read_text()
d = d.replace('- [ ] Apply the same async snapshot/result/publication contract to water geometry.\n',
'''- [ ] Apply the same async snapshot/result/publication contract to water geometry.
  - [x] Bound water dirty selection, brick traversal, region invalidation, residency pruning, arena pressure, and GPU publication.
  - [ ] Move water extraction itself to owned immutable snapshot + Burst jobs.
''', 1)
doc_path.write_text(d)

water = path.read_text()
assert 'foreach (int3 candidate in _dirty)' not in water
assert 'private readonly List<int3> _buildBricks' not in water
assert 'private void DropNoLongerResident' not in water
assert 'List<int3> gone' not in water
assert 'BuildSelectionCandidatesPerPrepare' in water
assert 'RegionInvalidationCandidatesPerPrepare' in water
assert 'ResidencyChecksPerPrepare' in water
