from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def replace_once(path: Path, old: str, new: str) -> None:
    text = path.read_text()
    count = text.count(old)
    if count != 1:
        raise RuntimeError(f"{path}: expected exactly one match, found {count}: {old[:120]!r}")
    path.write_text(text.replace(old, new, 1))


cache = ROOT / "Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs"
scheduler = ROOT / "Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/VoxelSurfaceScheduler.cs"
active = ROOT / "Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/SurfaceLodActiveCoverage.cs"
architecture_tests = ROOT / "Assets/Tests/EditMode/GeometryPipelineArchitectureTests.cs"
tracker = ROOT / "docs/GPU_VOXEL_RENDERER_MIGRATION_PLAN.md"

# A hierarchy request must not put an already in-flight generation back into the dirty FIFO.
replace_once(cache,
'''            if (!_desiredVersions.ContainsKey(coordinate) && !hasReady && !hasEmpty)
                Invalidate(coordinate);
            else if (_desiredVersions.ContainsKey(coordinate))
                MarkDirty(coordinate);
            return true;
        }

        /// <summary>True when a known node is in its legacy distance shell and camera frustum.''',
'''            if (!_desiredVersions.ContainsKey(coordinate) && !hasReady && !hasEmpty)
                Invalidate(coordinate);
            else if (_desiredVersions.ContainsKey(coordinate)
                     && !_dirty.Contains(coordinate)
                     && (!_build.Active || !_build.Coordinate.Equals(coordinate)))
                MarkDirty(coordinate);
            return true;
        }

        /// <summary>
        /// Active hierarchy leaves are coverage, not cache. Keep their live leases out of both
        /// arena-pressure and resident-capacity eviction for the entire world frame. The scheduler
        /// republishes this bounded set once per frame from its logical active-leaf hierarchy.
        /// </summary>
        internal void BeginHierarchyActiveFrame() => _hierarchyActive.Clear();

        internal void MarkHierarchyActive(int3 coordinate)
        {
            if (_known.Contains(coordinate)) _hierarchyActive.Add(coordinate);
        }

        /// <summary>True when a known node is in its legacy distance shell and camera frustum.''')

replace_once(cache,
'''        private readonly HashSet<int3> _hierarchyRequested = new();
        private readonly Dictionary<int3, double> _queuedAtSeconds = new();''',
'''        private readonly HashSet<int3> _hierarchyRequested = new();
        private readonly HashSet<int3> _hierarchyActive = new();
        private readonly Dictionary<int3, double> _queuedAtSeconds = new();''')

# Active coverage may never be selected as a lease victim. Cold eviction is not a build request;
# visibility/refinement will explicitly request the node again if it becomes useful later.
replace_once(cache,
'''                if (_build.Active && pair.Key.Equals(_build.Coordinate)) continue;
                Bounds bounds = ChunkWorldBounds(pair.Key, voxelSize);''',
'''                if (_build.Active && pair.Key.Equals(_build.Coordinate)) continue;
                if (_hierarchyActive.Contains(pair.Key)) continue;
                Bounds bounds = ChunkWorldBounds(pair.Key, voxelSize);''')

replace_once(cache,
'''            if (_entries.TryGetValue(victim, out Entry entry)) RecycleEntry(entry);
            _entries.Remove(victim);
            MarkDirty(victim);
            return true;
        }

        private void EnforceCapacity''',
'''            if (_entries.TryGetValue(victim, out Entry entry)) RecycleEntry(entry);
            _entries.Remove(victim);
            return true;
        }

        private void EnforceCapacity''')

replace_once(cache,
'''                if (camera != null && GeometryUtility.TestPlanesAABB(
                        _frustumPlanes, ChunkWorldBounds(pair.Key, voxelSize)))
                    continue;
                Vector3 centre =''',
'''                if (_hierarchyActive.Contains(pair.Key)) continue;
                if (camera != null && GeometryUtility.TestPlanesAABB(
                        _frustumPlanes, ChunkWorldBounds(pair.Key, voxelSize)))
                    continue;
                Vector3 centre =''')

replace_once(cache,
'''            if (_entries.TryGetValue(victim, out Entry entry)) RecycleEntry(entry);
            _entries.Remove(victim);
            MarkDirty(victim);
        }

        private bool TryRemoveChunk''',
'''            if (_entries.TryGetValue(victim, out Entry entry)) RecycleEntry(entry);
            _entries.Remove(victim);
        }

        private bool TryRemoveChunk''')

replace_once(cache,
'''            _emptyVersions.Remove(chunk);
            _hierarchyRequested.Remove(chunk);
            _queuedAtSeconds.Remove(chunk);''',
'''            _emptyVersions.Remove(chunk);
            _hierarchyRequested.Remove(chunk);
            _hierarchyActive.Remove(chunk);
            _queuedAtSeconds.Remove(chunk);''')

replace_once(cache,
'''            _desiredVersions.Clear();
            _hierarchyRequested.Clear();
            _queuedAtSeconds.Clear();''',
'''            _desiredVersions.Clear();
            _hierarchyRequested.Clear();
            _hierarchyActive.Clear();
            _queuedAtSeconds.Clear();''')

