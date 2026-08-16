from pathlib import Path


def replace_once(path: str, old: str, new: str) -> None:
    p = Path(path)
    text = p.read_text()
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{path}: expected exactly one match, found {count}\n--- needle ---\n{old}")
    p.write_text(text.replace(old, new, 1))


def insert_before(path: str, marker: str, addition: str) -> None:
    p = Path(path)
    text = p.read_text()
    count = text.count(marker)
    if count != 1:
        raise SystemExit(f"{path}: expected exactly one marker, found {count}: {marker}")
    p.write_text(text.replace(marker, addition + marker, 1))


scheduler = "Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/VoxelSurfaceScheduler.cs"
tests = "Assets/Tests/EditMode/SurfaceBrickDiscoveryTests.cs"

# SurfaceRing remembers the region AABB covered by its current clipmap window. The renderer still
# owns no world surface cache; these bounds are only used to request fresh compact discovery for
# regions newly exposed by camera motion.
replace_once(
    scheduler,
    """            public int3 ClipmapCentre { get; private set; }
            public int ClipmapRadius { get; private set; }
            public bool HasClipmapWindow { get; private set; }
""",
    """            public int3 ClipmapCentre { get; private set; }
            public int ClipmapRadius { get; private set; }
            public bool HasClipmapWindow { get; private set; }
            public int3 ClipmapRegionMin { get; private set; }
            public int3 ClipmapRegionMaxExclusive { get; private set; }
""",
)

old_update = """            public void UpdateClipmapWindow(Vector3 cameraPosition, float voxelSize)
            {
                float chunkMetres = CpuTransvoxelChunkCache.CellsPerAxis * SourceStep * voxelSize;
                int radius = Mathf.CeilToInt(OuterRadiusMetres / chunkMetres) + 1;
                int3 centre = new(
                    Mathf.FloorToInt(cameraPosition.x / chunkMetres),
                    Mathf.FloorToInt(cameraPosition.y / chunkMetres),
                    Mathf.FloorToInt(cameraPosition.z / chunkMetres));
                ClipmapCentre = centre;
                ClipmapRadius = radius;
                HasClipmapWindow = true;
                for (int i = 0; i < Workers.Length; i++)
                    Workers[i].SetClipmapWindow(centre, radius);
            }
"""
new_update = """            public void UpdateClipmapWindow(Vector3 cameraPosition, float voxelSize)
            {
                UpdateClipmapWindow(cameraPosition, voxelSize, out _, out _, out _, out _, out _);
            }

            public bool UpdateClipmapWindow(Vector3 cameraPosition, float voxelSize,
                                            out bool hadPrevious,
                                            out int3 previousRegionMin,
                                            out int3 previousRegionMaxExclusive,
                                            out int3 currentRegionMin,
                                            out int3 currentRegionMaxExclusive)
            {
                hadPrevious = HasClipmapWindow;
                previousRegionMin = ClipmapRegionMin;
                previousRegionMaxExclusive = ClipmapRegionMaxExclusive;

                float chunkMetres = CpuTransvoxelChunkCache.CellsPerAxis * SourceStep * voxelSize;
                int radius = Mathf.CeilToInt(OuterRadiusMetres / chunkMetres) + 1;
                int3 centre = new(
                    Mathf.FloorToInt(cameraPosition.x / chunkMetres),
                    Mathf.FloorToInt(cameraPosition.y / chunkMetres),
                    Mathf.FloorToInt(cameraPosition.z / chunkMetres));
                int voxelsPerChunk = CpuTransvoxelChunkCache.CellsPerAxis * SourceStep;
                int3 minChunk = centre - radius;
                int3 maxChunkExclusive = centre + radius + 1;
                int3 minVoxel = minChunk * voxelsPerChunk;
                int3 maxVoxelExclusive = maxChunkExclusive * voxelsPerChunk;

                currentRegionMin = FloorDiv(minVoxel, VoxelGrid.RegionVoxelEdge);
                currentRegionMaxExclusive = FloorDiv(
                    maxVoxelExclusive - 1, VoxelGrid.RegionVoxelEdge) + 1;

                ClipmapCentre = centre;
                ClipmapRadius = radius;
                ClipmapRegionMin = currentRegionMin;
                ClipmapRegionMaxExclusive = currentRegionMaxExclusive;
                HasClipmapWindow = true;
                for (int i = 0; i < Workers.Length; i++)
                    Workers[i].SetClipmapWindow(centre, radius);

                return !hadPrevious
                    || math.any(previousRegionMin != currentRegionMin)
                    || math.any(previousRegionMaxExclusive != currentRegionMaxExclusive);
            }
"""
replace_once(scheduler, old_update, new_update)

