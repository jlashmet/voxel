from pathlib import Path


def once(text, old, new, label):
    n = text.count(old)
    if n != 1:
        raise SystemExit(f'{label}: expected one match, found {n}')
    return text.replace(old, new, 1)


path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs')
s = path.read_text()

s = once(s,
'''        private readonly HashSet<int3> _dirty = new();
        private readonly Dictionary<int3, ulong> _desiredVersions = new();''',
'''        private readonly HashSet<int3> _dirty = new();
        // Dirty work is also kept in a persistent FIFO. The HashSet remains the authoritative
        // membership/coalescing structure; the queue gives build admission bounded incremental
        // traversal instead of rescanning every dirty chunk whenever one workspace becomes free.
        private readonly Queue<int3> _dirtyQueue = new();
        private readonly HashSet<int3> _queuedDirty = new();
        private const int BuildSelectionCandidatesPerSlice = 64;
        private readonly Dictionary<int3, ulong> _desiredVersions = new();''', 'dirty queue fields')

s = once(s,
'''                    bool selected = BeginNearestBuild(camera, voxelSize);''',
'''                    bool selected = BeginNearestBuild(camera, voxelSize, deadline);''',
'bounded build selection call')

start = s.index('        private bool BeginNearestBuild(Camera camera, float voxelSize)')
end = s.index('        private bool OwnsShard(int3 chunk)', start)
replacement = r'''        private bool BeginNearestBuild(Camera camera, float voxelSize,
                                       double deadlineSeconds)
        {
            if (_dirty.Count == 0 || _dirtyQueue.Count == 0
                || Time.realtimeSinceStartupAsDouble >= deadlineSeconds)
                return false;

            int3 best = default;
            bool hasBest = false;
            float bestScore = float.PositiveInfinity;
            float chunkMetres = VoxelsPerAxis * voxelSize;
            Vector3 cameraWorldPosition = camera.transform.position;
            GeometryUtility.CalculateFrustumPlanes(camera, _frustumPlanes);

            int candidates = math.min(BuildSelectionCandidatesPerSlice, _dirtyQueue.Count);
            for (int i = 0; i < candidates; i++)
            {
                int3 candidate = _dirtyQueue.Dequeue();
                _queuedDirty.Remove(candidate);
                if (!_dirty.Contains(candidate)) continue; // stale queue record

                Bounds bounds = ChunkWorldBounds(candidate, voxelSize);
                if (!WithinRingBand(bounds, cameraWorldPosition))
                {
                    RequeueDirty(candidate);
                    continue;
                }

                Vector3 centre = (new Vector3(candidate.x, candidate.y, candidate.z)
                                + Vector3.one * 0.5f) * chunkMetres;
                float distance = (centre - cameraWorldPosition).sqrMagnitude;
                float score = GeometryUtility.TestPlanesAABB(_frustumPlanes, bounds)
                    ? distance : distance + 1_000_000_000f;
                if (!hasBest || score < bestScore)
                {
                    if (hasBest) RequeueDirty(best);
                    bestScore = score;
                    best = candidate;
                    hasBest = true;
                }
                else
                {
                    RequeueDirty(candidate);
                }

                // Score checks are cheap, but a destruction burst can enqueue thousands. The
                // frame contract wins over exact global nearest ordering; later slices continue
                // from the queue tail and converge without a scan spike.
                if (Time.realtimeSinceStartupAsDouble >= deadlineSeconds) break;
            }

            if (!hasBest) return false;
            _dirty.Remove(best);
            _vertices.Clear();
            _indices.Clear();
            _transitionFace = -1;
            _transitionSampleCursor = 0;
            _transitionResultPending = false;
            _resultAppendStage = 0;
            _topologyAppendVertexCursor = 0;
            _topologyAppendIndexCursor = 0;
            _facetedAppendVertexCursor = 0;
            _facetedAppendIndexCursor = 0;
            _transitionAppendVertexCursor = 0;
            _transitionAppendIndexCursor = 0;
            _build = new BuildState
            {
                Active = true, Coordinate = best, Phase = 0, Cursor = 0,
                SourceVersion = _desiredVersions.TryGetValue(best, out ulong version) ? version : 0,
                SurfaceCatalogueVersion = _surfaceCatalogue.Version,
                SurfaceCatalogueHash = _surfaceCatalogue.CatalogueHash,
                CoatingCatalogueVersion = _coatingCatalogue.Version,
                CoatingCatalogueHash = _coatingCatalogue.CatalogueHash,
                BuildStartSeconds = Time.realtimeSinceStartupAsDouble
            };
            if (_queuedAtSeconds.TryGetValue(best, out double queuedAt))
                _queueLatencyTiming.Add(ElapsedMs(queuedAt));
            return true;
        }

        private void Invalidate(int3 chunk)
        {
            _emptyVersions.Remove(chunk);
            _desiredVersions[chunk] = ++_versionCounter;
            MarkDirty(chunk);
        }

        private void MarkDirty(int3 chunk)
        {
            if (_dirty.Add(chunk))
                _queuedAtSeconds[chunk] = Time.realtimeSinceStartupAsDouble;
            RequeueDirty(chunk);
        }

        private void RequeueDirty(int3 chunk)
        {
            if (!_dirty.Contains(chunk) || !_queuedDirty.Add(chunk)) return;
            _dirtyQueue.Enqueue(chunk);
        }

'''
s = s[:start] + replacement + s[end:]

