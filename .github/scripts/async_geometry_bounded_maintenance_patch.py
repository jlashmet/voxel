from pathlib import Path


def once(text, old, new, label):
    n = text.count(old)
    if n != 1:
        raise SystemExit(f'{label}: expected one match, found {n}')
    return text.replace(old, new, 1)


# -----------------------------------------------------------------------------
# Shared indirect-args scratch: no uint[4] allocation per resident Entry.
# -----------------------------------------------------------------------------
arena_path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/SurfaceGeometryArena.cs')
a = arena_path.read_text()
a = once(a,
'''        private readonly RangeAllocator _vertexRanges;
        private readonly RangeAllocator _indexRanges;
        private readonly RangeAllocator _argsRanges;
        private bool _disposed;''',
'''        private readonly RangeAllocator _vertexRanges;
        private readonly RangeAllocator _indexRanges;
        private readonly RangeAllocator _argsRanges;
        private NativeArray<uint> _argsScratch;
        private bool _disposed;''', 'arena args scratch field')
a = once(a,
'''            _vertexRanges = new RangeAllocator(vertexCapacity, 4096);
            _indexRanges = new RangeAllocator(indexCapacity, 4096);
            _argsRanges = new RangeAllocator(argsRecordCapacity * ArgsWordsPerDraw, 4096);

            ComputeBuffer vertices = null;''',
'''            _vertexRanges = new RangeAllocator(vertexCapacity, 4096);
            _indexRanges = new RangeAllocator(indexCapacity, 4096);
            _argsRanges = new RangeAllocator(argsRecordCapacity * ArgsWordsPerDraw, 4096);
            _argsScratch = new NativeArray<uint>(ArgsWordsPerDraw, Allocator.Persistent,
                                                 NativeArrayOptions.UninitializedMemory);

            ComputeBuffer vertices = null;''', 'arena args scratch allocation')
a = once(a,
'''        public void UploadArgs(uint[] source, in SurfaceGeometryLease lease)
        {
            Args.SetData(source, 0, lease.ArgsWordStart, ArgsWordsPerDraw);
        }''',
'''        public void UploadArgs(uint indexCount, in SurfaceGeometryLease lease)
        {
            _argsScratch[0] = indexCount;
            _argsScratch[1] = 1u;
            _argsScratch[2] = 0u;
            _argsScratch[3] = 0u;
            Args.SetData(_argsScratch, 0, lease.ArgsWordStart, ArgsWordsPerDraw);
        }''', 'arena args upload')
a = once(a,
'''            Vertices?.Release();
            Indices?.Release();
            Args?.Release();
        }''',
'''            Vertices?.Release();
            Indices?.Release();
            Args?.Release();
            if (_argsScratch.IsCreated) _argsScratch.Dispose();
        }''', 'arena args scratch dispose')
arena_path.write_text(a)


# -----------------------------------------------------------------------------
# Chunk-cache maintenance: bounded residency + bounded full-region invalidation.
# -----------------------------------------------------------------------------
cache_path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs')
s = cache_path.read_text()
s = once(s,
'''            private int _stagingVertexCursor;
            private int _stagingIndexCursor;
            private readonly uint[] _indirectArgs = new uint[4];
            internal bool WaitingForArena { get; private set; }''',
'''            private int _stagingVertexCursor;
            private int _stagingIndexCursor;
            internal bool WaitingForArena { get; private set; }''', 'remove per-entry args array')
s = once(s,
'''                _indirectArgs[0] = (uint)indices.Length;
                _indirectArgs[1] = 1u;
                _indirectArgs[2] = 0u;
                _indirectArgs[3] = 0u;
                _arena.UploadArgs(_indirectArgs, in _stagingLease);''',
'''                _arena.UploadArgs((uint)indices.Length, in _stagingLease);''', 'shared args upload call')

s = once(s,
'''        private readonly HashSet<int3> _known = new();
        private readonly HashSet<int3> _dirty = new();''',
'''        private readonly HashSet<int3> _known = new();
        // Known-chunk liveness is maintained incrementally. A full HashSet scan in every worker
        // turns residency pressure into O(world-residency) frame work, so each known chunk owns
        // one round-robin queue record instead.
        private readonly Queue<int3> _residencyQueue = new();
        private readonly HashSet<int3> _queuedResidency = new();
        private const int ResidencyChecksPerPrepare = 32;

        // Full-region invalidations (journal overflow, residency publication, atomic world swap)
        // are also incremental. Fine-grained edits continue to use the brick path immediately.
        private readonly Queue<int3> _regionInvalidationQueue = new();
        private readonly HashSet<int3> _queuedRegionInvalidations = new();
        private readonly HashSet<int3> _rescanRegionInvalidations = new();
        private const int RegionInvalidationCandidatesPerPrepare = 64;
        private bool _hasActiveRegionInvalidation;
        private int3 _activeRegionInvalidation;
        private int3 _activeRegionMinChunk;
        private int3 _activeRegionChunkCounts;
        private int _activeRegionCandidateCursor;

        private readonly HashSet<int3> _dirty = new();''', 'maintenance queue fields')