# Incremental region-box queue. Each box represents only newly exposed regions, not the entire
# clipmap, so ordinary one-chunk camera motion cannot create an O(view-volume) frame spike.
replace_once(
    scheduler,
    """        private readonly HashSet<int3> _surfaceDiscoveryRescanRegions = new();
        private NativeArray<ulong> _surfaceDiscoveryOccupiedWords;
""",
    """        private readonly HashSet<int3> _surfaceDiscoveryRescanRegions = new();

        private readonly struct ClipmapRegionBox
        {
            public readonly int3 Min;
            public readonly int3 MaxExclusive;

            public ClipmapRegionBox(int3 min, int3 maxExclusive)
            {
                Min = min;
                MaxExclusive = maxExclusive;
            }
        }

        private const int ClipmapAdmissionRegionsPerFrame = 64;
        private readonly Queue<ClipmapRegionBox> _clipmapAdmissionQueue = new();
        private ClipmapRegionBox _activeClipmapAdmissionBox;
        private int _activeClipmapAdmissionCursor;
        private bool _hasActiveClipmapAdmission;
        private NativeArray<ulong> _surfaceDiscoveryOccupiedWords;
""",
)

# Useful targeted diagnostic for the EditMode regression; no production caller needs reflection
# into private ring/worker ownership to answer which LOD has admitted a chunk.
replace_once(
    scheduler,
    """        public int LastVisibilityCandidateChecks => _lastVisibilityCandidateChecks;
        public bool ChangeFeedBacklogged => _changeFeedHasMore
""",
    """        public int LastVisibilityCandidateChecks => _lastVisibilityCandidateChecks;
        internal int KnownChunkCountForSourceStep(int sourceStep)
        {
            int count = 0;
            for (int r = 0; r < _rings.Length; r++)
            {
                if (_rings[r].SourceStep != sourceStep) continue;
                for (int w = 0; w < _rings[r].Workers.Length; w++)
                    count += _rings[r].Workers[w].KnownCount;
            }
            return count;
        }
        public bool ChangeFeedBacklogged => _changeFeedHasMore
""",
)

# Camera movement now records region-window deltas before change-feed processing. Initial
# discovery still comes from the authoritative journal/overflow recovery; only subsequent window
# movement needs re-admission. A small current-camera neighbourhood jumps ahead of a teleport scan.
replace_once(
    scheduler,
    """            if (camera != null)
            {
                Vector3 cameraPosition = camera.transform.position;
                for (int r = 0; r < _rings.Length; r++)
                    _rings[r].UpdateClipmapWindow(cameraPosition, voxelSize);
            }

            double journalStart = Time.realtimeSinceStartupAsDouble;
""",
    """            if (camera != null)
            {
                Vector3 cameraPosition = camera.transform.position;
                bool clipmapMoved = false;
                for (int r = 0; r < _rings.Length; r++)
                {
                    SurfaceRing ring = _rings[r];
                    bool changed = ring.UpdateClipmapWindow(
                        cameraPosition, voxelSize,
                        out bool hadPrevious,
                        out int3 previousMin,
                        out int3 previousMaxExclusive,
                        out int3 currentMin,
                        out int3 currentMaxExclusive);
                    if (!changed || !hadPrevious) continue;
                    clipmapMoved = true;
                    EnqueueClipmapRegionDifference(
                        previousMin, previousMaxExclusive,
                        currentMin, currentMaxExclusive);
                }

                if (clipmapMoved)
                    AddImmediateCameraDiscoveryRegions(storage, cameraPosition);
                StepClipmapAdmissionDiscovery(storage);
            }

            double journalStart = Time.realtimeSinceStartupAsDouble;
""",
)

