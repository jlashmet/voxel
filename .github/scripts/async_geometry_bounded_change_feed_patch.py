from pathlib import Path


def once(text, old, new, label):
    n = text.count(old)
    if n != 1:
        raise SystemExit(f'{label}: expected one match, found {n}')
    return text.replace(old, new, 1)


# -----------------------------------------------------------------------------
# Storage API: bounded consumer-owned cursors, preserving legacy methods.
# -----------------------------------------------------------------------------
feed_path = Path('Assets/VoxelEngine/Storage/Api/VoxelChangeFeed.cs')
f = feed_path.read_text()
f = once(f,
'''        ulong CurrentVersion { get; }
        bool ReadSince(ref ulong cursor, List<VoxelChangeRecord> destination);''',
'''        ulong CurrentVersion { get; }
        bool ReadSince(ref ulong cursor, List<VoxelChangeRecord> destination);

        /// <summary>
        /// Reads at most <paramref name="maxRecords"/> retained records newer than
        /// <paramref name="cursor"/>. On a valid incremental read, cursor advances only to the
        /// last emitted record and <paramref name="hasMore"/> reports remaining backlog. If the
        /// cursor has fallen behind retention, returns false, advances cursor to CurrentVersion,
        /// clears destination and reports no replay backlog; the consumer must perform its own
        /// bounded full-state recovery.
        /// </summary>
        bool ReadSince(ref ulong cursor, List<VoxelChangeRecord> destination,
                       int maxRecords, out bool hasMore);''', 'bounded change feed api')
feed_path.write_text(f)

region_api_path = Path('Assets/VoxelEngine/Storage/Api/IRegionReadSource.cs')
r = region_api_path.read_text()
r = once(r,
'''        /// <summary>Caller owns and disposes the returned coordinate array.</summary>
        NativeArray<int3> GetResidentRegionCoords(Allocator allocator);

        /// <summary>
        /// Acquires a borrowed read view''',
'''        /// <summary>Caller owns and disposes the returned coordinate array.</summary>
        NativeArray<int3> GetResidentRegionCoords(Allocator allocator);

        /// <summary>
        /// Copies a bounded slice of the resident-region table into caller-owned memory.
        /// <paramref name="cursor"/> is an opaque scan cursor owned by the consumer. At most
        /// destination.Length internal region slots are inspected per call, so sparse/free slots
        /// cannot turn recovery into an unbounded scan. Returns true when the current table scan
        /// has reached its end; later residency changes are delivered through the change feed.
        /// </summary>
        bool CopyResidentRegionCoords(ref int cursor, NativeArray<int3> destination,
                                      out int count);

        /// <summary>
        /// Acquires a borrowed read view''', 'bounded resident region api')
region_api_path.write_text(r)


# -----------------------------------------------------------------------------
# Storage implementations.
# -----------------------------------------------------------------------------
journal_path = Path('Assets/VoxelEngine/Storage/Runtime/VoxelChangeJournal.cs')
j = journal_path.read_text()
j = once(j,
'''        /// <summary>Reads records newer than cursor and advances it to the current version.</summary>
        public bool ReadSince(ref ulong cursor, List<VoxelChangeRecord> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            destination.Clear();
            bool overflowed = _count > 0 && cursor + 1 < OldestRetainedVersion;
            for (int i = 0; i < _count; i++)
            {
                VoxelChangeRecord record = _records[(_start + i) % _records.Length];
                if (overflowed || record.Version > cursor) destination.Add(record);
            }
            cursor = CurrentVersion;
            return !overflowed;
        }''',
'''        /// <summary>Reads records newer than cursor and advances it to the current version.</summary>
        public bool ReadSince(ref ulong cursor, List<VoxelChangeRecord> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            destination.Clear();
            bool overflowed = _count > 0 && cursor + 1 < OldestRetainedVersion;
            for (int i = 0; i < _count; i++)
            {
                VoxelChangeRecord record = _records[(_start + i) % _records.Length];
                if (overflowed || record.Version > cursor) destination.Add(record);
            }
            cursor = CurrentVersion;
            return !overflowed;
        }

        public bool ReadSince(ref ulong cursor, List<VoxelChangeRecord> destination,
                              int maxRecords, out bool hasMore)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (maxRecords <= 0) throw new ArgumentOutOfRangeException(nameof(maxRecords));
            destination.Clear();

            bool overflowed = _count > 0 && cursor + 1 < OldestRetainedVersion;
            if (overflowed)
            {
                // Exact replay is already impossible. Do not spend frame time copying a retained
                // suffix the consumer must ignore; move it to the recovery boundary immediately.
                cursor = CurrentVersion;
                hasMore = false;
                return false;
            }

            ulong targetVersion = CurrentVersion;
            int emitted = 0;
            for (int i = 0; i < _count && emitted < maxRecords; i++)
            {
                VoxelChangeRecord record = _records[(_start + i) % _records.Length];
                if (record.Version <= cursor) continue;
                destination.Add(record);
                cursor = record.Version;
                emitted++;
            }
            hasMore = cursor < targetVersion;
            return true;
        }''', 'bounded journal implementation')
