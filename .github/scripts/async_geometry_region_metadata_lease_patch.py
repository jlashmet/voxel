from pathlib import Path


def once(text, old, new, label):
    n = text.count(old)
    if n != 1:
        raise SystemExit(f'{label}: expected one match, found {n}')
    return text.replace(old, new, 1)

# -----------------------------------------------------------------------------
# Public Storage API: opaque region pin + job-friendly encoded block-ref view.
# -----------------------------------------------------------------------------
api_root = Path('Assets/VoxelEngine/Storage/Api')
lease_path = api_root / 'PinnedRegionBlockRefs.cs'
lease_path.write_text(r'''using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;

namespace VoxelEngine.Storage.Api
{
    /// <summary>
    /// Generation/revision token for one physically pinned region slot. Generation protects slot
    /// reuse; Revision detects any authoritative commit that raced an optimistic metadata job.
    /// </summary>
    public readonly struct VoxelRegionPinToken
    {
        internal readonly int Slot;
        public readonly uint Generation;
        public readonly uint Revision;
        public bool IsValid => Slot >= 0 && Generation != 0 && Revision != 0;

        internal VoxelRegionPinToken(int slot, uint generation, uint revision)
        {
            Slot = slot;
            Generation = generation;
            Revision = revision;
        }
    }

    /// <summary>
    /// Physically stable region block-reference storage for optimistic Burst metadata traversal.
    /// The encoded refs may change in place while the lease is pinned; consumers must therefore
    /// accept job output only when <c>IsPinnedRegionCurrent</c> still validates the token revision.
    /// Eviction is logical immediately but physical array disposal is deferred until release.
    /// </summary>
    public readonly struct PinnedRegionBlockRefs
    {
        public readonly int3 RegionCoord;
        public readonly NativeArray<int> EncodedBlockRefs;
        public readonly VoxelRegionPinToken Pin;

        public bool IsCreated => Pin.IsValid && EncodedBlockRefs.IsCreated;

        internal PinnedRegionBlockRefs(int3 regionCoord, NativeArray<int> encodedBlockRefs,
                                       in VoxelRegionPinToken pin)
        {
            RegionCoord = regionCoord;
            EncodedBlockRefs = encodedBlockRefs;
            Pin = pin;
        }
    }

    /// <summary>
    /// Stable decoder for Storage's compact block-reference representation. Rendering may consume
    /// encoded refs only through this helper; physical BrickRef remains a Storage.Runtime type.
    /// </summary>
    public static class VoxelReadBlockRefEncoding
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VoxelReadBlockKind Kind(int encoded) => encoded >= 0
            ? VoxelReadBlockKind.Mixed
            : encoded == -1 ? VoxelReadBlockKind.Empty : VoxelReadBlockKind.Uniform;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte UniformMaterial(int encoded) => (byte)(-encoded - 1);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int MixedPayloadOffset(int encoded) => encoded * VoxelReadGrid.VoxelsPerBlock;
    }
}
''')
(api_root / 'PinnedRegionBlockRefs.cs.meta').write_text(
    'fileFormatVersion: 2\nguid: d474b3f658864bf19156010f5a94794f\n')

api_path = api_root / 'IRegionReadSource.cs'
a = api_path.read_text()
a = once(a,
'''        void ReleasePinnedWorldBlock(in VoxelReadPinToken token);

        /// <summary>
        /// Copies compact per-block occupancy state''',
'''        void ReleasePinnedWorldBlock(in VoxelReadPinToken token);

        /// <summary>
        /// Pins the physical lifetime of one resident region's compact block-reference array for
        /// an optimistic job read. The job output is valid only if the token revision still passes
        /// <see cref="IsPinnedRegionCurrent"/> afterward. The backing array is Storage-owned.
        /// </summary>
        bool TryPinRegionBlockRefs(int3 regionCoord, out PinnedRegionBlockRefs region);

        /// <summary>Checks generation, logical residency and content revision for a pinned region.</summary>
        bool IsPinnedRegionCurrent(in VoxelRegionPinToken token);

        /// <summary>Releases a region metadata pin and completes deferred physical eviction if needed.</summary>
        void ReleasePinnedRegion(in VoxelRegionPinToken token);

        /// <summary>
        /// Copies compact per-block occupancy state''', 'region lease API')
