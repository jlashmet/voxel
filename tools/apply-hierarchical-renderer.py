from pathlib import Path

ROOT = Path(__file__).resolve().parents[1] if '__file__' in globals() else Path('.')


def replace_once(path: Path, old: str, new: str) -> None:
    text = path.read_text()
    count = text.count(old)
    if count != 1:
        raise RuntimeError(f'{path}: expected exactly one match, found {count}: {old[:100]!r}')
    path.write_text(text.replace(old, new, 1))


cache = ROOT / 'Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs'
coverage = ROOT / 'Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/SurfaceLodCoverageState.cs'
active = ROOT / 'Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/SurfaceLodActiveCoverage.cs'
scheduler = ROOT / 'Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/VoxelSurfaceScheduler.cs'

# CpuTransvoxelChunkCache: expose generation/visibility state to the scheduler and allow
# explicitly requested fallback nodes to build outside their nominal inner band.
replace_once(cache,
'''        private readonly Dictionary<int3, ulong> _emptyVersions = new();
        private readonly Dictionary<int3, double> _queuedAtSeconds = new();''',
'''        private readonly Dictionary<int3, ulong> _emptyVersions = new();
        // Hierarchical coverage may need a coarse parent inside its nominal inner LOD cut.
        // These requests are explicit and bounded by scheduler-visible coverage; they do not
        // make every discovered coarse chunk eligible for eager out-of-band rebuilding.
        private readonly HashSet<int3> _hierarchyRequested = new();
        private readonly Dictionary<int3, double> _queuedAtSeconds = new();''')

replace_once(cache,
'''        public bool OwnsRenderedChunk(int3 coordinate) =>
            _entries.TryGetValue(coordinate, out Entry entry) && entry.Ready;

        public int IndexedProfileBlockCount(int3 coordinate) =>''',
'''        public bool OwnsRenderedChunk(int3 coordinate) =>
            _entries.TryGetValue(coordinate, out Entry entry) && entry.Ready;

        /// <summary>
        /// Returns the cache's authoritative render-generation observation for one hierarchy
        /// node. A ready older entry remains a drawable fallback while DesiredGeneration points
        /// at a newer invalidated generation. Known-empty is a first-class completion proof.
        /// </summary>
        internal bool TryGetHierarchyState(int3 coordinate,
                                           out ulong desiredGeneration,
                                           out ulong drawableGeneration,
                                           out SurfaceLodCompletionKind drawableKind)
        {
            desiredGeneration = 0;
            drawableGeneration = 0;
            drawableKind = SurfaceLodCompletionKind.Incomplete;
            if (!_known.Contains(coordinate)) return false;

            _desiredVersions.TryGetValue(coordinate, out desiredGeneration);
            if (_entries.TryGetValue(coordinate, out Entry entry) && entry.Ready)
            {
                drawableGeneration = entry.SourceVersion;
                drawableKind = SurfaceLodCompletionKind.Ready;
                if (desiredGeneration == 0) desiredGeneration = drawableGeneration;
                return true;
            }

            if (_emptyVersions.TryGetValue(coordinate, out ulong emptyGeneration))
            {
                drawableGeneration = emptyGeneration;
                drawableKind = SurfaceLodCompletionKind.KnownEmpty;
                if (desiredGeneration == 0) desiredGeneration = drawableGeneration;
            }
            return true;
        }

        /// <summary>
        /// Ensures a hierarchy node has a render-generation proof in flight. Unlike ordinary
        /// ring admission, an explicit request may build a parent inside the ring's inner cut so
        /// it can cover missing fine detail. The request is cleared when the generation publishes.
        /// </summary>
        internal bool RequestHierarchyCoverage(int3 coordinate)
        {
            if (!OwnsShard(coordinate) || !TrackKnown(coordinate)) return false;
            _hierarchyRequested.Add(coordinate);

            bool hasReady = _entries.TryGetValue(coordinate, out Entry entry) && entry.Ready;
            bool hasEmpty = _emptyVersions.ContainsKey(coordinate);
            if (!_desiredVersions.ContainsKey(coordinate) && !hasReady && !hasEmpty)
                Invalidate(coordinate);
            else if (_desiredVersions.ContainsKey(coordinate))
                MarkDirty(coordinate);
            return true;
        }

        /// <summary>True when a known node is in its legacy distance shell and camera frustum.
        /// The scheduler uses this only to choose the desired refinement level; active fallback
        /// drawing is intentionally independent of the inner shell.</summary>
        internal bool IsDesiredVisibleCoordinate(int3 coordinate, Plane[] frustumPlanes,
                                                 Vector3 cameraPosition, float voxelSize)
        {
            if (!_known.Contains(coordinate)) return false;
            Bounds bounds = ChunkWorldBounds(coordinate, voxelSize);
            return WithinRingBand(bounds, cameraPosition)
                && GeometryUtility.TestPlanesAABB(frustumPlanes, bounds);
        }

        /// <summary>
        /// Adds an active hierarchy leaf to this worker's visible list without applying the
        /// legacy inner distance cut. Parent fallback must remain drawable while children refine.
        /// </summary>
        internal void CollectActiveCoordinate(int3 coordinate, Plane[] frustumPlanes,
                                              float voxelSize, int frame)
        {
            if (!_known.Contains(coordinate)) return;
            Bounds bounds = ChunkWorldBounds(coordinate, voxelSize);
            if (!GeometryUtility.TestPlanesAABB(frustumPlanes, bounds)) return;
            if (!_entries.TryGetValue(coordinate, out Entry entry) || !entry.Ready
                || entry.IndexCount == 0)
                return;
            entry.LastUsedFrame = frame;
            _visible.Add(entry);
        }

        internal void RecordHierarchyMissingVisible() => MissingVisibleCount++;

        public int IndexedProfileBlockCount(int3 coordinate) =>''')