s = once(s,
'''                    if (!OwnsShard(chunk)) continue;
                    _known.Add(chunk);
                    Invalidate(chunk);''',
'''                    if (!OwnsShard(chunk)) continue;
                    TrackKnown(chunk);
                    Invalidate(chunk);''', 'track known chunks')

start = s.index('        public void InvalidateDirtyRegions(HashSet<int3> dirtyRegions)')
end = s.index('        public void Prepare(IRegionReadSource source, in MaterialPaletteView palette,', start)
region_impl = r'''        public void InvalidateDirtyRegions(HashSet<int3> dirtyRegions)
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
            while (remaining > 0)
            {
                if (!_hasActiveRegionInvalidation)
                {
                    if (_regionInvalidationQueue.Count == 0) return;
                    _activeRegionInvalidation = _regionInvalidationQueue.Dequeue();
                    _hasActiveRegionInvalidation = true;
                    _activeRegionCandidateCursor = 0;

                    int halo = Padding * SourceStep;
                    int3 regionMin = _activeRegionInvalidation * VoxelGrid.RegionVoxelEdge;
                    int3 regionMax = regionMin + VoxelGrid.RegionVoxelEdge;
                    _activeRegionMinChunk = new int3(
                        FloorDiv(regionMin.x - halo, VoxelsPerAxis),
                        FloorDiv(regionMin.y - halo, VoxelsPerAxis),
                        FloorDiv(regionMin.z - halo, VoxelsPerAxis));
                    int3 maxChunk = new int3(
                        FloorDiv(regionMax.x + halo - 1, VoxelsPerAxis),
                        FloorDiv(regionMax.y + halo - 1, VoxelsPerAxis),
                        FloorDiv(regionMax.z + halo - 1, VoxelsPerAxis));
                    _activeRegionChunkCounts = maxChunk - _activeRegionMinChunk + 1;
                }

                int total = _activeRegionChunkCounts.x
                          * _activeRegionChunkCounts.y
                          * _activeRegionChunkCounts.z;
                while (remaining > 0 && _activeRegionCandidateCursor < total)
                {
                    int linear = _activeRegionCandidateCursor++;
                    int x = linear % _activeRegionChunkCounts.x;
                    int y = (linear / _activeRegionChunkCounts.x) % _activeRegionChunkCounts.y;
                    int z = linear / (_activeRegionChunkCounts.x * _activeRegionChunkCounts.y);
                    int3 chunk = _activeRegionMinChunk + new int3(x, y, z);
                    remaining--;
                    if (!OwnsShard(chunk) || !_known.Contains(chunk)) continue;
                    if (ChunkOverlapsRegion(chunk, _activeRegionInvalidation,
                                            VoxelsPerAxis, SourceStep))
                        Invalidate(chunk);
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
        }

'''
s = s[:start] + region_impl + s[end:]

s = once(s,
'''            sectionStart = Time.realtimeSinceStartupAsDouble;
            DropNoLongerResident(source);
            _residencyPruneTiming.Add(ElapsedMs(sectionStart));''',
'''            sectionStart = Time.realtimeSinceStartupAsDouble;
            StepResidencyPrune(source);
            _residencyPruneTiming.Add(ElapsedMs(sectionStart));''', 'bounded residency call')

start = s.index('        private void DropNoLongerResident(IRegionReadSource source)')
end = s.index('        /// <summary>\n        /// Whether any region the chunk overlaps is still resident.', start)
residency_impl = r'''        private void TrackKnown(int3 chunk)
        {
            if (_known.Add(chunk)) RequeueResidency(chunk);
        }

        private void RequeueResidency(int3 chunk)
        {
            if (!_known.Contains(chunk) || !_queuedResidency.Add(chunk)) return;
            _residencyQueue.Enqueue(chunk);
        }

        private void StepResidencyPrune(IRegionReadSource source)
        {
            int checks = math.min(ResidencyChecksPerPrepare, _residencyQueue.Count);
            for (int i = 0; i < checks; i++)
            {
                int3 chunk = _residencyQueue.Dequeue();
                _queuedResidency.Remove(chunk);
                if (!_known.Contains(chunk)) continue;

                if (AnyOverlappedRegionResident(source, chunk))
                {
                    RequeueResidency(chunk);
                    continue;
                }

                // In-flight geometry is never waited on. If removal is deferred, put the chunk
                // back in the liveness queue and recheck it on a later frame.
                if (!TryRemoveChunk(chunk)) RequeueResidency(chunk);
            }
        }

'''
s = s[:start] + residency_impl + s[end:]