# Add the bounded difference scanner immediately before change-feed logic. Boxes are decomposed
# into at most six non-overlapping slabs; a teleport with no overlap falls back to one new-window
# box but is still consumed at ClipmapAdmissionRegionsPerFrame.
marker = """        private void ProcessChangeFeed(IRegionReadSource storage, IVoxelChangeSource journal)
"""
addition = r'''        private static int FloorDiv(int value, int divisor)
        {
            int quotient = value / divisor;
            int remainder = value % divisor;
            return remainder < 0 ? quotient - 1 : quotient;
        }

        private static int3 FloorDiv(int3 value, int divisor) => new(
            FloorDiv(value.x, divisor),
            FloorDiv(value.y, divisor),
            FloorDiv(value.z, divisor));

        private void AddImmediateCameraDiscoveryRegions(IRegionReadSource storage,
                                                        Vector3 cameraPosition)
        {
            int3 cameraVoxel = new(
                Mathf.FloorToInt(cameraPosition.x / 0.1f),
                Mathf.FloorToInt(cameraPosition.y / 0.1f),
                Mathf.FloorToInt(cameraPosition.z / 0.1f));
            int3 cameraRegion = FloorDiv(cameraVoxel, VoxelGrid.RegionVoxelEdge);
            for (int z = -1; z <= 1; z++)
            for (int y = -1; y <= 1; y++)
            for (int x = -1; x <= 1; x++)
            {
                int3 region = cameraRegion + new int3(x, y, z);
                if (storage.IsRegionResident(region))
                    _surfaceDiscoveryRegions.Add(region);
            }
        }

        private void EnqueueClipmapRegionDifference(int3 oldMin, int3 oldMaxExclusive,
                                                    int3 newMin, int3 newMaxExclusive)
        {
            int3 overlapMin = math.max(oldMin, newMin);
            int3 overlapMax = math.min(oldMaxExclusive, newMaxExclusive);
            if (math.any(overlapMin >= overlapMax))
            {
                EnqueueClipmapRegionBox(newMin, newMaxExclusive);
                return;
            }

            // X slabs own the full Y/Z span. Y slabs are restricted to the overlapping X span,
            // and Z slabs to overlapping X/Y, making the six boxes disjoint.
            EnqueueClipmapRegionBox(
                newMin, new int3(overlapMin.x, newMaxExclusive.y, newMaxExclusive.z));
            EnqueueClipmapRegionBox(
                new int3(overlapMax.x, newMin.y, newMin.z), newMaxExclusive);
            EnqueueClipmapRegionBox(
                new int3(overlapMin.x, newMin.y, newMin.z),
                new int3(overlapMax.x, overlapMin.y, newMaxExclusive.z));
            EnqueueClipmapRegionBox(
                new int3(overlapMin.x, overlapMax.y, newMin.z),
                new int3(overlapMax.x, newMaxExclusive.y, newMaxExclusive.z));
            EnqueueClipmapRegionBox(
                new int3(overlapMin.x, overlapMin.y, newMin.z),
                new int3(overlapMax.x, overlapMax.y, overlapMin.z));
            EnqueueClipmapRegionBox(
                new int3(overlapMin.x, overlapMin.y, overlapMax.z),
                new int3(overlapMax.x, overlapMax.y, newMaxExclusive.z));
        }

        private void EnqueueClipmapRegionBox(int3 min, int3 maxExclusive)
        {
            if (math.any(min >= maxExclusive)) return;
            _clipmapAdmissionQueue.Enqueue(new ClipmapRegionBox(min, maxExclusive));
        }

        private void StepClipmapAdmissionDiscovery(IRegionReadSource storage)
        {
            int remaining = ClipmapAdmissionRegionsPerFrame;
            while (remaining > 0)
            {
                if (!_hasActiveClipmapAdmission)
                {
                    if (_clipmapAdmissionQueue.Count == 0) return;
                    _activeClipmapAdmissionBox = _clipmapAdmissionQueue.Dequeue();
                    _activeClipmapAdmissionCursor = 0;
                    _hasActiveClipmapAdmission = true;
                }

                int3 counts = _activeClipmapAdmissionBox.MaxExclusive
                            - _activeClipmapAdmissionBox.Min;
                int total = counts.x * counts.y * counts.z;
                while (remaining > 0 && _activeClipmapAdmissionCursor < total)
                {
                    int linear = _activeClipmapAdmissionCursor++;
                    int x = linear % counts.x;
                    int y = (linear / counts.x) % counts.y;
                    int z = linear / (counts.x * counts.y);
                    int3 region = _activeClipmapAdmissionBox.Min + new int3(x, y, z);
                    remaining--;
                    if (storage.IsRegionResident(region))
                        _surfaceDiscoveryRegions.Add(region);
                }

                if (_activeClipmapAdmissionCursor < total) return;
                _hasActiveClipmapAdmission = false;
                _activeClipmapAdmissionCursor = 0;
            }
        }

'''
insert_before(scheduler, marker, addition)

