from pathlib import Path

store_path = Path('Assets/VoxelEngine/Storage/Runtime/RegionMutationStore.cs')
s = store_path.read_text()
old = '''            int poolIndex;
            bool materializedUniform = false;
            if (original.IsUniform)
            {
                poolIndex = _pool.Allocate();
                _pool.FillBrick(poolIndex, original.UniformMaterial);
                Region writable = region;
                writable.BrickRefs[blockIndex] = BrickRef.FromPoolIndex(poolIndex);
                materializedUniform = true;
            }
            else
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

            _pool.BeginWrite(poolIndex);'''
new = '''            int poolIndex;
            bool materializedUniform = false;
            bool publishedPhysicalRef = false;
            Region writable = region;
            if (original.IsUniform)
            {
                poolIndex = _pool.Allocate();
                _pool.FillBrick(poolIndex, original.UniformMaterial);
                writable.BrickRefs[blockIndex] = BrickRef.FromPoolIndex(poolIndex);
                publishedPhysicalRef = true;
                materializedUniform = true;
            }
            else
            {
                poolIndex = _pool.EnsureWritable(original.PoolIndex);
                if (poolIndex != original.PoolIndex)
                {
                    // The NativeArray backing BrickRefs is shared by Region copies. Publish the
                    // COW version immediately and advance RegionTable's content revision before a
                    // long-lived borrowed writer can overlap an optimistic renderer metadata job.
                    writable.BrickRefs[blockIndex] = BrickRef.FromPoolIndex(poolIndex);
                    publishedPhysicalRef = true;
                }
            }

            if (publishedPhysicalRef)
                _table.CommitRegion(in writable);

            _pool.BeginWrite(poolIndex);'''
if s.count(old) != 1:
    raise SystemExit(f'MaterializeBlock anchor expected once, found {s.count(old)}')
s = s.replace(old, new, 1)
store_path.write_text(s)

# Focused behavior: the revision becomes stale at TryBegin time, not only Complete time.
test_path = Path('Assets/Tests/EditMode/StorageRenderingReadContractTests.cs')
t = test_path.read_text()
if 'BorrowedMutationInvalidatesPinnedRegionRevisionAtMaterialization' not in t:
    insert = r'''

        [Test]
        public void BorrowedMutationInvalidatesPinnedRegionRevisionAtMaterialization()
        {
            var table = new RegionTable(1, Allocator.Persistent);
            var pool = new BrickPool(4, Allocator.Persistent);
            try
            {
                Region region = table.LoadRegion(int3.zero);
                region.BrickRefs[0] = BrickRef.Uniform(4);
                table.CommitRegion(in region);
                var reads = new RegionReadSource(in table, in pool);
                var mutations = new RegionMutationStore(in table, in pool);

                Assert.True(reads.TryPinRegionBlockRefs(int3.zero,
                    out PinnedRegionBlockRefs metadata));
                Assert.True(reads.IsPinnedRegionCurrent(in metadata.Pin));

                Assert.True(mutations.TryBeginPartialBlock(
                    int3.zero, 5, false, out VoxelBlockMutation mutation));
                Assert.False(reads.IsPinnedRegionCurrent(in metadata.Pin),
                    "Publishing a materialized/COW BrickRef must advance the region revision "
                  + "before the borrowed mutation is completed.");

                Assert.True(mutations.CompletePartialBlock(ref mutation, payloadChanged: false));
                VoxelRegionPinToken token = metadata.Pin;
                reads.ReleasePinnedRegion(in token);
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
    if pos < 0:
        raise SystemExit('StorageRenderingReadContractTests insertion marker missing')
    t = t[:pos] + insert + t[pos:]
test_path.write_text(t)

arch_path = Path('Assets/Tests/EditMode/GeometryPipelineArchitectureTests.cs')
a = arch_path.read_text()
if 'BorrowedBrickRefPublicationAdvancesRegionRevisionImmediately' not in a:
    insert = r'''

        [Test]
        public void BorrowedBrickRefPublicationAdvancesRegionRevisionImmediately()
        {
            string store = File.ReadAllText(Path.Combine(
                Application.dataPath, "VoxelEngine", "Storage", "Runtime",
                "RegionMutationStore.cs"));
            int materialize = store.IndexOf("private VoxelBlockMutation MaterializeBlock",
                                            StringComparison.Ordinal);
            int end = store.IndexOf("private static byte DecodeUniformMaterial", materialize,
                                    StringComparison.Ordinal);
            Assert.GreaterOrEqual(materialize, 0);
            Assert.Greater(end, materialize);
            string method = store.Substring(materialize, end - materialize);
            StringAssert.Contains("publishedPhysicalRef", method);
            StringAssert.Contains("_table.CommitRegion(in writable)", method);
            Assert.Less(method.IndexOf("_table.CommitRegion(in writable)",
                                       StringComparison.Ordinal),
                        method.IndexOf("_pool.BeginWrite(poolIndex)", StringComparison.Ordinal));
        }
'''
    marker = '\n    }\n}'
    pos = a.rfind(marker)
    if pos < 0:
        raise SystemExit('architecture test closing marker missing')
    a = a[:pos] + insert + a[pos:]
arch_path.write_text(a)

assert 'if (publishedPhysicalRef)' in store_path.read_text()
assert '_table.CommitRegion(in writable);' in store_path.read_text()