journal_path.write_text(j)

region_table_path = Path('Assets/VoxelEngine/Storage/Runtime/RegionTable.cs')
t = region_table_path.read_text()
t = once(t,
'''        public NativeArray<int3> GetResidentCoords(Allocator allocator) =>
            _coordToSlot.GetKeyArray(allocator);

        public void Dispose()''',
'''        public NativeArray<int3> GetResidentCoords(Allocator allocator) =>
            _coordToSlot.GetKeyArray(allocator);

        public bool CopyResidentCoords(ref int cursor, NativeArray<int3> destination,
                                       out int count)
        {
            if (!destination.IsCreated || destination.Length == 0)
                throw new ArgumentException("Destination must contain at least one slot.",
                                            nameof(destination));
            cursor = math.clamp(cursor, 0, _regions.Length);
            count = 0;
            int slotsExamined = 0;
            while (cursor < _regions.Length && slotsExamined < destination.Length)
            {
                Region region = _regions[cursor++];
                slotsExamined++;
                if (!region.IsCreated) continue;
                destination[count++] = region.Coord;
            }
            return cursor >= _regions.Length;
        }

        public void Dispose()''', 'bounded region table scan')
region_table_path.write_text(t)

read_source_path = Path('Assets/VoxelEngine/Storage/Runtime/RegionReadSource.cs')
rs = read_source_path.read_text()
rs = once(rs,
'''        public NativeArray<int3> GetResidentRegionCoords(Allocator allocator) =>
            _table.GetResidentCoords(allocator);

        public bool TryAcquireRegionContainingBlock''',
'''        public NativeArray<int3> GetResidentRegionCoords(Allocator allocator) =>
            _table.GetResidentCoords(allocator);

        public bool CopyResidentRegionCoords(ref int cursor, NativeArray<int3> destination,
                                             out int count) =>
            _table.CopyResidentCoords(ref cursor, destination, out count);

        public bool TryAcquireRegionContainingBlock''', 'bounded region read source')
read_source_path.write_text(rs)


# -----------------------------------------------------------------------------
# Rendering scheduler: one bounded change batch/brick-expansion slice per frame.
# -----------------------------------------------------------------------------
scheduler_path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/VoxelSurfaceScheduler.cs')
s = scheduler_path.read_text()

# Preserve source compatibility for focused tests/tools that mean the near ring's shard count.
s = once(s,
'''        public const int NearSolidWorkerCount = 8;

        /// <summary>''',
'''        public const int NearSolidWorkerCount = 8;
        [Obsolete("Use NearSolidWorkerCount for the base ring or WorkerCountForSourceStep for a specific LOD.")]
        public const int SolidWorkerCount = NearSolidWorkerCount;

        /// <summary>''', 'legacy worker count alias')

