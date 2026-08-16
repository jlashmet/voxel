from pathlib import Path


def once(text, old, new, label):
    n = text.count(old)
    if n != 1:
        raise SystemExit(f'{label}: expected one match, found {n}')
    return text.replace(old, new, 1)

pool_path = Path('Assets/VoxelEngine/Storage/Runtime/BrickPool.cs')
s = pool_path.read_text()

s = once(s,
'''    public struct BrickPool : IDisposable
    {
        /// <summary>Voxel bytes: Capacity * 512, contiguous.</summary>''',
'''    public struct BrickPool : IDisposable
    {
        /// <summary>
        /// Generation-stamped lease for one immutable mixed-brick payload. Pins are acquired and
        /// released by the world owner; jobs consume the underlying arrays read-only. Generation
        /// prevents an old lease from unpinning a slot after it has been recycled (ABA).
        /// </summary>
        public readonly struct PinToken
        {
            internal readonly int Slot;
            public readonly uint Generation;
            public bool IsValid => Slot >= 0 && Generation != 0;
            public int BrickIndex => Slot;

            internal PinToken(int slot, uint generation)
            {
                Slot = slot;
                Generation = generation;
            }
        }

        /// <summary>Voxel bytes: Capacity * 512, contiguous.</summary>''', 'pin token')

s = once(s,
'''        private NativeList<int> _freeList;
        // Handle-like allocator state. BrickPool is copied into Storage capability objects, so
        // scalar allocator bookkeeping must live in shared native memory just like the payloads.
        private NativeArray<int> _highWaterState;''',
'''        private NativeList<int> _freeList;
        // Reader pins and slot generations live in shared native memory because BrickPool is a
        // copied handle type. A retired pinned slot remains allocated until its last reader exits.
        private NativeArray<int> _pinCounts;
        private NativeArray<uint> _slotGenerations;
        private NativeArray<byte> _retiredSlots;
        // Handle-like allocator state. BrickPool is copied into Storage capability objects, so
        // scalar allocator bookkeeping must live in shared native memory just like the payloads.
        private NativeArray<int> _highWaterState;''', 'cow allocator fields')

s = once(s,
'''            _freeList = new NativeList<int>(capacity >> 4, allocator);
            _highWaterState = new NativeArray<int>(1, allocator, NativeArrayOptions.ClearMemory);''',
'''            _freeList = new NativeList<int>(capacity >> 4, allocator);
            _pinCounts = new NativeArray<int>(capacity, allocator, NativeArrayOptions.ClearMemory);
            _slotGenerations = new NativeArray<uint>(capacity, allocator,
                                                     NativeArrayOptions.ClearMemory);
            _retiredSlots = new NativeArray<byte>(capacity, allocator,
                                                  NativeArrayOptions.ClearMemory);
            _highWaterState = new NativeArray<int>(1, allocator, NativeArrayOptions.ClearMemory);''', 'cow allocator allocation')

s = once(s,
'''            ClearBrick(index);
            return index;
        }''',
'''            _pinCounts[index] = 0;
            _retiredSlots[index] = 0;
            uint generation = _slotGenerations[index] + 1u;
            _slotGenerations[index] = generation == 0u ? 1u : generation;
            ClearBrick(index);
            return index;
        }''', 'generation on allocation')

