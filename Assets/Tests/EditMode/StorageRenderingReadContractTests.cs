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

            // A stride equal to the block edge stays on exact voxel samples rather than
            // dropping to level 0. Level 0 is a conservative any-solid 8^3 summary, so
            // sampling it at an 8-voxel stride expands thin structures to whole cells and
            // closes architectural openings. The legacy mapping this test is named for
            // returned 0 here; that is the coarse-LOD regression, not the contract.
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