api_path.write_text(a)

# -----------------------------------------------------------------------------
# RegionTable: shared generation/revision/pin state and deferred physical eviction.
# -----------------------------------------------------------------------------
table_path = Path('Assets/VoxelEngine/Storage/Runtime/RegionTable.cs')
s = table_path.read_text()
s = once(s,
'''        private NativeList<Region> _regions;
        private NativeList<int> _freeSlots;
        private readonly Allocator _allocator;''',
'''        private NativeList<Region> _regions;
        private NativeList<int> _freeSlots;
        private NativeList<int> _pinCounts;
        private NativeList<uint> _slotGenerations;
        private NativeList<uint> _contentRevisions;
        private NativeList<byte> _retiredSlots;
        private readonly Allocator _allocator;''', 'region lease state fields')
s = once(s,
'''            _regions = new NativeList<Region>(expectedResident, allocator);
            _freeSlots = new NativeList<int>(expectedResident >> 2, allocator);
            _lastCoord = default;''',
'''            _regions = new NativeList<Region>(expectedResident, allocator);
            _freeSlots = new NativeList<int>(expectedResident >> 2, allocator);
            _pinCounts = new NativeList<int>(expectedResident, allocator);
            _slotGenerations = new NativeList<uint>(expectedResident, allocator);
            _contentRevisions = new NativeList<uint>(expectedResident, allocator);
            _retiredSlots = new NativeList<byte>(expectedResident, allocator);
            _lastCoord = default;''', 'region lease state allocation')

# Copied RegionTable scalar caches must not resurrect a physically retained but logically evicted slot.
s = once(s,
'''                Region cached = _regions[_lastSlot];
                if (cached.IsCreated && cached.Coord.Equals(coord))
                {
                    region = cached;''',
'''                Region cached = _regions[_lastSlot];
                if (cached.IsCreated && cached.Coord.Equals(coord)
                    && _retiredSlots[_lastSlot] == 0)
                {
                    region = cached;''', 'TryGetRegion rejects retired cache')
s = once(s,
'''                Region cached = _regions[_lastSlot];
                if (cached.IsCreated && cached.Coord.Equals(coord))
                    return cached;''',
'''                Region cached = _regions[_lastSlot];
                if (cached.IsCreated && cached.Coord.Equals(coord)
                    && _retiredSlots[_lastSlot] == 0)
                    return cached;''', 'LoadRegion rejects retired cache')

# Initialize/reinitialize parallel slot state on load.
s = once(s,
'''                _freeSlots.RemoveAt(_freeSlots.Length - 1);
                _regions[slot] = region;
            }
            else
            {
                slot = _regions.Length;
                _regions.Add(region);
            }

            _coordToSlot.Add(coord, slot);''',
'''                _freeSlots.RemoveAt(_freeSlots.Length - 1);
                _regions[slot] = region;
                _pinCounts[slot] = 0;
                _retiredSlots[slot] = 0;
                uint generation = _slotGenerations[slot] + 1u;
                _slotGenerations[slot] = generation == 0u ? 1u : generation;
                _contentRevisions[slot] = 1u;
            }
            else
            {
                slot = _regions.Length;
                _regions.Add(region);
                _pinCounts.Add(0);
                _slotGenerations.Add(1u);
                _contentRevisions.Add(1u);
                _retiredSlots.Add(0);
            }

            _coordToSlot.Add(coord, slot);''', 'region load initializes lease state')

