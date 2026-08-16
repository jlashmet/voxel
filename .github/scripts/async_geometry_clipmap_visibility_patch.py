from pathlib import Path


def once(text, old, new, label):
    n = text.count(old)
    if n != 1:
        raise SystemExit(f'{label}: expected one match, found {n}')
    return text.replace(old, new, 1)


cache_path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs')
s = cache_path.read_text()

start = s.index('        public IReadOnlyList<Entry> CollectVisible(Camera camera, float voxelSize, int frame)')
end = s.index('        private bool BeginNearestBuild(', start)
visibility_impl = r'''        public void BeginVisibilityCollection()
        {
            _visible.Clear();
            MissingVisibleCount = 0;
        }

        /// <summary>
        /// Evaluates one clipmap coordinate already routed to this shard. Visibility traversal is
        /// driven by the bounded camera-centred ring grid, never by the lifetime size of _known.
        /// </summary>
        public void CollectVisibleCoordinate(int3 coordinate, Plane[] frustumPlanes,
                                             Vector3 cameraPosition, float voxelSize, int frame)
        {
            if (!_known.Contains(coordinate)) return;

            Bounds bounds = ChunkWorldBounds(coordinate, voxelSize);
            if (!WithinRingBand(bounds, cameraPosition)) return;
            if (!GeometryUtility.TestPlanesAABB(frustumPlanes, bounds)) return;

            if (!_entries.TryGetValue(coordinate, out Entry entry) || !entry.Ready)
            {
                // A known-empty chunk is a completed build with nothing to draw, not a hole.
                if (!_emptyVersions.ContainsKey(coordinate)) MissingVisibleCount++;
                return;
            }
            if (entry.IndexCount == 0) return;
            entry.LastUsedFrame = frame;
            _visible.Add(entry);
        }

        /// <summary>
        /// Compatibility entry point for focused tests/tools. Production scheduling performs one
        /// ring traversal in VoxelSurfaceScheduler and routes coordinates directly to shards.
        /// This fallback is still bounded by the ring's configured view distance.
        /// </summary>
        public IReadOnlyList<Entry> CollectVisible(Camera camera, float voxelSize, int frame)
        {
            BeginVisibilityCollection();
            if (camera == null) return _visible;

            GeometryUtility.CalculateFrustumPlanes(camera, _frustumPlanes);
            Vector3 cameraPosition = camera.transform.position;
            float chunkMetres = VoxelsPerAxis * voxelSize;
            int radius = Mathf.CeilToInt(MaxViewDistanceMetres / chunkMetres) + 1;
            int3 centre = new(
                Mathf.FloorToInt(cameraPosition.x / chunkMetres),
                Mathf.FloorToInt(cameraPosition.y / chunkMetres),
                Mathf.FloorToInt(cameraPosition.z / chunkMetres));

            for (int z = -radius; z <= radius; z++)
            for (int y = -radius; y <= radius; y++)
            for (int x = -radius; x <= radius; x++)
            {
                int3 coordinate = centre + new int3(x, y, z);
                if (!OwnsShard(coordinate)) continue;
                CollectVisibleCoordinate(coordinate, _frustumPlanes, cameraPosition,
                                         voxelSize, frame);
            }
            return _visible;
        }

'''
s = s[:start] + visibility_impl + s[end:]

s = once(s,
'''        private bool OwnsShard(int3 chunk)
        {
            int count = math.max(1, ShardCount);
            uint hash = math.hash(chunk);
            return (int)(hash % (uint)count) == math.clamp(ShardIndex, 0, count - 1);
        }''',
'''        public static int ShardForChunk(int3 chunk, int shardCount)
        {
            int count = math.max(1, shardCount);
            return (int)(math.hash(chunk) % (uint)count);
        }

        private bool OwnsShard(int3 chunk) =>
            ShardForChunk(chunk, ShardCount) == math.clamp(ShardIndex, 0, math.max(1, ShardCount) - 1);''',
'shard routing helper')
cache_path.write_text(s)


scheduler_path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/VoxelSurfaceScheduler.cs')
q = scheduler_path.read_text()
q = once(q,
'''        private readonly List<CpuTransvoxelChunkCache.Entry> _visibleSolids = new();
        private readonly VoxelTimingWindow _prepareTiming = new();''',
'''        private readonly List<CpuTransvoxelChunkCache.Entry> _visibleSolids = new();
        private readonly Plane[] _visibilityFrustumPlanes = new Plane[6];
        private int _lastVisibilityCandidateChecks;
        private readonly VoxelTimingWindow _prepareTiming = new();''', 'scheduler visibility state')

q = once(q,
'''        public int SolidBuildWorkspaceCount => _allWorkers.Length;''',
'''        public int SolidBuildWorkspaceCount => _allWorkers.Length;
        public int LastVisibilityCandidateChecks => _lastVisibilityCandidateChecks;''',
'visibility diagnostic')

