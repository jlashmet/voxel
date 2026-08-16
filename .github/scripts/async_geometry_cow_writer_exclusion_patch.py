from pathlib import Path


def once(text, old, new, label):
    n = text.count(old)
    if n != 1:
        raise SystemExit(f'{label}: expected one match, found {n}')
    return text.replace(old, new, 1)

pool_path = Path('Assets/VoxelEngine/Storage/Runtime/BrickPool.cs')
s = pool_path.read_text()
s = once(s,
'''        private NativeArray<int> _pinCounts;
        private NativeArray<uint> _slotGenerations;
        private NativeArray<byte> _retiredSlots;''',
'''        private NativeArray<int> _pinCounts;
        private NativeArray<uint> _slotGenerations;
        private NativeArray<byte> _retiredSlots;
        private NativeArray<byte> _writeBorrowedSlots;''', 'writer state field')
s = once(s,
'''            _retiredSlots = new NativeArray<byte>(capacity, allocator,
                                                  NativeArrayOptions.ClearMemory);
            _highWaterState''',
'''            _retiredSlots = new NativeArray<byte>(capacity, allocator,
                                                  NativeArrayOptions.ClearMemory);
            _writeBorrowedSlots = new NativeArray<byte>(capacity, allocator,
                                                        NativeArrayOptions.ClearMemory);
            _highWaterState''', 'writer state allocation')
s = once(s,
'''            _pinCounts[index] = 0;
            _retiredSlots[index] = 0;
            uint generation''',
'''            _pinCounts[index] = 0;
            _retiredSlots[index] = 0;
            _writeBorrowedSlots[index] = 0;
            uint generation''', 'writer state reset')
s = once(s,
'''            _retiredSlots[brickIndex] = 1;
            if (_pinCounts[brickIndex] == 0)
                _freeList.Add(brickIndex);''',
'''            _retiredSlots[brickIndex] = 1;
            if (_pinCounts[brickIndex] == 0 && _writeBorrowedSlots[brickIndex] == 0)
                _freeList.Add(brickIndex);''', 'defer free for writer')

old_pin = '''        /// <summary>Acquires an immutable read lease for one currently live mixed-brick slot.</summary>
        public PinToken Pin(int brickIndex)
        {
            ValidateAllocatedSlot(brickIndex);
            if (_retiredSlots[brickIndex] != 0)
                throw new InvalidOperationException($"Cannot pin retired brick slot {brickIndex}.");

            int count = _pinCounts[brickIndex];
            if (count == int.MaxValue)
                throw new InvalidOperationException($"Brick slot {brickIndex} pin count overflow.");
            _pinCounts[brickIndex] = count + 1;
            return new PinToken(brickIndex, _slotGenerations[brickIndex]);
        }
'''
new_pin = '''        /// <summary>
        /// Attempts to acquire an immutable read lease. A borrowed writer temporarily makes the
        /// slot unavailable rather than allowing a renderer/job to observe an in-progress edit.
        /// </summary>
        public bool TryPin(int brickIndex, out PinToken token)
        {
            ValidateAllocatedSlot(brickIndex);
            if (_retiredSlots[brickIndex] != 0 || _writeBorrowedSlots[brickIndex] != 0)
            {
                token = default;
                return false;
            }

            int count = _pinCounts[brickIndex];
            if (count == int.MaxValue)
                throw new InvalidOperationException($"Brick slot {brickIndex} pin count overflow.");
            _pinCounts[brickIndex] = count + 1;
            token = new PinToken(brickIndex, _slotGenerations[brickIndex]);
            return true;
        }

        /// <summary>Acquires an immutable read lease or throws when a writer owns the slot.</summary>
        public PinToken Pin(int brickIndex)
        {
            if (TryPin(brickIndex, out PinToken token)) return token;
            throw new InvalidOperationException(
                $"Cannot pin brick slot {brickIndex} while it is retired or write-borrowed.");
        }

        /// <summary>
        /// Marks a live unpinned slot as exclusively borrowed for direct mutation. The caller must
        /// pair this with <see cref="EndWrite"/> even if its owning region is evicted meanwhile.
        /// </summary>
        public void BeginWrite(int brickIndex)
        {
            ValidateAllocatedSlot(brickIndex);
            if (_retiredSlots[brickIndex] != 0 || _pinCounts[brickIndex] != 0
                || _writeBorrowedSlots[brickIndex] != 0)
                throw new InvalidOperationException(
                    $"Brick slot {brickIndex} is not available for exclusive mutation.");
            _writeBorrowedSlots[brickIndex] = 1;
        }

        public void EndWrite(int brickIndex)
        {
            ValidateAllocatedSlot(brickIndex);
            if (_writeBorrowedSlots[brickIndex] == 0)
                throw new InvalidOperationException(
                    $"Brick slot {brickIndex} has no active borrowed writer.");
            _writeBorrowedSlots[brickIndex] = 0;
            if (_retiredSlots[brickIndex] != 0 && _pinCounts[brickIndex] == 0)
                _freeList.Add(brickIndex);
        }
'''
s = once(s, old_pin, new_pin, 'writer-aware pin API')
s = once(s,
'''            if (count == 0 && _retiredSlots[token.Slot] != 0)
                _freeList.Add(token.Slot);''',
'''            if (count == 0 && _retiredSlots[token.Slot] != 0
                && _writeBorrowedSlots[token.Slot] == 0)
                _freeList.Add(token.Slot);''', 'unpin waits for writer')