# Every authoritative commit advances a per-region revision even when only a block-ref changed.
s = once(s,
'''                _regions[slot] = region;
                _lastCoord = region.Coord;''',
'''                _regions[slot] = region;
                uint revision = _contentRevisions[slot] + 1u;
                _contentRevisions[slot] = revision == 0u ? 1u : revision;
                _lastCoord = region.Coord;''', 'region commit revision')

old_evict = '''        public void EvictRegion(int3 coord, ref BrickPool pool)
        {
            if (!_coordToSlot.TryGetValue(coord, out var slot)) return;

            var region = _regions[slot];
            region.ReleaseBricks(ref pool);
            region.Dispose();

            _regions[slot] = default;
            _freeSlots.Add(slot);
            _coordToSlot.Remove(coord);

            if (_hasLast && (_lastSlot == slot || _lastCoord.Equals(coord)))
            {
                _hasLast = false;
                _lastSlot = -1;
            }
        }
'''
new_evict = '''        public void EvictRegion(int3 coord, ref BrickPool pool)
        {
            if (!_coordToSlot.TryGetValue(coord, out var slot)) return;

            // Logical eviction is immediate. A pinned metadata job keeps only the physical Region
            // arrays alive; no normal lookup may observe this slot after removal from the map.
            _coordToSlot.Remove(coord);
            _retiredSlots[slot] = 1;
            if (_pinCounts[slot] == 0)
                ReleaseRetiredSlot(slot, ref pool);

            if (_hasLast && (_lastSlot == slot || _lastCoord.Equals(coord)))
            {
                _hasLast = false;
                _lastSlot = -1;
            }
        }

        internal bool TryPinRegion(int3 coord, out Region region,
                                   out int slot, out uint generation, out uint revision)
        {
            if (!_coordToSlot.TryGetValue(coord, out slot) || _retiredSlots[slot] != 0)
            {
                region = default;
                generation = 0;
                revision = 0;
                return false;
            }

            int pins = _pinCounts[slot];
            if (pins == int.MaxValue)
                throw new InvalidOperationException($"Region slot {slot} pin count overflow.");
            _pinCounts[slot] = pins + 1;
            generation = _slotGenerations[slot];
            revision = _contentRevisions[slot];
            region = _regions[slot];
            return true;
        }

        internal bool IsRegionPinCurrent(int slot, uint generation, uint revision)
        {
            return (uint)slot < (uint)_regions.Length
                && _slotGenerations[slot] == generation
                && _retiredSlots[slot] == 0
                && _contentRevisions[slot] == revision;
        }

        internal void UnpinRegion(int slot, uint generation, ref BrickPool pool)
        {
            if ((uint)slot >= (uint)_regions.Length || _slotGenerations[slot] != generation)
                throw new InvalidOperationException("Stale region pin generation.");
            int pins = _pinCounts[slot];
            if (pins <= 0)
                throw new InvalidOperationException($"Region slot {slot} is not pinned.");
            pins--;
            _pinCounts[slot] = pins;
            if (pins == 0 && _retiredSlots[slot] != 0)
                ReleaseRetiredSlot(slot, ref pool);
        }

        private void ReleaseRetiredSlot(int slot, ref BrickPool pool)
        {
            Region region = _regions[slot];
            if (region.IsCreated)
            {
                region.ReleaseBricks(ref pool);
                region.Dispose();
            }
            _regions[slot] = default;
            _retiredSlots[slot] = 0;
            _freeSlots.Add(slot);
        }
'''
s = once(s, old_evict, new_evict, 'deferred region eviction and pin API')

# Resident scan skips physically retained retired slots.
s = once(s,
'''                Region region = _regions[cursor++];
                slotsExamined++;
                if (!region.IsCreated) continue;
                destination[count++] = region.Coord;''',
'''                int slot = cursor++;
                Region region = _regions[slot];
                slotsExamined++;
                if (!region.IsCreated || _retiredSlots[slot] != 0) continue;
                destination[count++] = region.Coord;''', 'resident scan skips retired')