s = once(s,
'''        private readonly List<VoxelChangeRecord> _changeScratch = new(256);
        private readonly HashSet<int3> _changedSolidRegions = new();''',
'''        private const int ChangeReadRecordsPerFrame = 64;
        private const int ChangeBrickExpansionsPerFrame = 256;
        private const int ChangeRecoverySlotsPerFrame = 32;
        private readonly List<VoxelChangeRecord> _changeScratch = new(ChangeReadRecordsPerFrame);
        private NativeArray<int3> _changeRecoveryRegions;
        private int _changeRecordIndex;
        private bool _changeFeedHasMore;
        private bool _recoveringChangeOverflow;
        private int _changeRecoveryCursor;
        private bool _changeExpansionActive;
        private int3 _changeExpansionMinBrick;
        private int3 _changeExpansionCounts;
        private int _changeExpansionCursor;
        private bool _changeExpansionAffectsSolids;
        private bool _changeExpansionAffectsWater;
        private readonly HashSet<int3> _changedSolidRegions = new();''', 'bounded change state')

s = s.replace('private readonly List<int3> _changedBricks = new(64);',
              'private readonly List<int3> _changedBricks = new(ChangeBrickExpansionsPerFrame);')
s = s.replace('private readonly List<int3> _changedWaterBricks = new(64);',
              'private readonly List<int3> _changedWaterBricks = new(ChangeBrickExpansionsPerFrame);')

s = once(s,
'''        public int LastVisibilityCandidateChecks => _lastVisibilityCandidateChecks;
        public int PendingSolidUploadBytes''',
'''        public int LastVisibilityCandidateChecks => _lastVisibilityCandidateChecks;
        public bool ChangeFeedBacklogged => _changeFeedHasMore
            || _changeRecordIndex < _changeScratch.Count || _changeExpansionActive;
        public bool RecoveringChangeFeedOverflow => _recoveringChangeOverflow;
        public int PendingSolidUploadBytes''', 'change backlog diagnostics')

s = once(s,
'''            _surfaceDiscoveryResults = new NativeList<int3>(
                VoxelReadGrid.BlocksPerRegion, Allocator.Persistent);
        }''',
'''            _surfaceDiscoveryResults = new NativeList<int3>(
                VoxelReadGrid.BlocksPerRegion, Allocator.Persistent);
            _changeRecoveryRegions = new NativeArray<int3>(
                ChangeRecoverySlotsPerFrame, Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
        }''', 'change recovery scratch allocation')

journal_start = s.index('            double journalStart = Time.realtimeSinceStartupAsDouble;')
journal_end = s.index('            _journalTiming.Add(ElapsedMs(journalStart));', journal_start)
old_journal = s[journal_start:journal_end]
new_journal = '''            double journalStart = Time.realtimeSinceStartupAsDouble;
            using (s_JournalMarker.Auto())
                ProcessChangeFeed(storage, journal);
'''
s = s[:journal_start] + new_journal + s[journal_end:]