s = once(s,
'''            if (_retiredSlots[brickIndex] != 0)
                throw new InvalidOperationException(
                    $"Cannot mutate retired brick slot {brickIndex}.");
            if (_pinCounts[brickIndex] == 0) return brickIndex;''',
'''            if (_retiredSlots[brickIndex] != 0)
                throw new InvalidOperationException(
                    $"Cannot mutate retired brick slot {brickIndex}.");
            if (_writeBorrowedSlots[brickIndex] != 0)
                throw new InvalidOperationException(
                    $"Brick slot {brickIndex} already has a borrowed writer.");
            if (_pinCounts[brickIndex] == 0) return brickIndex;''', 'EnsureWritable writer guard')
s = once(s,
'''            if (_retiredSlots.IsCreated) _retiredSlots.Dispose();
            if (_highWaterState.IsCreated) _highWaterState.Dispose();''',
'''            if (_retiredSlots.IsCreated) _retiredSlots.Dispose();
            if (_writeBorrowedSlots.IsCreated) _writeBorrowedSlots.Dispose();
            if (_highWaterState.IsCreated) _highWaterState.Dispose();''', 'writer state dispose')
pool_path.write_text(s)

# Borrowed block mutation owns the writer marker from materialisation until completion.
store_path = Path('Assets/VoxelEngine/Storage/Runtime/RegionMutationStore.cs')
r = store_path.read_text()
r = once(r,
'''            return new VoxelBlockMutation(
                _pool.Voxels,''',
'''            _pool.BeginWrite(poolIndex);
            return new VoxelBlockMutation(
                _pool.Voxels,''', 'begin borrowed writer')
# End the physical writer before any collapse/free and even if the region disappeared.
r = once(r,
'''        public bool CompletePartialBlock(ref VoxelBlockMutation mutation, bool payloadChanged)
        {
            if (!_table.TryGetRegion(mutation.RegionCoord, out Region region) || !region.BrickRefs.IsCreated)
            {
                mutation = default;
                return false;
            }

            bool changed''',
'''        public bool CompletePartialBlock(ref VoxelBlockMutation mutation, bool payloadChanged)
        {
            if (mutation.IsCreated)
                _pool.EndWrite(mutation.PoolIndex);

            if (!_table.TryGetRegion(mutation.RegionCoord, out Region region) || !region.BrickRefs.IsCreated)
            {
                mutation = default;
                return false;
            }

            bool changed''', 'end borrowed writer before completion')
store_path.write_text(r)

# Storage read source treats an active writer as a temporary unavailable snapshot, not an exception.
source_path = Path('Assets/VoxelEngine/Storage/Runtime/RegionReadSource.cs')
rs = source_path.read_text()
rs = once(rs,
'''            BrickPool.PinToken physicalPin = _pool.Pin(brick.PoolIndex);
            var apiPin = new VoxelReadPinToken(physicalPin.BrickIndex,''',
'''            if (!_pool.TryPin(brick.PoolIndex, out BrickPool.PinToken physicalPin))
            {
                block = default;
                return false;
            }
            var apiPin = new VoxelReadPinToken(physicalPin.BrickIndex,''', 'nonthrowing Storage read pin')
source_path.write_text(rs)

# Renderer retries the same block on a later frame if a scoped writer is active. Previously acquired
# pins remain valid and no fake empty descriptor is committed.
cache_path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs')
c = cache_path.read_text()
c = once(c,
'''        private int _pinnedReleaseCursor;
        private bool _discardBuildAfterPinRelease;
        private JobHandle _densityJobHandle;''',
'''        private int _pinnedReleaseCursor;
        private bool _discardBuildAfterPinRelease;
        private bool _snapshotPinUnavailable;
        private JobHandle _densityJobHandle;''', 'snapshot pin retry field')
old_loop = '''                    int3 worldBrick = cacheOrigin + new int3(x, y, z);
                    TransvoxelDensityBrick brick = SnapshotBlock(source, ref cursor, worldBrick);
                    _densityBricks[index] = brick;

                    bool ownsCore'''