s = once(s,
'''            if (_coordToSlot.IsCreated) _coordToSlot.Dispose();
            if (_freeSlots.IsCreated) _freeSlots.Dispose();
            _hasLast = false;''',
'''            if (_coordToSlot.IsCreated) _coordToSlot.Dispose();
            if (_freeSlots.IsCreated) _freeSlots.Dispose();
            if (_pinCounts.IsCreated) _pinCounts.Dispose();
            if (_slotGenerations.IsCreated) _slotGenerations.Dispose();
            if (_contentRevisions.IsCreated) _contentRevisions.Dispose();
            if (_retiredSlots.IsCreated) _retiredSlots.Dispose();
            _hasLast = false;''', 'region lease state disposal')
table_path.write_text(s)

# -----------------------------------------------------------------------------
# RegionReadSource maps physical Runtime slot state to opaque API leases.
# -----------------------------------------------------------------------------
source_path = Path('Assets/VoxelEngine/Storage/Runtime/RegionReadSource.cs')
r = source_path.read_text()
anchor = '        public bool TryCopyBlockSummary(int3 regionCoord,\n'
pos = r.index(anchor)
methods = r'''        public bool TryPinRegionBlockRefs(int3 regionCoord, out PinnedRegionBlockRefs pinned)
        {
            if (!_table.TryPinRegion(regionCoord, out Region region,
                                     out int slot, out uint generation, out uint revision))
            {
                pinned = default;
                return false;
            }

            var token = new VoxelRegionPinToken(slot, generation, revision);
            pinned = new PinnedRegionBlockRefs(
                regionCoord, region.BrickRefs.Reinterpret<int>(), in token);
            return true;
        }

        public bool IsPinnedRegionCurrent(in VoxelRegionPinToken token) =>
            token.IsValid
            && _table.IsRegionPinCurrent(token.Slot, token.Generation, token.Revision);

        public void ReleasePinnedRegion(in VoxelRegionPinToken token)
        {
            if (!token.IsValid) return;
            _table.UnpinRegion(token.Slot, token.Generation, ref _pool);
        }

'''
r = r[:pos] + methods + r[pos:]
source_path.write_text(r)

# -----------------------------------------------------------------------------
# Behavioral tests.
# -----------------------------------------------------------------------------
test_path = Path('Assets/Tests/EditMode/StorageRenderingReadContractTests.cs')
t = test_path.read_text()
if 'PinnedRegionMetadataSurvivesPhysicalEvictionAndDetectsRevisionChanges' not in t:
    insert = r'''

        [Test]
        public void PinnedRegionMetadataSurvivesPhysicalEvictionAndDetectsRevisionChanges()
        {
            var table = new RegionTable(2, Allocator.Persistent);
            var pool = new BrickPool(2, Allocator.Persistent);
            try
            {
                Region region = table.LoadRegion(int3.zero);
                region.BrickRefs[0] = BrickRef.Uniform(3);
                table.CommitRegion(in region);
                var source = new RegionReadSource(in table, in pool);

                Assert.True(source.TryPinRegionBlockRefs(int3.zero, out PinnedRegionBlockRefs pinned));
                Assert.True(pinned.IsCreated);
                Assert.AreEqual(VoxelReadBlockKind.Uniform,
                    VoxelReadBlockRefEncoding.Kind(pinned.EncodedBlockRefs[0]));
                Assert.AreEqual(3, VoxelReadBlockRefEncoding.UniformMaterial(
                    pinned.EncodedBlockRefs[0]));
                Assert.True(source.IsPinnedRegionCurrent(in pinned.Pin));

                Assert.True(table.TryGetRegion(int3.zero, out Region changed));
                changed.BrickRefs[0] = BrickRef.Uniform(5);
                table.CommitRegion(in changed);
                Assert.False(source.IsPinnedRegionCurrent(in pinned.Pin),
                    "A region commit must invalidate optimistic metadata job output.");

                VoxelRegionPinToken token = pinned.Pin;
                table.EvictRegion(int3.zero, ref pool);
                Assert.False(source.IsRegionResident(int3.zero));
                Assert.True(pinned.EncodedBlockRefs.IsCreated,
                    "Physical block-ref storage was disposed while a job lease was pinned.");
                source.ReleasePinnedRegion(in token);

                Region replacement = table.LoadRegion(int3.zero);
                replacement.BrickRefs[0] = BrickRef.Uniform(7);
                table.CommitRegion(in replacement);
                Assert.True(source.TryPinRegionBlockRefs(int3.zero, out PinnedRegionBlockRefs next));
                Assert.AreNotEqual(token.Generation, next.Pin.Generation,
                    "Reused region slots must advance generation to prevent ABA.");
                VoxelRegionPinToken nextToken = next.Pin;
                source.ReleasePinnedRegion(in nextToken);
            }
            finally
            {
                if (table.IsCreated) table.Dispose();
                if (pool.IsCreated) pool.Dispose();
            }
        }
'''
    marker = '\n        [Test]\n        public void WorldBlockCoordinatesRemainCorrectAcrossNegativeRegions()'
    pos = t.find(marker)
    if pos < 0: raise SystemExit('StorageRenderingReadContractTests insertion marker missing')
    t = t[:pos] + insert + t[pos:]