# Add bounded change processor before discovery helper methods.
anchor = '        private void EnqueueSurfaceDiscovery(HashSet<int3> regions)\n'
change_methods = r'''        private void ProcessChangeFeed(IRegionReadSource storage, IVoxelChangeSource journal)
        {
            _lastChangeRecords = 0;
            if (journal == null)
            {
                ResetChangeFeedState(null);
                return;
            }

            if (!ReferenceEquals(journal, _journal))
                ResetChangeFeedState(journal);

            if (_recoveringChangeOverflow)
            {
                StepChangeOverflowRecovery(storage);
                return;
            }

            if (_changeRecordIndex >= _changeScratch.Count)
            {
                _changeScratch.Clear();
                _changeRecordIndex = 0;
                _changeExpansionActive = false;
                bool incremental = journal.ReadSince(
                    ref _changeCursor, _changeScratch, ChangeReadRecordsPerFrame,
                    out _changeFeedHasMore);
                if (!incremental)
                {
                    _changeScratch.Clear();
                    _changeRecordIndex = 0;
                    _changeExpansionActive = false;
                    _changeFeedHasMore = false;
                    _recoveringChangeOverflow = true;
                    _changeRecoveryCursor = 0;
                    StepChangeOverflowRecovery(storage);
                    return;
                }
            }

            StepChangeRecords();
        }

        private void ResetChangeFeedState(IVoxelChangeSource journal)
        {
            _journal = journal;
            _changeCursor = 0;
            _changeScratch.Clear();
            _changeRecordIndex = 0;
            _changeFeedHasMore = false;
            _recoveringChangeOverflow = false;
            _changeRecoveryCursor = 0;
            _changeExpansionActive = false;
            _changeExpansionCursor = 0;
        }

        private void StepChangeOverflowRecovery(IRegionReadSource storage)
        {
            bool complete = storage.CopyResidentRegionCoords(
                ref _changeRecoveryCursor, _changeRecoveryRegions, out int count);
            for (int i = 0; i < count; i++)
            {
                int3 region = _changeRecoveryRegions[i];
                _changedSolidRegions.Add(region);
                _changedWaterRegions.Add(region);
                _surfaceDiscoveryRegions.Add(region);
            }

            if (!complete) return;
            _recoveringChangeOverflow = false;
            _changeRecoveryCursor = 0;
        }

        private void StepChangeRecords()
        {
            int brickBudget = ChangeBrickExpansionsPerFrame;
            int recordBudget = ChangeReadRecordsPerFrame;
            while (_changeRecordIndex < _changeScratch.Count && recordBudget > 0)
            {
                VoxelChangeRecord change = _changeScratch[_changeRecordIndex];
                if (!_changeExpansionActive)
                {
                    bool affectsSolids = (change.Kind & (VoxelChangeKind.Occupancy
                        | VoxelChangeKind.BaseMaterial | VoxelChangeKind.SurfaceStyle
                        | VoxelChangeKind.Coating | VoxelChangeKind.Residency)) != 0;
                    bool affectsWater = (change.Kind & (VoxelChangeKind.Occupancy
                        | VoxelChangeKind.BaseMaterial | VoxelChangeKind.Water
                        | VoxelChangeKind.Residency)) != 0;
                    int3 extent = change.MaxVoxelExclusive - change.MinVoxel;
                    if (!affectsSolids && !affectsWater || math.any(extent <= 0))
                    {
                        _changeRecordIndex++;
                        _lastChangeRecords++;
                        recordBudget--;
                        continue;
                    }

                    if (math.any(extent >= VoxelGrid.RegionVoxelEdge))
                    {
                        if (affectsSolids) _changedSolidRegions.Add(change.Region);
                        if (affectsWater) _changedWaterRegions.Add(change.Region);
                        _surfaceDiscoveryRegions.Add(change.Region);
                        _changeRecordIndex++;
                        _lastChangeRecords++;
                        recordBudget--;
                        continue;
                    }

                    int3 minBrick = change.MinVoxel >> VoxelReadGrid.BlockEdgeLog2;
                    int3 maxBrick = (change.MaxVoxelExclusive - 1)
                                  >> VoxelReadGrid.BlockEdgeLog2;
                    _changeExpansionMinBrick = minBrick;
                    _changeExpansionCounts = maxBrick - minBrick + 1;
                    _changeExpansionCursor = 0;
                    _changeExpansionAffectsSolids = affectsSolids;
                    _changeExpansionAffectsWater = affectsWater;
                    _changeExpansionActive = true;
                }

                int total = _changeExpansionCounts.x
                          * _changeExpansionCounts.y
                          * _changeExpansionCounts.z;
                while (_changeExpansionCursor < total && brickBudget > 0)
                {
                    int linear = _changeExpansionCursor++;
                    int x = linear % _changeExpansionCounts.x;
                    int y = (linear / _changeExpansionCounts.x) % _changeExpansionCounts.y;
                    int z = linear / (_changeExpansionCounts.x * _changeExpansionCounts.y);
                    int3 brick = _changeExpansionMinBrick + new int3(x, y, z);
                    if (_changeExpansionAffectsSolids && _changedBrickSet.Add(brick))
                        _changedBricks.Add(brick);
                    if (_changeExpansionAffectsWater && _changedWaterBrickSet.Add(brick))
                        _changedWaterBricks.Add(brick);
                    brickBudget--;
                }

                if (_changeExpansionCursor < total) return;
                _changeExpansionActive = false;
                _changeExpansionCursor = 0;
                _changeRecordIndex++;
                _lastChangeRecords++;
                recordBudget--;
            }
        }

'''
s = once(s, anchor, change_methods + anchor, 'bounded change processor methods')