old_free = '''        public void Free(int brickIndex)
        {
            if ((uint)brickIndex >= (uint)HighWater)
                throw new ArgumentOutOfRangeException(nameof(brickIndex));

            _freeList.Add(brickIndex);
        }
'''
new_free = '''        public void Free(int brickIndex)
        {
            ValidateAllocatedSlot(brickIndex);
            if (_retiredSlots[brickIndex] != 0) return;

            // A pinned slot is an immutable version still visible to a reader. Retire it now but
            // do not recycle its memory until the final reader releases the generation-stamped pin.
            _retiredSlots[brickIndex] = 1;
            if (_pinCounts[brickIndex] == 0)
                _freeList.Add(brickIndex);
        }

        /// <summary>True when at least one immutable reader still owns this live brick version.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsPinned(int brickIndex)
        {
            ValidateAllocatedSlot(brickIndex);
            return _pinCounts[brickIndex] > 0;
        }

        /// <summary>Acquires an immutable read lease for one currently live mixed-brick slot.</summary>
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

        /// <summary>
        /// Releases one immutable reader. A slot retired while pinned becomes recyclable exactly
        /// when the final reader exits; no frame-thread wait or reader synchronization is needed.
        /// </summary>
        public void Unpin(in PinToken token)
        {
            if (!token.IsValid || (uint)token.Slot >= (uint)HighWater)
                throw new ArgumentException("Invalid brick pin token.", nameof(token));
            if (_slotGenerations[token.Slot] != token.Generation)
                throw new InvalidOperationException(
                    $"Stale brick pin generation for slot {token.Slot}.");

            int count = _pinCounts[token.Slot];
            if (count <= 0)
                throw new InvalidOperationException($"Brick slot {token.Slot} is not pinned.");
            count--;
            _pinCounts[token.Slot] = count;
            if (count == 0 && _retiredSlots[token.Slot] != 0)
                _freeList.Add(token.Slot);
        }

        /// <summary>
        /// Returns a slot safe for mutation. Unpinned live bricks stay in place. A pinned brick is
        /// copied to a fresh live slot and the old version is retired until its readers release it.
        /// The caller must publish the returned slot in its BrickRef before mutating the payload.
        /// </summary>
        public int EnsureWritable(int brickIndex)
        {
            ValidateAllocatedSlot(brickIndex);
            if (_retiredSlots[brickIndex] != 0)
                throw new InvalidOperationException(
                    $"Cannot mutate retired brick slot {brickIndex}.");
            if (_pinCounts[brickIndex] == 0) return brickIndex;

            int clone = Allocate();
            CopyBrick(brickIndex, clone);
            Free(brickIndex);
            return clone;
        }

        /// <summary>Copies all physical payload planes between two allocated brick slots.</summary>
        private void CopyBrick(int source, int destination)
        {
            int sourceVoxel = VoxelOffset(source);
            int destinationVoxel = VoxelOffset(destination);
            NativeArray<byte>.Copy(Voxels, sourceVoxel, Voxels, destinationVoxel,
                                   VoxelDimensions.VoxelsPerBrick);
            NativeArray<ushort>.Copy(SurfaceSemantics, sourceVoxel,
                                     SurfaceSemantics, destinationVoxel,
                                     VoxelDimensions.VoxelsPerBrick);
            NativeArray<byte>.Copy(BoundarySamples, sourceVoxel,
                                   BoundarySamples, destinationVoxel,
                                   VoxelDimensions.VoxelsPerBrick);

            int sourceOccupancy = OccupancyOffset(source);
            int destinationOccupancy = OccupancyOffset(destination);
            NativeArray<ulong>.Copy(Occupancy, sourceOccupancy, Occupancy, destinationOccupancy,
                                    VoxelDimensions.OccupancyWordsPerBrick);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ValidateAllocatedSlot(int brickIndex)
        {
            if ((uint)brickIndex >= (uint)HighWater)
                throw new ArgumentOutOfRangeException(nameof(brickIndex));
        }
'''
s = once(s, old_free, new_free, 'pin/deferred-free methods')

s = once(s,
'''            if (_freeList.IsCreated) _freeList.Dispose();
            if (_highWaterState.IsCreated) _highWaterState.Dispose();''',
'''            if (_freeList.IsCreated) _freeList.Dispose();
            if (_pinCounts.IsCreated) _pinCounts.Dispose();
            if (_slotGenerations.IsCreated) _slotGenerations.Dispose();
            if (_retiredSlots.IsCreated) _retiredSlots.Dispose();
            if (_highWaterState.IsCreated) _highWaterState.Dispose();''', 'cow allocator dispose')

pool_path.write_text(s)