s = once(s,
'''            _known.Remove(chunk);
            _dirty.Remove(chunk);
            _queuedDirty.Remove(chunk);''',
'''            _known.Remove(chunk);
            _queuedResidency.Remove(chunk);
            _dirty.Remove(chunk);
            _queuedDirty.Remove(chunk);''', 'remove residency membership')

cache_path.write_text(s)


# -----------------------------------------------------------------------------
# Render diagnostics: metrics stay rich; formatted per-frame strings become opt-in.
# -----------------------------------------------------------------------------
bridge_path = Path('Assets/VoxelEngine/Rendering/Runtime/RenderFeature/VoxelRenderBridge.cs')
b = bridge_path.read_text()
b = once(b,
'''        public static string LastSurfacePassState { get; internal set; } = "not-recorded";

        public static void ResetSurfacePassDiagnostics''',
'''        public static string LastSurfacePassState { get; internal set; } = "not-recorded";
        /// <summary>
        /// Full human-readable per-frame diagnostic strings allocate. Keep them disabled in
        /// gameplay; structured SurfaceMetrics carries the same data without formatting garbage.
        /// </summary>
        public static bool VerboseSurfaceDiagnostics;

        public static void ResetSurfacePassDiagnostics''', 'verbose diagnostics flag')
bridge_path.write_text(b)

pass_path = Path('Assets/VoxelEngine/Rendering/Runtime/RenderFeature/VoxelRenderPass.cs')
p = pass_path.read_text()
p = once(p,
'''            VoxelRenderBridge.LastSurfacePassState = $"preparing-{camera.cameraType}";''',
'''            VoxelRenderBridge.LastSurfacePassState = VoxelRenderBridge.VerboseSurfaceDiagnostics
                ? $"preparing-{camera.cameraType}" : "preparing";''', 'preparing diagnostic allocation')
old_state = '''            VoxelRenderBridge.LastSurfacePassState =
                $"feature-aware resident={VoxelRenderBridge.SurfaceMetrics.SolidResidentChunks}/"
              + $"{VoxelRenderBridge.SurfaceMetrics.SolidKnownChunks} "
              + $"dirty={VoxelRenderBridge.SurfaceMetrics.SolidDirtyChunks} "
              + $"visible={VoxelRenderBridge.SurfaceMetrics.VisibleSolidChunks} "
              + $"missingVisible={VoxelRenderBridge.SurfaceMetrics.MissingVisibleSolidChunks} "
              + $"jobs={VoxelRenderBridge.SurfaceMetrics.RunningSolidJobs} "
              + $"prepare.p95={VoxelRenderBridge.SurfaceMetrics.SchedulerPrepareTiming.P95Ms:0.00}ms "
              + $"discover.p95={VoxelRenderBridge.SurfaceMetrics.SurfaceDiscoveryTiming.P95Ms:0.00}ms "
              + $"select.p95={VoxelRenderBridge.SurfaceMetrics.BuildSelectionTiming.P95Ms:0.00}ms "
              + $"visibility.p95={VoxelRenderBridge.SurfaceMetrics.VisibilityTiming.P95Ms:0.00}ms "
              + $"queue.p95={VoxelRenderBridge.SurfaceMetrics.QueueLatencyTiming.P95Ms:0.0}ms "
              + $"build.p95={VoxelRenderBridge.SurfaceMetrics.BuildLatencyTiming.P95Ms:0.0}ms "
              + $"snapshot.p95={VoxelRenderBridge.SurfaceMetrics.SnapshotTiming.P95Ms:0.00}ms "
              + $"compact.p95={VoxelRenderBridge.SurfaceMetrics.TopologyCompactTiming.P95Ms:0.00}ms "
              + $"merge.p95={VoxelRenderBridge.SurfaceMetrics.FacetedMergeTiming.P95Ms:0.00}ms "
              + $"upload.p95={VoxelRenderBridge.SurfaceMetrics.UploadTiming.P95Ms:0.00}ms";'''