# Exact-leaf retirement is permitted only when the underlying clipmap cache has already retired
# ownership; desired coverage will seed a valid ancestor in the same visibility pass if needed.
replace_once(active,
'''        public bool HasActiveDescendant(in SurfaceLodNodeKey ancestor)
        {
            foreach (SurfaceLodNodeKey node in _active)
                if (IsStrictDescendantOf(node, ancestor)) return true;
            return false;
        }

        /// <summary>
        /// Seeds an uncovered region once a current-generation completion proof exists.''',
'''        public bool HasActiveDescendant(in SurfaceLodNodeKey ancestor)
        {
            foreach (SurfaceLodNodeKey node in _active)
                if (IsStrictDescendantOf(node, ancestor)) return true;
            return false;
        }

        internal bool RemoveRetiredLeaf(in SurfaceLodNodeKey key) => _active.Remove(key);

        /// <summary>
        /// Seeds an uncovered region once a current-generation completion proof exists.''')

# Clipmap movement changes discovery ownership, not presentation truth. Retain still-owned active
# leaves and prune only leaves whose cache slot was actually retired.
replace_once(scheduler,
'''                if (clipmapMoved)
                {
                    // Active leaves are camera-window local. Rebuild the logical leaf set from
                    // still-resident cache generations after a clipmap move instead of retaining
                    // stale off-window ownership indefinitely. Ready entries are not discarded.
                    _activeLodCoverage.Clear();
                    _lodCoverageState.Clear();
                    AddImmediateCameraDiscoveryRegions(storage, cameraPosition, voxelSize);
                }''',
'''                if (clipmapMoved)
                {
                    // A clipmap move changes cache admission, not presentation ownership. Keep
                    // still-owned active leaves so camera motion cannot erase a stale-but-drawable
                    // fallback while its replacement generation is pending. Retired leaves are
                    // pruned after worker residency maintenance below.
                    AddImmediateCameraDiscoveryRegions(storage, cameraPosition, voxelSize);
                }''')

# Publish logical active leaves to per-cache eviction pins before any worker can enforce capacity
# or react to shared arena pressure.
replace_once(scheduler,
'''            // Discovery is correctness work rather than build admission: every worker must learn
            // about newly surfaced bricks even if this frame has no time left to rebuild them.
            for (int i = 0; i < _allWorkers.Length; i++)
                _allWorkers[i].DiscoverSurfaceBricks(_discoveredSurfaceBricks);

            double workersStart = Time.realtimeSinceStartupAsDouble;''',
'''            // Discovery is correctness work rather than build admission: every worker must learn
            // about newly surfaced bricks even if this frame has no time left to rebuild them.
            for (int i = 0; i < _allWorkers.Length; i++)
                _allWorkers[i].DiscoverSurfaceBricks(_discoveredSurfaceBricks);

            PinActiveHierarchyCoverageForFrame();

            double workersStart = Time.realtimeSinceStartupAsDouble;''')

replace_once(scheduler,
'''                for (int i = 0; i < _allWorkers.Length; i++)
                    _allWorkers[i].BeginVisibilityCollection();

                if (camera != null)''',
'''                for (int i = 0; i < _allWorkers.Length; i++)
                    _allWorkers[i].BeginVisibilityCollection();
                PruneRetiredActiveCoverage();

                if (camera != null)''')

replace_once(scheduler,
'''        private bool EnsureDesiredCoverage(in SurfaceLodNodeKey desired)
        {''',
'''        private void PinActiveHierarchyCoverageForFrame()
        {
            for (int i = 0; i < _allWorkers.Length; i++)
                _allWorkers[i].BeginHierarchyActiveFrame();

            _activeLodScratch.Clear();
            _activeLodCoverage.CopyActiveTo(_activeLodScratch);
            for (int i = 0; i < _activeLodScratch.Count; i++)
            {
                SurfaceLodNodeKey active = _activeLodScratch[i];
                WorkerFor(active).MarkHierarchyActive(active.Coordinate);
            }
        }

        private void PruneRetiredActiveCoverage()
        {
            _activeLodScratch.Clear();
            _activeLodCoverage.CopyActiveTo(_activeLodScratch);
            for (int i = 0; i < _activeLodScratch.Count; i++)
            {
                SurfaceLodNodeKey active = _activeLodScratch[i];
                CpuTransvoxelChunkCache worker = WorkerFor(active);
                if (worker.TryGetHierarchyState(
                        active.Coordinate, out _, out _, out _))
                    continue;
                _activeLodCoverage.RemoveRetiredLeaf(active);
                _lodCoverageState.Remove(active);
            }
        }

        private bool EnsureDesiredCoverage(in SurfaceLodNodeKey desired)
        {''')

# The source architecture guard now enforces two-stage LOD selection + active-leaf rendering.
replace_once(architecture_tests,
'''            StringAssert.Contains("ShardForChunk", productionVisibility);
            StringAssert.Contains("CollectVisibleCoordinate", productionVisibility);
            StringAssert.DoesNotContain("for (int z = -radius; z <= radius; z++)", productionVisibility);''',
'''            StringAssert.Contains("ShardForChunk", productionVisibility);
            StringAssert.Contains("IsDesiredVisibleCoordinate", productionVisibility);
            StringAssert.Contains("CollectActiveCoordinate", productionVisibility);
            StringAssert.Contains("_activeLodCoverage", productionVisibility);
            StringAssert.DoesNotContain("CollectVisibleCoordinate(", productionVisibility);
            StringAssert.DoesNotContain("for (int z = -radius; z <= radius; z++)", productionVisibility);''')

# T1.1 already passed the Unity EditMode runner on the foundation commit.
replace_once(tracker,
'''- [ ] **T1.1** Add tested floor-safe parent/child coordinate mapping for steps 1/2/4/8.''',
'''- [x] **T1.1** Add tested floor-safe parent/child coordinate mapping for steps 1/2/4/8.''')

print("hierarchical coverage follow-up applied")