start = q.index('        private void CollectVisibility(Camera camera, float voxelSize, int frame)')
end = q.index('        private void EnqueueSurfaceDiscovery(', start)
collect_impl = r'''        private void CollectVisibility(Camera camera, float voxelSize, int frame)
        {
            _visibleSolids.Clear();
            _lastVisibilityCandidateChecks = 0;
            double visibilityStart = Time.realtimeSinceStartupAsDouble;
            using (s_VisibilityMarker.Auto())
            {
                if (camera != null)
                {
                    GeometryUtility.CalculateFrustumPlanes(camera, _visibilityFrustumPlanes);
                    Vector3 cameraPosition = camera.transform.position;
                    for (int r = 0; r < _rings.Length; r++)
                    {
                        SurfaceRing ring = _rings[r];
                        for (int w = 0; w < ring.Workers.Length; w++)
                            ring.Workers[w].BeginVisibilityCollection();

                        float chunkMetres = CpuTransvoxelChunkCache.CellsPerAxis
                                          * ring.SourceStep * voxelSize;
                        int radius = Mathf.CeilToInt(ring.OuterRadiusMetres / chunkMetres) + 1;
                        int3 centre = new(
                            Mathf.FloorToInt(cameraPosition.x / chunkMetres),
                            Mathf.FloorToInt(cameraPosition.y / chunkMetres),
                            Mathf.FloorToInt(cameraPosition.z / chunkMetres));

                        // One bounded clipmap-coordinate walk per ring. Sharding chooses the
                        // workspace in O(1); it no longer causes each workspace to rescan the
                        // same coordinate volume or the lifetime-sized _known set.
                        for (int z = -radius; z <= radius; z++)
                        for (int y = -radius; y <= radius; y++)
                        for (int x = -radius; x <= radius; x++)
                        {
                            int3 coordinate = centre + new int3(x, y, z);
                            int shard = CpuTransvoxelChunkCache.ShardForChunk(
                                coordinate, ring.Workers.Length);
                            ring.Workers[shard].CollectVisibleCoordinate(
                                coordinate, _visibilityFrustumPlanes, cameraPosition,
                                voxelSize, frame);
                            _lastVisibilityCandidateChecks++;
                        }

                        for (int w = 0; w < ring.Workers.Length; w++)
                        {
                            IReadOnlyList<CpuTransvoxelChunkCache.Entry> visible =
                                ring.Workers[w].Visible;
                            for (int i = 0; i < visible.Count; i++)
                                _visibleSolids.Add(visible[i]);
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < _allWorkers.Length; i++)
                        _allWorkers[i].BeginVisibilityCollection();
                }

                _water.CollectVisible(camera, voxelSize);
            }
            _visibilityTiming.Add(ElapsedMs(visibilityStart));
        }

'''
q = q[:start] + collect_impl + q[end:]
scheduler_path.write_text(q)


# Guard the production visibility contract.
test_path = Path('Assets/Tests/EditMode/GeometryPipelineArchitectureTests.cs')
t = test_path.read_text()
if 'SolidVisibilityTraversesBoundedClipmapCoordinatesOncePerRing' in t:
    raise SystemExit('clipmap visibility test already exists')
insert = r'''

        [Test]
        public void SolidVisibilityTraversesBoundedClipmapCoordinatesOncePerRing()
        {
            string cache = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "CpuTransvoxelChunkCache.cs"));
            string scheduler = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "VoxelSurfaceScheduler.cs"));
            int collect = scheduler.IndexOf("private void CollectVisibility", StringComparison.Ordinal);
            int collectEnd = scheduler.IndexOf("private void EnqueueSurfaceDiscovery", collect,
                                               StringComparison.Ordinal);
            Assert.GreaterOrEqual(collect, 0);
            Assert.Greater(collectEnd, collect);
            string productionVisibility = scheduler.Substring(collect, collectEnd - collect);
            StringAssert.Contains("for (int r = 0; r < _rings.Length; r++)", productionVisibility);
            StringAssert.Contains("ShardForChunk", productionVisibility);
            StringAssert.Contains("CollectVisibleCoordinate", productionVisibility);
            StringAssert.DoesNotContain("_allWorkers[i].CollectVisible", productionVisibility);

            int cacheCollect = cache.IndexOf("public IReadOnlyList<Entry> CollectVisible(",
                                             StringComparison.Ordinal);
            int cacheCollectEnd = cache.IndexOf("private bool BeginNearestBuild", cacheCollect,
                                                StringComparison.Ordinal);
            Assert.GreaterOrEqual(cacheCollect, 0);
            StringAssert.DoesNotContain("foreach (int3 coordinate in _known)",
                cache.Substring(cacheCollect, cacheCollectEnd - cacheCollect));
        }
'''
marker = '\n    }\n}'
pos = t.rfind(marker)
if pos < 0:
    raise SystemExit('architecture test closing marker missing')
t = t[:pos] + insert + t[pos:]
test_path.write_text(t)

cache = cache_path.read_text()
scheduler = scheduler_path.read_text()
assert 'foreach (int3 coordinate in _known)' not in cache
assert 'ShardForChunk' in scheduler
assert '_allWorkers[i].CollectVisible' not in scheduler[scheduler.index('private void CollectVisibility'):scheduler.index('private void EnqueueSurfaceDiscovery')]