s = once(s,
'''            if (_surfaceDiscoveryResults.IsCreated) _surfaceDiscoveryResults.Dispose();
            if (_surfaceDiscoveryFlags.IsCreated) _surfaceDiscoveryFlags.Dispose();''',
'''            if (_surfaceDiscoveryResults.IsCreated) _surfaceDiscoveryResults.Dispose();
            if (_changeRecoveryRegions.IsCreated) _changeRecoveryRegions.Dispose();
            if (_surfaceDiscoveryFlags.IsCreated) _surfaceDiscoveryFlags.Dispose();''', 'change recovery scratch dispose')

scheduler_path.write_text(s)


# -----------------------------------------------------------------------------
# Regression tests + update stale LOD expectation.
# -----------------------------------------------------------------------------
arch_test_path = Path('Assets/Tests/EditMode/VoxelSurfaceArchitectureTests.cs')
a = arch_test_path.read_text()
if 'BoundedJournalReadsAdvanceIncrementally' not in a:
    marker = '''        [Test]
        public void SolidInvalidationIsBoundedToChangedChunkAndRequiredHalo()'''
    test = r'''        [Test]
        public void BoundedJournalReadsAdvanceIncrementallyAndPreserveOverflowSignal()
        {
            var journal = new VoxelChangeJournal(8);
            for (int i = 0; i < 5; i++)
                journal.PublishRegion(new int3(i, 0, 0), VoxelChangeKind.Occupancy);

            ulong cursor = 0;
            var records = new System.Collections.Generic.List<VoxelChangeRecord>();
            Assert.True(journal.ReadSince(ref cursor, records, 2, out bool hasMore));
            Assert.AreEqual(2, records.Count);
            Assert.AreEqual(2ul, cursor);
            Assert.True(hasMore);

            Assert.True(journal.ReadSince(ref cursor, records, 2, out hasMore));
            Assert.AreEqual(2, records.Count);
            Assert.AreEqual(4ul, cursor);
            Assert.True(hasMore);

            Assert.True(journal.ReadSince(ref cursor, records, 2, out hasMore));
            Assert.AreEqual(1, records.Count);
            Assert.AreEqual(5ul, cursor);
            Assert.False(hasMore);

            var tiny = new VoxelChangeJournal(2);
            tiny.PublishRegion(int3.zero);
            tiny.PublishRegion(new int3(1, 0, 0));
            tiny.PublishRegion(new int3(2, 0, 0));
            ulong stale = 0;
            Assert.False(tiny.ReadSince(ref stale, records, 1, out hasMore));
            Assert.AreEqual(0, records.Count,
                "A consumer that lost exact history should recover state, not copy unusable replay data.");
            Assert.AreEqual(tiny.CurrentVersion, stale);
            Assert.False(hasMore);
        }

'''
    a = once(a, marker, test + marker, 'bounded journal test insertion')
arch_test_path.write_text(a)

storage_test_path = Path('Assets/Tests/EditMode/StorageRenderingReadContractTests.cs')
st = storage_test_path.read_text()
# Preserve the step-8 regression fix in this older mapping test.
st = st.replace('Assert.AreEqual(0, VoxelReadGrid.LevelForStride(8));',
                'Assert.AreEqual(-1, VoxelReadGrid.LevelForStride(8));')
