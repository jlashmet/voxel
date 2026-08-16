from pathlib import Path


def once(text, old, new, label):
    n = text.count(old)
    if n != 1:
        raise SystemExit(f'{label}: expected one match, found {n}')
    return text.replace(old, new, 1)

# VoxelAccess: compare first, then publish a writable clone before the first physical write.
path = Path('Assets/VoxelEngine/Storage/Runtime/VoxelAccess.cs')
s = path.read_text()
s = once(s,
'''            if (current.Equals(normalized)) return false;

            pool.SetCell(poolIndex, voxelIdx, in normalized);''',
'''            if (current.Equals(normalized)) return false;

            int writableIndex = pool.EnsureWritable(poolIndex);
            if (writableIndex != poolIndex)
            {
                // Publish the new live version before mutation. Readers pinned to the old slot
                // keep observing its immutable payload until they release their generation token.
                poolIndex = writableIndex;
                region.BrickRefs[brickIdx] = BrickRef.FromPoolIndex(poolIndex);
            }
            pool.SetCell(poolIndex, voxelIdx, in normalized);''', 'VoxelAccess COW publication')
path.write_text(s)

# RegionMutationStore: both whole-cell overwrite and borrowed block mutation must COW first.
path = Path('Assets/VoxelEngine/Storage/Runtime/RegionMutationStore.cs')
s = path.read_text()
s = once(s,
'''                if (current.IsMixed)
                {
                    poolIndex = current.PoolIndex;
                }
                else''',
'''                if (current.IsMixed)
                {
                    poolIndex = _pool.EnsureWritable(current.PoolIndex);
                    if (poolIndex != current.PoolIndex)
                        region.BrickRefs[blockIndex] = BrickRef.FromPoolIndex(poolIndex);
                }
                else''', 'whole-cell block COW')
s = once(s,
'''            else
            {
                poolIndex = original.PoolIndex;
            }

            return new VoxelBlockMutation(''',
'''            else
            {
                poolIndex = _pool.EnsureWritable(original.PoolIndex);
                if (poolIndex != original.PoolIndex)
                {
                    // The NativeArray backing BrickRefs is shared by Region copies, so publishing
                    // this ref is immediately visible even though no semantic metadata changed.
                    Region writable = region;
                    writable.BrickRefs[blockIndex] = BrickRef.FromPoolIndex(poolIndex);
                }
            }

            return new VoxelBlockMutation(''', 'borrowed mutation COW')
path.write_text(s)

# Showcase bulk destruction has two deliberate direct pool-mutation paths for batching. Make both
# publish a writable physical version before changing bytes. Fresh terrain-generation SetVoxel is
# intentionally untouched because it writes a newly allocated, unpublished slot.
path = Path('Assets/VoxelEngine/Composition/Showcase/ShowcaseWorld.cs')
s = path.read_text()
old = '''                if (_pool.GetVoxel(brick.PoolIndex, voxelIndex)
                    == VoxelDimensions.MaterialEmpty) continue;

                _pool.SetVoxel(brick.PoolIndex, voxelIndex, VoxelDimensions.MaterialEmpty);'''
new = '''                if (_pool.GetVoxel(brick.PoolIndex, voxelIndex)
                    == VoxelDimensions.MaterialEmpty) continue;

                int writableIndex = _pool.EnsureWritable(brick.PoolIndex);
                if (writableIndex != brick.PoolIndex)
                {
                    brick = BrickRef.FromPoolIndex(writableIndex);
                    region.BrickRefs[brickIndex] = brick;
                }
                _pool.SetVoxel(brick.PoolIndex, voxelIndex, VoxelDimensions.MaterialEmpty);'''
s = once(s, old, new, 'bulk clear COW')
old = '''                    for (int z = 0; z < VoxelDimensions.BrickEdge; z++)
                    for (int y = firstY; y < VoxelDimensions.BrickEdge; y++)
                    for (int x = 0; x < VoxelDimensions.BrickEdge; x++)
                    {
                        int index = VoxelEngine.Storage.Runtime.Occupancy.OccupancyMask.VoxelIndex(x, y, z);'''
new = '''                    int writableIndex = _pool.EnsureWritable(brick.PoolIndex);
                    if (writableIndex != brick.PoolIndex)
                    {
                        brick = BrickRef.FromPoolIndex(writableIndex);
                        region.BrickRefs[brickIndex] = brick;
                    }

                    for (int z = 0; z < VoxelDimensions.BrickEdge; z++)
                    for (int y = firstY; y < VoxelDimensions.BrickEdge; y++)
                    for (int x = 0; x < VoxelDimensions.BrickEdge; x++)
                    {
                        int index = VoxelEngine.Storage.Runtime.Occupancy.OccupancyMask.VoxelIndex(x, y, z);'''
s = once(s, old, new, 'collapse COW')
path.write_text(s)