replace_once(cache,
'''                Bounds bounds = ChunkWorldBounds(candidate, voxelSize);
                if (!WithinRingBand(bounds, cameraWorldPosition))
                {
                    RequeueDirty(candidate);
                    continue;
                }''',
'''                Bounds bounds = ChunkWorldBounds(candidate, voxelSize);
                if (!WithinRingBand(bounds, cameraWorldPosition)
                    && !_hierarchyRequested.Contains(candidate))
                {
                    RequeueDirty(candidate);
                    continue;
                }''')

replace_once(cache,
'''                _emptyVersions[_build.Coordinate] = _build.SourceVersion;
                CompletedBuildCount++;''',
'''                _emptyVersions[_build.Coordinate] = _build.SourceVersion;
                _hierarchyRequested.Remove(_build.Coordinate);
                CompletedBuildCount++;''')

replace_once(cache,
'''            entry.CoatingCatalogueHash = _build.CoatingCatalogueHash;
            CompletedBuildCount++;''',
'''            entry.CoatingCatalogueHash = _build.CoatingCatalogueHash;
            _hierarchyRequested.Remove(_build.Coordinate);
            CompletedBuildCount++;''')

replace_once(cache,
'''            _emptyVersions.Remove(chunk);
            _queuedAtSeconds.Remove(chunk);''',
'''            _emptyVersions.Remove(chunk);
            _hierarchyRequested.Remove(chunk);
            _queuedAtSeconds.Remove(chunk);''')

replace_once(cache,
'''            _dirty.Clear();
            _desiredVersions.Clear();
            _queuedAtSeconds.Clear();''',
'''            _dirty.Clear();
            _desiredVersions.Clear();
            _hierarchyRequested.Clear();
            _queuedAtSeconds.Clear();''')

# SurfaceLodCoverageState: mirror real cache observations, including stale-but-drawable.
replace_once(coverage,
'''        public bool TryGet(in SurfaceLodNodeKey key, out SurfaceLodNodeState state) =>
            _nodes.TryGetValue(key, out state);

        /// <summary>
        /// Advances the authoritative target generation while preserving any older drawable''',
'''        public bool TryGet(in SurfaceLodNodeKey key, out SurfaceLodNodeState state) =>
            _nodes.TryGetValue(key, out state);

        /// <summary>
        /// Mirrors the source cache's complete observation for one node. This is the integration
        /// path used by the scheduler: an older Ready proof may coexist with a newer desired
        /// generation, while an observed Incomplete state explicitly clears a proof that was
        /// evicted or retired from the source cache.
        /// </summary>
        public void Observe(in SurfaceLodNodeKey key, ulong desiredGeneration,
                            ulong drawableGeneration, SurfaceLodCompletionKind drawableKind)
        {
            if (_nodes.TryGetValue(key, out SurfaceLodNodeState previous)
                && desiredGeneration < previous.DesiredGeneration)
                throw new InvalidOperationException(
                    $"Cannot move {key} desired generation backward from " +
                    $"{previous.DesiredGeneration} to {desiredGeneration}.");
            if (drawableKind == SurfaceLodCompletionKind.Incomplete)
                drawableGeneration = 0;
            else if (drawableGeneration > desiredGeneration)
                throw new InvalidOperationException(
                    $"{key} drawable generation {drawableGeneration} cannot be newer than " +
                    $"desired generation {desiredGeneration}.");

            _nodes[key] = new SurfaceLodNodeState(
                desiredGeneration, drawableGeneration, drawableKind);
        }

        /// <summary>
        /// Advances the authoritative target generation while preserving any older drawable''')