if 'ResidentRegionCopyCanBeConsumedInBoundedSlices' not in st:
    marker = '''        [Test]
        public void WorldBlockCoordinatesRemainCorrectAcrossNegativeRegions()'''
    test = r'''        [Test]
        public void ResidentRegionCopyCanBeConsumedInBoundedSlices()
        {
            var table = new RegionTable(4, Allocator.Persistent);
            var pool = new BrickPool(1, Allocator.Persistent);
            try
            {
                table.LoadRegion(new int3(0, 0, 0));
                table.LoadRegion(new int3(1, 0, 0));
                table.LoadRegion(new int3(2, 0, 0));
                var source = new RegionReadSource(in table, in pool);
                using var scratch = new NativeArray<int3>(1, Allocator.Temp);
                var seen = new System.Collections.Generic.HashSet<int3>();
                int cursor = 0;
                bool complete;
                int calls = 0;
                do
                {
                    complete = source.CopyResidentRegionCoords(ref cursor, scratch, out int count);
                    Assert.LessOrEqual(count, scratch.Length);
                    for (int i = 0; i < count; i++) seen.Add(scratch[i]);
                    calls++;
                    Assert.Less(calls, 16, "Bounded resident scan failed to make progress.");
                }
                while (!complete);

                Assert.AreEqual(3, seen.Count);
                Assert.GreaterOrEqual(calls, 3,
                    "A one-slot destination must not materialize the whole resident table at once.");
            }
            finally
            {
                if (table.IsCreated) table.Dispose();
                if (pool.IsCreated) pool.Dispose();
            }
        }

'''
    st = once(st, marker, test + marker, 'bounded region read test insertion')
storage_test_path.write_text(st)

pipeline_test_path = Path('Assets/Tests/EditMode/GeometryPipelineArchitectureTests.cs')
g = pipeline_test_path.read_text()
if 'ChangeJournalAndOverflowRecoveryAreFrameBounded' not in g:
    insert = r'''

        [Test]
        public void ChangeJournalAndOverflowRecoveryAreFrameBounded()
        {
            string scheduler = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "VoxelSurfaceScheduler.cs"));
            StringAssert.Contains("ChangeReadRecordsPerFrame", scheduler);
            StringAssert.Contains("ChangeBrickExpansionsPerFrame", scheduler);
            StringAssert.Contains("journal.ReadSince(", scheduler);
            StringAssert.Contains("ChangeReadRecordsPerFrame", scheduler);
            StringAssert.Contains("CopyResidentRegionCoords", scheduler);
            StringAssert.DoesNotContain("storage.GetResidentRegionCoords(Allocator.Temp)", scheduler);
        }
'''
    marker = '\n    }\n}'
    pos = g.rfind(marker)
    if pos < 0:
        raise SystemExit('pipeline test closing marker missing')
    g = g[:pos] + insert + g[pos:]
pipeline_test_path.write_text(g)

# Update the in-repo execution checklist for the slices that have actually landed.
doc_path = Path('docs/ASYNC_GEOMETRY_PIPELINE.md')
d = doc_path.read_text()
d = d.replace('- [x] Bound full-region invalidation with resumable candidate traversal; fine-grained brick edits remain immediate.\n',
              '- [x] Bound full-region invalidation with resumable candidate traversal; fine-grained brick edits remain immediate.\n'
              '- [x] Bound change-journal reads, record-to-brick expansion, and retention-overflow recovery.\n')
d = d.replace('- [ ] Replace `CollectVisible` scans of all known chunks with bounded ring/clipmap slot traversal.',
              '- [x] Replace `CollectVisible` scans of all known chunks with bounded ring/clipmap coordinate traversal.')
doc_path.write_text(d)

# Exact guards.
scheduler = scheduler_path.read_text()
assert 'journal.ReadSince(' in scheduler
assert 'ChangeReadRecordsPerFrame' in scheduler
assert 'ChangeBrickExpansionsPerFrame' in scheduler
assert 'storage.GetResidentRegionCoords(Allocator.Temp)' not in scheduler
assert 'CopyResidentRegionCoords' in scheduler
assert 'public const int SolidWorkerCount = NearSolidWorkerCount;' in scheduler
assert 'bool ReadSince(ref ulong cursor, List<VoxelChangeRecord> destination,\n                       int maxRecords, out bool hasMore);' in feed_path.read_text()
assert 'CopyResidentRegionCoords' in region_api_path.read_text()
assert 'Assert.AreEqual(-1, VoxelReadGrid.LevelForStride(8));' in storage_test_path.read_text()
