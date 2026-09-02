using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class StorageRenderingReadContractTests
    {
        [Test]
        public void WorldBlockCopyAndFullSolidPreserveLogicalValues()
        {
            var table = new RegionTable(2, Allocator.Persistent);
            var pool = new BrickPool(2, Allocator.Persistent);
            try
            {
                Region region = table.LoadRegion(int3.zero);
                int3 uniformBlock = new int3(2, 3, 4);
                region.BrickRefs[Region.BrickIndex(uniformBlock.x, uniformBlock.y, uniformBlock.z)] =
                    BrickRef.Uniform(6);

                int mixedSlot = pool.Allocate();
                pool.SetVoxel(mixedSlot, 0, 9);
                int3 mixedBlock = new int3(5, 6, 7);
                region.BrickRefs[Region.BrickIndex(mixedBlock.x, mixedBlock.y, mixedBlock.z)] =
                    BrickRef.FromPoolIndex(mixedSlot);
                table.CommitRegion(in region);

                var source = new RegionReadSource(in table, in pool);
                using NativeArray<int3> resident = source.GetResidentRegionCoords(Allocator.Temp);
                Assert.AreEqual(1, resident.Length);
                Assert.AreEqual(int3.zero, resident[0]);

                Assert.IsTrue(source.TryAcquireRegion(int3.zero, out RegionReadView view));
                Assert.IsTrue(view.ContainsWorldBlock(uniformBlock));
                Assert.IsTrue(view.IsWorldBlockFullySolid(uniformBlock));
                Assert.IsFalse(view.IsWorldBlockFullySolid(mixedBlock));

                using var materials = new NativeArray<byte>(VoxelReadGrid.VoxelsPerBlock, Allocator.Temp);
                using var surfaces = new NativeArray<ushort>(VoxelReadGrid.VoxelsPerBlock, Allocator.Temp);
                using var boundaries = new NativeArray<byte>(VoxelReadGrid.VoxelsPerBlock, Allocator.Temp);

                Assert.IsTrue(view.TryCopyWorldBlock(
                    uniformBlock, materials, surfaces, boundaries, 0));
                for (int i = 0; i < VoxelReadGrid.VoxelsPerBlock; i++)
                {
                    Assert.AreEqual(6, materials[i]);
                    Assert.AreEqual(0, surfaces[i]);
                    Assert.AreEqual(0, boundaries[i]);
                }

                Assert.IsTrue(view.TryCopyWorldBlock(
                    mixedBlock, materials, surfaces, boundaries, 0));
                Assert.AreEqual(9, materials[0]);
                for (int i = 1; i < VoxelReadGrid.VoxelsPerBlock; i++)
                    Assert.AreEqual(VoxelGrid.MaterialEmpty, materials[i]);
            }
            finally
            {
                if (table.IsCreated) table.Dispose();
                if (pool.IsCreated) pool.Dispose();
            }
        }

        [Test]
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


        [Test]
        public void PinnedMixedBlockRemainsImmutableAcrossAuthoritativeEdit()
        {
            var table = new RegionTable(2, Allocator.Persistent);
            var pool = new BrickPool(8, Allocator.Persistent);
            try
            {
                int3 voxel = new int3(3, 4, 5);
                Assert.True(VoxelAccess.SetVoxel(ref table, ref pool, voxel, 6));
                var source = new RegionReadSource(in table, in pool);
                int3 worldBlock = voxel >> VoxelReadGrid.BlockEdgeLog2;
                Assert.True(source.TryPinWorldBlock(worldBlock, out PinnedVoxelReadBlock pinned));
                Assert.AreEqual(VoxelReadBlockKind.Mixed, pinned.Kind);
                Assert.True(pinned.HasPinnedPayload);

                int3 inner = voxel & VoxelReadGrid.BlockEdgeMask;
                int voxelIndex = inner.x | (inner.y << 3) | (inner.z << 6);
                Assert.AreEqual(6, pinned.MixedVoxels[pinned.MixedOffset + voxelIndex]);

                Assert.True(VoxelAccess.SetVoxel(ref table, ref pool, voxel, 9));
                Assert.AreEqual(6, pinned.MixedVoxels[pinned.MixedOffset + voxelIndex],
                    "Pinned Storage payload changed after authoritative COW edit.");
                Assert.True(source.TryRead(voxel, out VoxelCell current));
                Assert.AreEqual(9, current.BaseMaterialId);

                VoxelReadPinToken token = pinned.Pin;
                source.ReleasePinnedWorldBlock(in token);
            }
            finally
            {
                if (table.IsCreated) table.Dispose();
                if (pool.IsCreated) pool.Dispose();
            }
        }

        [Test]
        public void UniformPinnedReadRequiresNoPhysicalLease()
        {
            var table = new RegionTable(1, Allocator.Persistent);
            var pool = new BrickPool(1, Allocator.Persistent);
            try
            {
                Region region = table.LoadRegion(int3.zero);
                region.BrickRefs[0] = BrickRef.Uniform(4);
                table.CommitRegion(in region);
                var source = new RegionReadSource(in table, in pool);
                Assert.True(source.TryPinWorldBlock(int3.zero, out PinnedVoxelReadBlock pinned));
                Assert.AreEqual(VoxelReadBlockKind.Uniform, pinned.Kind);
                Assert.AreEqual(4, pinned.UniformMaterial);
                Assert.False(pinned.HasPinnedPayload);
            }
            finally
            {
                if (table.IsCreated) table.Dispose();
                if (pool.IsCreated) pool.Dispose();
            }
        }


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

                Assert.False(mutations.CompletePartialBlock(ref mutation, payloadChanged: false),
                    "Unused materialisation should roll back without reporting an authoritative change.");
                VoxelRegionPinToken token = metadata.Pin;
                reads.ReleasePinnedRegion(in token);
            }
            finally
            {
                if (table.IsCreated) table.Dispose();
                if (pool.IsCreated) pool.Dispose();
            }
        }

        [Test]
        public void WorldBlockCoordinatesRemainCorrectAcrossNegativeRegions()
        {
            var table = new RegionTable(2, Allocator.Persistent);
            var pool = new BrickPool(1, Allocator.Persistent);
            try
            {
                int3 regionCoord = new int3(-1, 0, -1);
                Region region = table.LoadRegion(regionCoord);
                int edge = VoxelReadGrid.BlocksPerRegionEdge;
                int3 localBlock = new int3(edge - 1, 0, edge - 1);
                region.BrickRefs[Region.BrickIndex(localBlock.x, localBlock.y, localBlock.z)] =
                    BrickRef.Uniform(5);
                table.CommitRegion(in region);

                var source = new RegionReadSource(in table, in pool);
                int3 worldBlock = regionCoord * edge + localBlock;
                Assert.AreEqual(new int3(-1, 0, -1), worldBlock);
                Assert.IsTrue(source.TryAcquireRegionContainingBlock(worldBlock, out RegionReadView view));
                Assert.AreEqual(regionCoord, view.RegionCoord);
                Assert.IsTrue(view.ContainsWorldBlock(worldBlock));
                Assert.IsTrue(view.TryGetWorldBlock(worldBlock, out VoxelReadBlock block));
                Assert.AreEqual(VoxelReadBlockKind.Uniform, block.Kind);
                Assert.AreEqual(5, block.UniformMaterial);
            }
            finally
            {
                if (table.IsCreated) table.Dispose();
                if (pool.IsCreated) pool.Dispose();
            }
        }

        [Test]
        public void ReadGridStrideMappingMatchesLegacyMipGeometry()
        {
            Assert.AreEqual(-1, VoxelReadGrid.LevelForStride(1));
            Assert.AreEqual(-1, VoxelReadGrid.LevelForStride(2));
            Assert.AreEqual(-1, VoxelReadGrid.LevelForStride(4));
            Assert.AreEqual(-1, VoxelReadGrid.LevelForStride(8));
            Assert.AreEqual(1, VoxelReadGrid.LevelForStride(16));
            Assert.AreEqual(2, VoxelReadGrid.LevelForStride(32));
            Assert.AreEqual(3, VoxelReadGrid.LevelForStride(64));

            Assert.AreEqual(8, VoxelReadGrid.VoxelsPerCell(0));
            Assert.AreEqual(16, VoxelReadGrid.VoxelsPerCell(1));
            Assert.AreEqual(32, VoxelReadGrid.VoxelsPerCell(2));
        }
    }
}