# The immediate-camera helper must respect projects whose voxel scale differs from the showcase.
# Pass the current voxel size rather than baking 10 cm into the scheduler.
replace_once(
    scheduler,
    "AddImmediateCameraDiscoveryRegions(storage, cameraPosition);",
    "AddImmediateCameraDiscoveryRegions(storage, cameraPosition, voxelSize);",
)
replace_once(
    scheduler,
    """        private void AddImmediateCameraDiscoveryRegions(IRegionReadSource storage,
                                                        Vector3 cameraPosition)
        {
            int3 cameraVoxel = new(
                Mathf.FloorToInt(cameraPosition.x / 0.1f),
                Mathf.FloorToInt(cameraPosition.y / 0.1f),
                Mathf.FloorToInt(cameraPosition.z / 0.1f));
""",
    """        private void AddImmediateCameraDiscoveryRegions(IRegionReadSource storage,
                                                        Vector3 cameraPosition,
                                                        float voxelSize)
        {
            float safeVoxelSize = math.max(1e-6f, voxelSize);
            int3 cameraVoxel = new(
                Mathf.FloorToInt(cameraPosition.x / safeVoxelSize),
                Mathf.FloorToInt(cameraPosition.y / safeVoxelSize),
                Mathf.FloorToInt(cameraPosition.z / safeVoxelSize));
""",
)

# Regression: a surface first discovered while outside step-1 is deliberately ignored by that
# worker. Moving the camera into its already-resident region must rediscover/re-admit it without a
# new world mutation.
test_marker = """        [Test]
        public void SchedulerPrepareDiscoversSurfaceBricksWithoutMips()
"""
test_addition = r'''        [Test]
        public void ClipmapMotionReadmitsAlreadyResidentSurfaceIntoFinerLod()
        {
            // Region 5 starts at 256 m. It is initially in the step-4 band but outside step-1.
            // After moving the camera to x=200 m the same unchanged surface is ~57 m away and
            // belongs to step-1. No second journal publication is allowed: clipmap admission must
            // request compact discovery for the newly exposed region itself.
            int3 regionCoord = new(5, 0, 0);
            MakeRegion(regionCoord, new int3(1, 10, 10));
            var source = new RegionReadSource(in _table, in _pool, _journal);

            var cameraObject = new GameObject("ClipmapReadmissionCamera");
            var camera = cameraObject.AddComponent<Camera>();
            var scheduler = new VoxelSurfaceScheduler();
            try
            {
                MaterialPaletteView palette = default;
                SurfaceCatalogueView surfaceCatalogue = default;
                CoatingCatalogueView coatingCatalogue = default;
                camera.transform.position = Vector3.zero;

                bool discoveredByOuterLod = false;
                for (int frame = 1; frame <= 256 && !discoveredByOuterLod; frame++)
                {
                    scheduler.Prepare(source, in palette, in surfaceCatalogue,
                                      in coatingCatalogue, null, _journal,
                                      camera, 0.1f, frame);
                    discoveredByOuterLod = scheduler.KnownChunkCountForSourceStep(4) > 0;
                    if (!discoveredByOuterLod) System.Threading.Thread.Yield();
                }

                Assert.True(discoveredByOuterLod,
                    "Initial discovery never reached the step-4 ring, so the re-admission setup is invalid.");
                Assert.AreEqual(0, scheduler.KnownChunkCountForSourceStep(1),
                    "The target surface must begin outside the fine-ring clipmap.");

                camera.transform.position = new Vector3(200f, 0f, 0f);
                bool admittedToFineLod = false;
                for (int frame = 257; frame <= 640 && !admittedToFineLod; frame++)
                {
                    scheduler.Prepare(source, in palette, in surfaceCatalogue,
                                      in coatingCatalogue, null, _journal,
                                      camera, 0.1f, frame);
                    admittedToFineLod = scheduler.KnownChunkCountForSourceStep(1) > 0;
                    if (!admittedToFineLod) System.Threading.Thread.Yield();
                }

                Assert.True(admittedToFineLod,
                    "Camera motion entered an already-resident surface region but the fine LOD "
                  + "never re-ran surface discovery. This would create an LOD handoff hole.");
            }
            finally
            {
                scheduler.Dispose();
                Object.DestroyImmediate(cameraObject);
            }
        }

'''
insert_before(tests, test_marker, test_addition)

print("bounded clipmap surface re-admission patch applied")