test_path.write_text(t)

arch_path = Path('Assets/Tests/EditMode/GeometryPipelineArchitectureTests.cs')
a = arch_path.read_text()
if 'RegionMetadataLeasesAreVersionedAndEvictionSafe' not in a:
    insert = r'''

        [Test]
        public void RegionMetadataLeasesAreVersionedAndEvictionSafe()
        {
            string api = File.ReadAllText(Path.Combine(
                Application.dataPath, "VoxelEngine", "Storage", "Api", "IRegionReadSource.cs"));
            string table = File.ReadAllText(Path.Combine(
                Application.dataPath, "VoxelEngine", "Storage", "Runtime", "RegionTable.cs"));
            StringAssert.Contains("TryPinRegionBlockRefs", api);
            StringAssert.Contains("IsPinnedRegionCurrent", api);
            StringAssert.Contains("ReleasePinnedRegion", api);
            StringAssert.Contains("_contentRevisions", table);
            StringAssert.Contains("_retiredSlots", table);
            StringAssert.Contains("ReleaseRetiredSlot", table);
            StringAssert.Contains("_contentRevisions[slot] =", table);
        }
'''
    marker = '\n    }\n}'
    pos = a.rfind(marker)
    if pos < 0: raise SystemExit('architecture test closing marker missing')
    a = a[:pos] + insert + a[pos:]
arch_path.write_text(a)

# Progress doc.
doc_path = Path('docs/ASYNC_GEOMETRY_PIPELINE.md')
d = doc_path.read_text()
d = d.replace('  - [ ] Move compact block-kind/ref snapshot traversal itself off the frame thread with versioned job-safe region metadata.\n',
'''  - [ ] Move compact block-kind/ref snapshot traversal itself off the frame thread with versioned job-safe region metadata.
    - [x] Add generation/revision-pinned region block-ref leases and defer physical region eviction while jobs read metadata.
    - [ ] Schedule exact block-kind/ref classification in Burst and validate every pinned region revision before accepting output.
''', 1)
d = d.replace('- [ ] Replace global-world version dependence with region/brick dependency revisions where appropriate.\n',
'''- [ ] Replace global-world version dependence with region/brick dependency revisions where appropriate.
  - [x] Add per-region content revisions for optimistic rendering metadata jobs.
''', 1)
doc_path.write_text(d)

# Guards.
assert '_contentRevisions' in table_path.read_text()
assert 'TryPinRegion' in table_path.read_text()
assert 'ReleaseRetiredSlot' in table_path.read_text()
assert 'TryPinRegionBlockRefs' in api_path.read_text()
assert 'public bool TryPinRegionBlockRefs' in source_path.read_text()