new_state = '''            if (VoxelRenderBridge.VerboseSurfaceDiagnostics)
            {
                VoxelRenderBridge.LastSurfacePassState =
                    $"feature-aware resident={VoxelRenderBridge.SurfaceMetrics.SolidResidentChunks}/"
                  + $"{VoxelRenderBridge.SurfaceMetrics.SolidKnownChunks} "
                  + $"dirty={VoxelRenderBridge.SurfaceMetrics.SolidDirtyChunks} "
                  + $"visible={VoxelRenderBridge.SurfaceMetrics.VisibleSolidChunks} "
                  + $"missingVisible={VoxelRenderBridge.SurfaceMetrics.MissingVisibleSolidChunks} "
                  + $"jobs={VoxelRenderBridge.SurfaceMetrics.RunningSolidJobs} "
                  + $"prepare.p95={VoxelRenderBridge.SurfaceMetrics.SchedulerPrepareTiming.P95Ms:0.00}ms "
                  + $"discover.p95={VoxelRenderBridge.SurfaceMetrics.SurfaceDiscoveryTiming.P95Ms:0.00}ms "
                  + $"select.p95={VoxelRenderBridge.SurfaceMetrics.BuildSelectionTiming.P95Ms:0.00}ms "
                  + $"visibility.p95={VoxelRenderBridge.SurfaceMetrics.VisibilityTiming.P95Ms:0.00}ms "
                  + $"queue.p95={VoxelRenderBridge.SurfaceMetrics.QueueLatencyTiming.P95Ms:0.0}ms "
                  + $"build.p95={VoxelRenderBridge.SurfaceMetrics.BuildLatencyTiming.P95Ms:0.0}ms "
                  + $"snapshot.p95={VoxelRenderBridge.SurfaceMetrics.SnapshotTiming.P95Ms:0.00}ms "
                  + $"compact.p95={VoxelRenderBridge.SurfaceMetrics.TopologyCompactTiming.P95Ms:0.00}ms "
                  + $"merge.p95={VoxelRenderBridge.SurfaceMetrics.FacetedMergeTiming.P95Ms:0.00}ms "
                  + $"upload.p95={VoxelRenderBridge.SurfaceMetrics.UploadTiming.P95Ms:0.00}ms";
            }
            else
            {
                VoxelRenderBridge.LastSurfacePassState = "feature-aware";
            }'''
p = once(p, old_state, new_state, 'verbose detailed diagnostic allocation')
pass_path.write_text(p)


# -----------------------------------------------------------------------------
# Architecture guards.
# -----------------------------------------------------------------------------
test_path = Path('Assets/Tests/EditMode/GeometryPipelineArchitectureTests.cs')
t = test_path.read_text()
if 'GeometryMaintenanceDoesNotScanAllKnownChunksEachFrame' in t:
    raise SystemExit('bounded maintenance tests already exist')
insert = r'''

        [Test]
        public void GeometryMaintenanceDoesNotScanAllKnownChunksEachFrame()
        {
            string cache = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "CpuTransvoxelChunkCache.cs"));
            StringAssert.Contains("ResidencyChecksPerPrepare", cache);
            StringAssert.Contains("RegionInvalidationCandidatesPerPrepare", cache);
            StringAssert.Contains("private readonly Queue<int3> _residencyQueue", cache);
            StringAssert.Contains("private readonly Queue<int3> _regionInvalidationQueue", cache);
            StringAssert.DoesNotContain("private void DropNoLongerResident", cache);
            StringAssert.DoesNotContain("List<int3> affected", cache);
            StringAssert.DoesNotContain("foreach (int3 chunk in _known)", cache);
        }

        [Test]
        public void GameplaySurfaceDiagnosticsAndIndirectArgsAvoidManagedFrameGarbage()
        {
            string cache = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "CpuTransvoxelChunkCache.cs"));
            string arena = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "SurfaceGeometryArena.cs"));
            string renderPass = ReadRenderingSource(
                Path.Combine("RenderFeature", "VoxelRenderPass.cs"));
            StringAssert.DoesNotContain("new uint[4]", cache);
            StringAssert.Contains("NativeArray<uint> _argsScratch", arena);
            StringAssert.Contains("VerboseSurfaceDiagnostics", renderPass);
            StringAssert.Contains("LastSurfacePassState = \"feature-aware\"", renderPass);
        }
'''
marker = '\n    }\n}'
pos = t.rfind(marker)
if pos < 0:
    raise SystemExit('architecture test closing marker missing')
t = t[:pos] + insert + t[pos:]
test_path.write_text(t)

# Guard exact legacy shapes.
cache = cache_path.read_text()
assert 'new uint[4]' not in cache
assert 'private void DropNoLongerResident' not in cache
assert 'List<int3> affected' not in cache
assert 'foreach (int3 chunk in _known)' not in cache
assert 'ResidencyChecksPerPrepare' in cache
assert 'RegionInvalidationCandidatesPerPrepare' in cache
assert 'NativeArray<uint> _argsScratch' in arena_path.read_text()
assert 'VerboseSurfaceDiagnostics' in pass_path.read_text()