# Tests prove immutable old payload, deferred recycling, and generation ABA protection.
test_path = Path('Assets/Tests/EditMode/StorageAllocationTests.cs')
t = test_path.read_text()
if 'PinnedBrickUsesCopyOnWriteAndDefersRecycling' not in t:
    insert = r'''

        [Test]
        public void PinnedBrickUsesCopyOnWriteAndDefersRecycling()
        {
            int original = _pool.Allocate();
            _pool.FillBrick(original, 3);
            BrickPool.PinToken pin = _pool.Pin(original);

            int writable = _pool.EnsureWritable(original);
            Assert.AreNotEqual(original, writable,
                "A pinned version must never be mutated in place.");
            Assert.AreEqual(2, _pool.AllocatedCount,
                "The retired reader version stays allocated until its final pin releases.");
            Assert.AreEqual(3, _pool.GetVoxel(original, 0));
            Assert.AreEqual(3, _pool.GetVoxel(writable, 0));

            _pool.SetVoxel(writable, 0, 7);
            Assert.AreEqual(3, _pool.GetVoxel(original, 0),
                "Reader-visible payload changed after COW publication.");
            Assert.AreEqual(7, _pool.GetVoxel(writable, 0));

            _pool.Unpin(in pin);
            Assert.AreEqual(1, _pool.AllocatedCount,
                "Retired storage must become recyclable when its final reader exits.");
            _pool.Free(writable);
            Assert.AreEqual(0, _pool.AllocatedCount);
        }

        [Test]
        public void BrickPinGenerationRejectsAbaReuse()
        {
            int slot = _pool.Allocate();
            BrickPool.PinToken oldPin = _pool.Pin(slot);
            uint oldGeneration = oldPin.Generation;
            _pool.Unpin(in oldPin);
            _pool.Free(slot);

            int reused = _pool.Allocate();
            Assert.AreEqual(slot, reused, "Free-list reuse is expected for this ABA guard test.");
            BrickPool.PinToken newPin = _pool.Pin(reused);
            Assert.AreNotEqual(oldGeneration, newPin.Generation);
            _pool.Unpin(in newPin);
            _pool.Free(reused);
        }
'''
    marker = '\n        [Test]\n        public void BrickRefEncodingRoundTrips()'
    pos = t.find(marker)
    if pos < 0:
        raise SystemExit('StorageAllocationTests insertion marker missing')
    t = t[:pos] + insert + t[pos:]
test_path.write_text(t)

# Architecture guard: no rendering integration yet; pins are a Storage-owned foundation.
arch_path = Path('Assets/Tests/EditMode/GeometryPipelineArchitectureTests.cs')
a = arch_path.read_text()
if 'BrickPoolSupportsGenerationStampedCowReaders' not in a:
    insert = r'''

        [Test]
        public void BrickPoolSupportsGenerationStampedCowReaders()
        {
            string pool = File.ReadAllText(Path.Combine(
                Application.dataPath, "VoxelEngine", "Storage", "Runtime", "BrickPool.cs"));
            StringAssert.Contains("public readonly struct PinToken", pool);
            StringAssert.Contains("private NativeArray<int> _pinCounts", pool);
            StringAssert.Contains("private NativeArray<uint> _slotGenerations", pool);
            StringAssert.Contains("public int EnsureWritable", pool);
            StringAssert.Contains("CopyBrick(brickIndex, clone)", pool);
            StringAssert.Contains("if (count == 0 && _retiredSlots[token.Slot] != 0)", pool);
        }
'''
    marker = '\n    }\n}'
    pos = a.rfind(marker)
    if pos < 0:
        raise SystemExit('architecture test closing marker missing')
    a = a[:pos] + insert + a[pos:]
arch_path.write_text(a)

# Progress doc records foundation separately from rendering adoption.
doc_path = Path('docs/ASYNC_GEOMETRY_PIPELINE.md')
d = doc_path.read_text()
d = d.replace('- [ ] Add immutable/versioned or copy-on-write mixed-brick/page publication so snapshot copying can move off the frame thread.\n',
'''- [ ] Add immutable/versioned or copy-on-write mixed-brick/page publication so snapshot copying can move off the frame thread.
  - [x] Add generation-stamped mixed-brick pins, COW cloning, and deferred slot retirement in `BrickPool`.
  - [ ] Route every production mixed-brick mutation through `EnsureWritable` before rendering may pin payloads.
  - [ ] Expose bounded Storage snapshot leases to rendering and retire them after jobs complete.
''', 1)
doc_path.write_text(d)

pool = pool_path.read_text()
assert 'public readonly struct PinToken' in pool
assert 'public int EnsureWritable' in pool
assert 'private NativeArray<int> _pinCounts;' in pool
assert 'private NativeArray<uint> _slotGenerations;' in pool
assert 'if (_pinCounts[brickIndex] == 0) return brickIndex;' in pool