new_loop = '''                    int3 worldBrick = cacheOrigin + new int3(x, y, z);
                    _snapshotPinUnavailable = false;
                    TransvoxelDensityBrick brick = SnapshotBlock(source, ref cursor, worldBrick);
                    if (_snapshotPinUnavailable)
                    {
                        AccumulateSnapshotSlice(sliceStart, completed: false);
                        return false;
                    }
                    _densityBricks[index] = brick;

                    bool ownsCore'''
c = once(c, old_loop, new_loop, 'snapshot retries active writer')
c = once(c,
'''            if (!source.TryPinWorldBlock(worldBlock, out PinnedVoxelReadBlock pinned)
                || pinned.Kind != VoxelReadBlockKind.Mixed || !pinned.HasPinnedPayload)
                throw new InvalidOperationException(
                    $"Failed to pin mixed Storage read block {worldBlock}.");''',
'''            if (!source.TryPinWorldBlock(worldBlock, out PinnedVoxelReadBlock pinned))
            {
                _snapshotPinUnavailable = true;
                return default;
            }
            if (pinned.Kind != VoxelReadBlockKind.Mixed || !pinned.HasPinnedPayload)
                throw new InvalidOperationException(
                    $"Storage changed mixed block kind without invalidating {worldBlock}.");''', 'snapshot nonthrowing pin retry')
c = once(c,
'''            _pinnedReadSource = null;
            _pinnedMixedVoxels = default;''',
'''            _pinnedReadSource = null;
            _snapshotPinUnavailable = false;
            _pinnedMixedVoxels = default;''', 'clear pin retry state')
cache_path.write_text(c)

# Behavioral tests.
test_path = Path('Assets/Tests/EditMode/StorageAllocationTests.cs')
t = test_path.read_text()
if 'BorrowedWriterBlocksPinsAndDefersRetiredSlotReuse' not in t:
    insert = r'''

        [Test]
        public void BorrowedWriterBlocksPinsAndDefersRetiredSlotReuse()
        {
            int slot = _pool.Allocate();
            _pool.FillBrick(slot, 4);
            _pool.BeginWrite(slot);
            Assert.False(_pool.TryPin(slot, out _));

            _pool.Free(slot);
            Assert.AreEqual(1, _pool.AllocatedCount,
                "A retired slot with an active writer must not be recycled.");
            _pool.EndWrite(slot);
            Assert.AreEqual(0, _pool.AllocatedCount);
        }
'''
    marker = '\n        [Test]\n        public void BrickRefEncodingRoundTrips()'
    pos = t.find(marker)
    if pos < 0: raise SystemExit('StorageAllocationTests insertion marker missing')
    t = t[:pos] + insert + t[pos:]
test_path.write_text(t)

arch_path = Path('Assets/Tests/EditMode/GeometryPipelineArchitectureTests.cs')
a = arch_path.read_text()
if 'PinnedGeometryNeverReadsBorrowedWriterPayloads' not in a:
    insert = r'''

        [Test]
        public void PinnedGeometryNeverReadsBorrowedWriterPayloads()
        {
            string pool = File.ReadAllText(Path.Combine(
                Application.dataPath, "VoxelEngine", "Storage", "Runtime", "BrickPool.cs"));
            string store = File.ReadAllText(Path.Combine(
                Application.dataPath, "VoxelEngine", "Storage", "Runtime", "RegionMutationStore.cs"));
            string cache = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "CpuTransvoxelChunkCache.cs"));
            StringAssert.Contains("_writeBorrowedSlots", pool);
            StringAssert.Contains("public bool TryPin", pool);
            StringAssert.Contains("_pool.BeginWrite(poolIndex)", store);
            StringAssert.Contains("_pool.EndWrite(mutation.PoolIndex)", store);
            StringAssert.Contains("_snapshotPinUnavailable", cache);
        }
'''
    marker = '\n    }\n}'
    pos = a.rfind(marker)
    if pos < 0: raise SystemExit('architecture test insertion marker missing')
    a = a[:pos] + insert + a[pos:]
arch_path.write_text(a)

# Progress doc.
doc_path = Path('docs/ASYNC_GEOMETRY_PIPELINE.md')
d = doc_path.read_text()
d = d.replace('  - [x] Read mixed exact-snapshot payloads directly from pinned COW Storage arrays instead of copying 8^3 payloads into renderer lists.\n',
'''  - [x] Read mixed exact-snapshot payloads directly from pinned COW Storage arrays instead of copying 8^3 payloads into renderer lists.
  - [x] Exclude scoped borrowed writers from read pins and defer retired-slot reuse until both readers and writers exit.
''', 1)
doc_path.write_text(d)

pool = pool_path.read_text()
assert 'public bool TryPin' in pool
assert '_writeBorrowedSlots' in pool
assert '_pool.BeginWrite(poolIndex);' in store_path.read_text()
assert '_pool.EndWrite(mutation.PoolIndex);' in store_path.read_text()
assert '_snapshotPinUnavailable' in cache_path.read_text()