# Removal clears authoritative queue membership. A stale FIFO record can remain and is skipped
# later without scanning/removing from the middle of Queue<T>.
s = once(s,
'''            _known.Remove(chunk);
            _dirty.Remove(chunk);
            _desiredVersions.Remove(chunk);''',
'''            _known.Remove(chunk);
            _dirty.Remove(chunk);
            _queuedDirty.Remove(chunk);
            _desiredVersions.Remove(chunk);''', 'remove queued membership')

# Arena/capacity eviction requeues without inventing a new source generation.
s = s.replace('            _dirty.Add(victim);\n            return true;\n',
              '            MarkDirty(victim);\n            return true;\n', 1)

old_capacity = '''        private void EnforceCapacity(Camera camera, float voxelSize)
        {
            while (_entries.Count >= MaxResidentChunks && _dirty.Count > 0)
            {
                int3 victim = default;
                float farthest = -1f;
                Vector3 cameraPosition = camera != null ? camera.transform.position : Vector3.zero;
                float chunkMetres = VoxelsPerAxis * voxelSize;
                if (camera != null) GeometryUtility.CalculateFrustumPlanes(camera, _frustumPlanes);

                foreach (var pair in _entries)
                {
                    // A capacity limit may delay new work, but may never create a visible hole.
                    if (camera != null && GeometryUtility.TestPlanesAABB(
                            _frustumPlanes, ChunkWorldBounds(pair.Key, voxelSize)))
                        continue;
                    Vector3 centre = (new Vector3(pair.Key.x, pair.Key.y, pair.Key.z)
                                    + Vector3.one * 0.5f) * chunkMetres;
                    float distance = (centre - cameraPosition).sqrMagnitude;
                    if (distance <= farthest) continue;
                    farthest = distance;
                    victim = pair.Key;
                }

                if (farthest < 0f)
                {
                    CapacityPressureCount++;
                    break;
                }
                if (_entries.TryGetValue(victim, out Entry entry)) entry.Dispose();
                _entries.Remove(victim);
                // Keep it known and queued. If the camera returns, nearest-first admission can
                // rebuild it; an evicted chunk must never become a permanent silent hole.
                _dirty.Add(victim);
            }
        }'''
new_capacity = '''        private void EnforceCapacity(Camera camera, float voxelSize)
        {
            if (_entries.Count < MaxResidentChunks || _dirty.Count == 0) return;

            int3 victim = default;
            float farthest = -1f;
            Vector3 cameraPosition = camera != null ? camera.transform.position : Vector3.zero;
            float chunkMetres = VoxelsPerAxis * voxelSize;
            if (camera != null) GeometryUtility.CalculateFrustumPlanes(camera, _frustumPlanes);

            foreach (var pair in _entries)
            {
                // Capacity pressure is also bounded: at most one offscreen lease retires from
                // this workspace per Prepare call. Repeated eviction loops turn a cache miss into
                // a frame spike exactly when streaming is already under pressure.
                if (camera != null && GeometryUtility.TestPlanesAABB(
                        _frustumPlanes, ChunkWorldBounds(pair.Key, voxelSize)))
                    continue;
                Vector3 centre = (new Vector3(pair.Key.x, pair.Key.y, pair.Key.z)
                                + Vector3.one * 0.5f) * chunkMetres;
                float distance = (centre - cameraPosition).sqrMagnitude;
                if (distance <= farthest) continue;
                farthest = distance;
                victim = pair.Key;
            }

            if (farthest < 0f)
            {
                CapacityPressureCount++;
                return;
            }
            if (_entries.TryGetValue(victim, out Entry entry)) entry.Dispose();
            _entries.Remove(victim);
            MarkDirty(victim);
        }'''
s = once(s, old_capacity, new_capacity, 'bounded capacity eviction')

path.write_text(s)

# Source-level guard for bounded admission.
test_path = Path('Assets/Tests/EditMode/GeometryPipelineArchitectureTests.cs')
t = test_path.read_text()
if 'DirtyBuildSelectionIsIncremental' in t:
    raise SystemExit('dirty selection guard already exists')
insert = r'''

        [Test]
        public void DirtyBuildSelectionIsIncremental()
        {
            string cache = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "CpuTransvoxelChunkCache.cs"));
            StringAssert.Contains("BuildSelectionCandidatesPerSlice", cache);
            StringAssert.Contains("private readonly Queue<int3> _dirtyQueue", cache);
            StringAssert.Contains("BeginNearestBuild(camera, voxelSize, deadline)", cache);
            StringAssert.DoesNotContain("foreach (int3 candidate in _dirty)", cache);
            StringAssert.DoesNotContain("while (_entries.Count >= MaxResidentChunks", cache);
        }
'''
marker = '\n    }\n}'
pos = t.rfind(marker)
if pos < 0:
    raise SystemExit('architecture test closing marker missing')
t = t[:pos] + insert + t[pos:]
test_path.write_text(t)

cache = path.read_text()
assert 'private readonly Queue<int3> _dirtyQueue' in cache
assert 'foreach (int3 candidate in _dirty)' not in cache
assert 'BeginNearestBuild(camera, voxelSize, deadline)' in cache
assert 'while (_entries.Count >= MaxResidentChunks' not in cache