# SurfaceLodActiveCoverage: navigation helpers; mutations remain seed/refine/merge only.
replace_once(active,
'''        public bool IsActive(in SurfaceLodNodeKey key) => _active.Contains(key);

        /// <summary>
        /// Seeds an uncovered region once a current-generation completion proof exists.''',
'''        public bool IsActive(in SurfaceLodNodeKey key) => _active.Contains(key);

        public bool TryFindActiveAncestorOrSelf(in SurfaceLodNodeKey key,
                                                out SurfaceLodNodeKey active)
        {
            SurfaceLodNodeKey cursor = key;
            while (true)
            {
                if (_active.Contains(cursor))
                {
                    active = cursor;
                    return true;
                }
                if (!SurfaceLodHierarchy.TryGetParentSourceStep(
                        cursor.SourceStep, out int parentStep))
                    break;
                cursor = new SurfaceLodNodeKey(
                    parentStep, SurfaceLodHierarchy.ParentCoordinate(cursor.Coordinate));
            }
            active = default;
            return false;
        }

        public bool HasActiveDescendant(in SurfaceLodNodeKey ancestor)
        {
            foreach (SurfaceLodNodeKey node in _active)
                if (IsStrictDescendantOf(node, ancestor)) return true;
            return false;
        }

        /// <summary>
        /// Seeds an uncovered region once a current-generation completion proof exists.''')

# VoxelSurfaceScheduler: hard bands express desired refinement; active hierarchy owns drawing.
replace_once(scheduler,
'''        private readonly List<CpuTransvoxelChunkCache.Entry> _visibleSolids = new(256);
        private readonly Plane[] _visibilityFrustumPlanes = new Plane[6];''',
'''        private readonly List<CpuTransvoxelChunkCache.Entry> _visibleSolids = new(256);
        private readonly SurfaceLodCoverageState _lodCoverageState = new();
        private readonly SurfaceLodActiveCoverage _activeLodCoverage = new();
        private readonly HashSet<SurfaceLodNodeKey> _desiredLodNodes = new();
        private readonly List<SurfaceLodNodeKey> _activeLodScratch = new(512);
        private readonly Plane[] _visibilityFrustumPlanes = new Plane[6];''')

replace_once(scheduler,
'''                if (clipmapMoved)
                    AddImmediateCameraDiscoveryRegions(storage, cameraPosition, voxelSize);
                StepClipmapAdmissionDiscovery(storage);''',
'''                if (clipmapMoved)
                {
                    // Active leaves are camera-window local. Rebuild the logical leaf set from
                    // still-resident cache generations after a clipmap move instead of retaining
                    // stale off-window ownership indefinitely. Ready entries are not discarded.
                    _activeLodCoverage.Clear();
                    _lodCoverageState.Clear();
                    AddImmediateCameraDiscoveryRegions(storage, cameraPosition, voxelSize);
                }
                StepClipmapAdmissionDiscovery(storage);''')