# Behavioral test through public VoxelAccess proves an in-flight pinned version is immutable while
# the authoritative Region ref atomically moves to a writable clone.
test_path = Path('Assets/Tests/EditMode/StorageAllocationTests.cs')
t = test_path.read_text()
if 'VoxelAccessPublishesCowCloneBeforeEditingPinnedBrick' not in t:
    insert = r'''

        [Test]
        public void VoxelAccessPublishesCowCloneBeforeEditingPinnedBrick()
        {
            int3 voxel = new int3(1, 2, 3);
            Assert.True(VoxelAccess.SetVoxel(ref _table, ref _pool, voxel, 5));
            VoxelAccess.Decompose(voxel, out int3 regionCoord,
                                  out int3 brickInRegion, out int3 voxelInBrick);
            Assert.True(_table.TryGetRegion(regionCoord, out Region before));
            int blockIndex = Region.BrickIndex(brickInRegion.x, brickInRegion.y, brickInRegion.z);
            int oldSlot = before.BrickRefs[blockIndex].PoolIndex;
            int voxelIndex = VoxelEngine.Storage.Runtime.Occupancy.OccupancyMask.VoxelIndex(
                voxelInBrick.x, voxelInBrick.y, voxelInBrick.z);
            BrickPool.PinToken pin = _pool.Pin(oldSlot);

            Assert.True(VoxelAccess.SetVoxel(ref _table, ref _pool, voxel, 7));
            Assert.True(_table.TryGetRegion(regionCoord, out Region after));
            int newSlot = after.BrickRefs[blockIndex].PoolIndex;
            Assert.AreNotEqual(oldSlot, newSlot);
            Assert.AreEqual(5, _pool.GetVoxel(oldSlot, voxelIndex),
                "Pinned reader version was mutated in place.");
            Assert.AreEqual(7, _pool.GetVoxel(newSlot, voxelIndex));

            _pool.Unpin(in pin);
        }
'''
    marker = '\n        [Test]\n        public void BrickRefEncodingRoundTrips()'
    pos = t.find(marker)
    if pos < 0:
        raise SystemExit('StorageAllocationTests insertion marker missing')
    t = t[:pos] + insert + t[pos:]
test_path.write_text(t)

# Source-level guard covers the block mutation API and both showcase batching paths.
arch_path = Path('Assets/Tests/EditMode/GeometryPipelineArchitectureTests.cs')
a = arch_path.read_text()
if 'ProductionMixedBrickMutationsPublishCowVersions' not in a:
    insert = r'''

        [Test]
        public void ProductionMixedBrickMutationsPublishCowVersions()
        {
            string storageRoot = Path.Combine(Application.dataPath, "VoxelEngine", "Storage", "Runtime");
            string voxelAccess = File.ReadAllText(Path.Combine(storageRoot, "VoxelAccess.cs"));
            string mutationStore = File.ReadAllText(Path.Combine(storageRoot, "RegionMutationStore.cs"));
            string showcase = File.ReadAllText(Path.Combine(
                Application.dataPath, "VoxelEngine", "Composition", "Showcase", "ShowcaseWorld.cs"));
            StringAssert.Contains("pool.EnsureWritable(poolIndex)", voxelAccess);
            Assert.GreaterOrEqual(CountOccurrences(mutationStore, "_pool.EnsureWritable("), 2);
            Assert.GreaterOrEqual(CountOccurrences(showcase, "_pool.EnsureWritable(brick.PoolIndex)"), 2);
        }
'''
    # Add a tiny helper if this file does not already have one.
    if 'private static int CountOccurrences(' not in a:
        helper = r'''

        private static int CountOccurrences(string text, string value)
        {
            int count = 0;
            int offset = 0;
            while ((offset = text.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
            {
                count++;
                offset += value.Length;
            }
            return count;
        }
'''
        marker = '\n    }\n}'
        pos = a.rfind(marker)
        if pos < 0:
            raise SystemExit('architecture test helper marker missing')
        a = a[:pos] + helper + a[pos:]
    marker = '\n    }\n}'
    pos = a.rfind(marker)
    if pos < 0:
        raise SystemExit('architecture test insertion marker missing')
    a = a[:pos] + insert + a[pos:]
arch_path.write_text(a)

# Mark mutation adoption complete, but snapshot leasing itself remains open.
doc_path = Path('docs/ASYNC_GEOMETRY_PIPELINE.md')
d = doc_path.read_text()
d = d.replace('  - [ ] Route every production mixed-brick mutation through `EnsureWritable` before rendering may pin payloads.\n',
              '  - [x] Route every production mixed-brick mutation through `EnsureWritable` before rendering may pin payloads.\n', 1)
doc_path.write_text(d)

assert 'pool.EnsureWritable(poolIndex)' in Path('Assets/VoxelEngine/Storage/Runtime/VoxelAccess.cs').read_text()
assert Path('Assets/VoxelEngine/Storage/Runtime/RegionMutationStore.cs').read_text().count('_pool.EnsureWritable(') >= 2
assert Path('Assets/VoxelEngine/Composition/Showcase/ShowcaseWorld.cs').read_text().count('_pool.EnsureWritable(brick.PoolIndex)') >= 2