old_collect = '''        private void CollectVisibility(Camera camera, float voxelSize, int frame)
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

                        if (!ring.HasClipmapWindow)
                            ring.UpdateClipmapWindow(cameraPosition, voxelSize);
                        int radius = ring.ClipmapRadius;
                        int3 centre = ring.ClipmapCentre;

                        // The ring's toroidal grid already knows exactly which clipmap cells own
                        // discovered surface chunks. Walk that dense active list rather than the
                        // entire (2r+1)^3 coordinate volume. Outgoing slots can remain active for
                        // a few frames while retirement is sliced; skip them against the current
                        // window so delayed cleanup never draws stale residency.
                        int activeSlots = ring.ActiveSlotCount;
                        for (int slotIndex = 0; slotIndex < activeSlots; slotIndex++)
                        {
                            int3 coordinate = ring.ActiveSlotCoordinate(slotIndex);
                            int3 delta = math.abs(coordinate - centre);
                            if (math.cmax(delta) > radius) continue;

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

new_collect = '''        private void CollectVisibility(Camera camera, float voxelSize, int frame)
        {
            _visibleSolids.Clear();
            _desiredLodNodes.Clear();
            _lastVisibilityCandidateChecks = 0;
            double visibilityStart = Time.realtimeSinceStartupAsDouble;
            using (s_VisibilityMarker.Auto())
            {
                for (int i = 0; i < _allWorkers.Length; i++)
                    _allWorkers[i].BeginVisibilityCollection();

                if (camera != null)
                {
                    GeometryUtility.CalculateFrustumPlanes(camera, _visibilityFrustumPlanes);
                    Vector3 cameraPosition = camera.transform.position;

                    // Legacy box shells now answer only "what detail do we want here?". They no
                    // longer decide what may be drawn. The hierarchical active-leaf set below
                    // owns presentation and may keep a coarser parent inside this desired band.
                    for (int r = 0; r < _rings.Length; r++)
                    {
                        SurfaceRing ring = _rings[r];
                        if (!ring.HasClipmapWindow)
                            ring.UpdateClipmapWindow(cameraPosition, voxelSize);
                        int radius = ring.ClipmapRadius;
                        int3 centre = ring.ClipmapCentre;
                        int activeSlots = ring.ActiveSlotCount;
                        for (int slotIndex = 0; slotIndex < activeSlots; slotIndex++)
                        {
                            int3 coordinate = ring.ActiveSlotCoordinate(slotIndex);
                            int3 delta = math.abs(coordinate - centre);
                            if (math.cmax(delta) > radius) continue;

                            int shard = CpuTransvoxelChunkCache.ShardForChunk(
                                coordinate, ring.Workers.Length);
                            CpuTransvoxelChunkCache worker = ring.Workers[shard];
                            if (worker.IsDesiredVisibleCoordinate(
                                    coordinate, _visibilityFrustumPlanes, cameraPosition,
                                    voxelSize))
                                _desiredLodNodes.Add(new SurfaceLodNodeKey(
                                    ring.SourceStep, coordinate));
                            _lastVisibilityCandidateChecks++;
                        }
                    }

                    foreach (SurfaceLodNodeKey desired in _desiredLodNodes)
                    {
                        if (EnsureDesiredCoverage(desired)) continue;
                        WorkerFor(desired).RecordHierarchyMissingVisible();
                    }

                    _activeLodScratch.Clear();
                    _activeLodCoverage.CopyActiveTo(_activeLodScratch);
                    for (int i = 0; i < _activeLodScratch.Count; i++)
                    {
                        SurfaceLodNodeKey active = _activeLodScratch[i];
                        RequestAndSync(active);
                        WorkerFor(active).CollectActiveCoordinate(
                            active.Coordinate, _visibilityFrustumPlanes, voxelSize, frame);
                    }

                    for (int i = 0; i < _allWorkers.Length; i++)
                    {
                        IReadOnlyList<CpuTransvoxelChunkCache.Entry> visible =
                            _allWorkers[i].Visible;
                        for (int v = 0; v < visible.Count; v++)
                            _visibleSolids.Add(visible[v]);
                    }
                }

                _water.CollectVisible(camera, voxelSize);
            }
            _visibilityTiming.Add(ElapsedMs(visibilityStart));
        }

        private bool EnsureDesiredCoverage(in SurfaceLodNodeKey desired)
        {
            // Moving outward: descendants continue to draw until the requested parent is current.
            if (_activeLodCoverage.HasActiveDescendant(desired))
            {
                RequestAndSync(desired);
                _activeLodCoverage.TryMerge(desired, _lodCoverageState);
                return true;
            }

            if (!_activeLodCoverage.TryFindActiveAncestorOrSelf(
                    desired, out SurfaceLodNodeKey active))
            {
                SurfaceLodNodeKey root = CoarsestAncestor(desired);
                RequestAndSync(root);
                _activeLodCoverage.TryActivateCompleteNode(root, _lodCoverageState);
                if (!_activeLodCoverage.TryFindActiveAncestorOrSelf(desired, out active))
                    return false;
            }

            RequestAndSync(active);

            // Moving inward: subdivide one level at a time. A parent is removed only after all
            // eight children have current Ready/KnownEmpty proofs.
            while (active.SourceStep > desired.SourceStep)
            {
                if (!RequestAndSyncChildren(active)) break;
                if (!_activeLodCoverage.TryRefine(active, _lodCoverageState)) break;

                int childStep = active.SourceStep / 2;
                active = new SurfaceLodNodeKey(
                    childStep, AncestorCoordinateAtStep(desired, childStep));
            }
            return true;
        }

        private bool RequestAndSyncChildren(in SurfaceLodNodeKey parent)
        {
            if (!SurfaceLodHierarchy.TryGetChildSourceStep(
                    parent.SourceStep, out int childStep))
                return false;

            bool allObserved = true;
            for (int childIndex = 0;
                 childIndex < SurfaceLodHierarchy.ChildrenPerParent;
                 childIndex++)
            {
                var child = new SurfaceLodNodeKey(
                    childStep,
                    SurfaceLodHierarchy.ChildCoordinate(parent.Coordinate, childIndex));
                allObserved &= RequestAndSync(child);
            }
            return allObserved;
        }

        private bool RequestAndSync(in SurfaceLodNodeKey key)
        {
            CpuTransvoxelChunkCache worker = WorkerFor(key);
            if (!worker.RequestHierarchyCoverage(key.Coordinate)) return false;
            return SyncLodState(key, worker);
        }

        private bool SyncLodState(in SurfaceLodNodeKey key, CpuTransvoxelChunkCache worker)
        {
            if (!worker.TryGetHierarchyState(
                    key.Coordinate, out ulong desiredGeneration,
                    out ulong drawableGeneration, out SurfaceLodCompletionKind drawableKind))
            {
                _lodCoverageState.Remove(key);
                return false;
            }

            _lodCoverageState.Observe(
                key, desiredGeneration, drawableGeneration, drawableKind);
            return true;
        }

        private CpuTransvoxelChunkCache WorkerFor(in SurfaceLodNodeKey key)
        {
            SurfaceRing ring = RingForSourceStep(key.SourceStep);
            int shard = CpuTransvoxelChunkCache.ShardForChunk(
                key.Coordinate, ring.Workers.Length);
            return ring.Workers[shard];
        }

        private SurfaceRing RingForSourceStep(int sourceStep)
        {
            for (int i = 0; i < _rings.Length; i++)
                if (_rings[i].SourceStep == sourceStep) return _rings[i];
            throw new ArgumentOutOfRangeException(
                nameof(sourceStep), sourceStep, "Unknown surface LOD source step.");
        }

        private static SurfaceLodNodeKey CoarsestAncestor(in SurfaceLodNodeKey node)
        {
            int step = node.SourceStep;
            int3 coordinate = node.Coordinate;
            while (SurfaceLodHierarchy.TryGetParentSourceStep(step, out int parentStep))
            {
                coordinate = SurfaceLodHierarchy.ParentCoordinate(coordinate);
                step = parentStep;
            }
            return new SurfaceLodNodeKey(step, coordinate);
        }

        private static int3 AncestorCoordinateAtStep(in SurfaceLodNodeKey node, int sourceStep)
        {
            if (!SurfaceLodHierarchy.IsSupportedSourceStep(sourceStep)
                || sourceStep < node.SourceStep)
                throw new ArgumentOutOfRangeException(nameof(sourceStep));

            int step = node.SourceStep;
            int3 coordinate = node.Coordinate;
            while (step < sourceStep)
            {
                if (!SurfaceLodHierarchy.TryGetParentSourceStep(step, out int parentStep))
                    throw new InvalidOperationException(
                        $"Cannot map {node} to source step {sourceStep}.");
                coordinate = SurfaceLodHierarchy.ParentCoordinate(coordinate);
                step = parentStep;
            }
            return coordinate;
        }
'''
replace_once(scheduler, old_collect, new_collect)

print('Hierarchical renderer integration patch applied successfully.')
