using System.Diagnostics;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Runtime.Occupancy;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class StorageRegionReadViewTests
    {
        [Test]
        public void ReadViewPreservesUniformMixedSurfaceAndMipSemantics()
        {
            var table = new RegionTable(1, Allocator.Persistent);
            var pool = new BrickPool(4, Allocator.Persistent);
            var changes = new VoxelChangeJournal();

            try
            {
                int3 regionCoord = int3.zero;
                Region region = table.LoadRegion(regionCoord);

                int3 uniformBlock = new(1, 2, 3);
                region.SetBrick(uniformBlock.x, uniformBlock.y, uniformBlock.z,
                                BrickRef.Uniform(7));

                int mixedPoolIndex = pool.Allocate();
                pool.FillBrick(mixedPoolIndex, 4);
                int3 mixedBlock = new(4, 5, 6);
                region.SetBrick(mixedBlock.x, mixedBlock.y, mixedBlock.z,
                                BrickRef.FromPoolIndex(mixedPoolIndex));

                int3 inner = new(2, 3, 4);
                int voxelIndex = inner.x | (inner.y << 3) | (inner.z << 6);
                var authored = new VoxelCell
                {
                    BaseMaterialId = 9,
                    Surface = new VoxelSurfaceSemantics
                    {
                        StyleId = 12,
                        CoatingId = 3,
                        Flags = VoxelSurfaceFlags.PreserveFeature,
                        Detail = 5,
                    },
                    Boundary = new VoxelBoundarySample { Packed = 0xA7 },
                };
                pool.SetCell(mixedPoolIndex, voxelIndex, in authored);
                int waterVoxelIndex = 1 | (2 << 3) | (3 << 6);
                var water = new VoxelCell { BaseMaterialId = 11 };
                pool.SetCell(mixedPoolIndex, waterVoxelIndex, in water);

                int mixedBlockIndex = Region.BrickIndex(mixedBlock.x, mixedBlock.y, mixedBlock.z);
                Assert.IsTrue(region.MarkHardSurfaceBrick(mixedBlockIndex));

                region.AllocateMips(MipBuilder.MaxLevels, Allocator.Persistent);
                MipBuilder.RebuildRegion(in pool, ref region);
                table.CommitRegion(in region);
                changes.PublishRegion(regionCoord);

                var source = new RegionReadSource(in table, in pool, changes);
                Assert.AreEqual(1UL, source.Version);
                Assert.IsTrue(source.TryAcquireRegion(regionCoord, out RegionReadView view));
                Assert.AreEqual(regionCoord, view.RegionCoord);
                Assert.AreEqual(source.Version, view.Version);
                Assert.IsTrue(view.IsCreated);
                Assert.IsTrue(view.HasMips);

                Assert.IsTrue(view.TryGetBlock(uniformBlock, out VoxelReadBlock uniform));
                Assert.AreEqual(VoxelReadBlockKind.Uniform, uniform.Kind);
                Assert.AreEqual(7, uniform.UniformMaterial);
                Assert.IsTrue(view.IsBlockOccupied(uniformBlock));

                int3 uniformVoxel = uniformBlock * 8 + new int3(1, 1, 1);
                Assert.IsTrue(view.TryReadCell(uniformVoxel, out VoxelCell uniformCell));
                Assert.AreEqual(7, uniformCell.BaseMaterialId);
                Assert.AreEqual(0, uniformCell.Surface.PackedStorage);
                Assert.AreEqual(0, uniformCell.Boundary.Packed);

                Assert.IsTrue(view.TryGetBlock(mixedBlock, out VoxelReadBlock mixed));
                Assert.AreEqual(VoxelReadBlockKind.Mixed, mixed.Kind);
                Assert.IsTrue(view.IsBlockOccupied(mixedBlock));
                Assert.IsTrue(view.IsHardSurfaceBlock(mixedBlock));
                Assert.IsTrue(view.TryWorldBlockContainsEitherMaterial(
                    uniformBlock, 11, 16, out bool uniformContainsWater));
                Assert.IsFalse(uniformContainsWater);
                Assert.IsTrue(view.TryWorldBlockContainsEitherMaterial(
                    mixedBlock, 11, 16, out bool mixedContainsWater));
                Assert.IsTrue(mixedContainsWater);
                Assert.IsTrue(view.TryWorldBlockContainsEitherMaterial(
                    int3.zero, 11, 16, out bool emptyContainsWater));
                Assert.IsFalse(emptyContainsWater);

                int3 authoredVoxel = mixedBlock * 8 + inner;
                Assert.IsTrue(view.TryReadCell(authoredVoxel, out VoxelCell read));
                Assert.AreEqual(authored.BaseMaterialId, read.BaseMaterialId);
                Assert.AreEqual(authored.Surface.PackedStorage, read.Surface.PackedStorage);
                Assert.AreEqual(authored.Boundary.Packed, read.Boundary.Packed);

                using var copiedMaterials = new NativeArray<byte>(512, Allocator.Temp);
                using var copiedSurfaces = new NativeArray<ushort>(512, Allocator.Temp);
                using var copiedBoundaries = new NativeArray<byte>(512, Allocator.Temp);
                Assert.IsTrue(view.TryCopyMixedBlock(mixedBlock, copiedMaterials,
                                                     copiedSurfaces, copiedBoundaries, 0));
                Assert.AreEqual(authored.BaseMaterialId, copiedMaterials[voxelIndex]);
                Assert.AreEqual(authored.Surface.PackedStorage, copiedSurfaces[voxelIndex]);
                Assert.AreEqual(authored.Boundary.Packed, copiedBoundaries[voxelIndex]);

                Assert.IsTrue(view.TrySample(authoredVoxel, -1,
                                             out bool exactOccupied, out byte exactMaterial));
                Assert.IsTrue(exactOccupied);
                Assert.AreEqual(authored.BaseMaterialId, exactMaterial);

                Assert.IsTrue(view.TrySample(mixedBlock * 8, 0,
                                             out bool blockOccupied, out byte blockMaterial));
                Assert.IsTrue(blockOccupied);
                Assert.AreEqual(4, blockMaterial,
                    "Level zero must preserve MipBuilder's dominant-material rule.");

                Assert.IsTrue(view.TrySample(mixedBlock * 8, 1,
                                             out bool mipOccupied, out byte mipMaterial));
                Assert.IsTrue(mipOccupied);
                Assert.AreEqual(4, mipMaterial,
                    "Stored mip sampling must match the authoritative region pyramid.");
            }
            finally
            {
                if (table.IsCreated) table.Dispose();
                if (pool.IsCreated) pool.Dispose();
            }
        }

        [Test]
        public void BulkRegionSummaryPreservesEveryBrickStateWithinCompletionBudget()
        {
            var table = new RegionTable(1, Allocator.Persistent);
            var pool = new BrickPool(3, Allocator.Persistent);
            var region = new Region(int3.zero, Allocator.Persistent);

            try
            {
                int partialPool = pool.Allocate();
                pool.FillBrick(partialPool, VoxelGrid.MaterialEmpty);
                var partialCell = new VoxelCell { BaseMaterialId = 7 };
                pool.SetCell(partialPool, 0, in partialCell);

                int fullPool = pool.Allocate();
                pool.FillBrick(fullPool, 9);

                int emptyMixedPool = pool.Allocate();
                pool.FillBrick(emptyMixedPool, VoxelGrid.MaterialEmpty);

                int[] uniformSolid = { 0, 63, 64, VoxelDimensions.BricksPerRegion - 1 };
                for (int i = 0; i < uniformSolid.Length; i++)
                    region.BrickRefs[uniformSolid[i]] = BrickRef.Uniform(4);
                const int partial = 65;
                const int fullMixed = 129;
                const int emptyMixed = 130;
                region.BrickRefs[partial] = BrickRef.FromPoolIndex(partialPool);
                region.BrickRefs[fullMixed] = BrickRef.FromPoolIndex(fullPool);
                region.BrickRefs[emptyMixed] = BrickRef.FromPoolIndex(emptyMixedPool);

                for (int i = 0; i < Region.BlockSummaryWordCount; i++)
                {
                    region.OccupiedBlockWords[i] = ulong.MaxValue;
                    region.FullySolidBlockWords[i] = ulong.MaxValue;
                }

                var storage = new RegionMutationStore(in table, in pool);
                storage.RefreshRegionSummary(ref region);

                var expectedOccupied = new ulong[Region.BlockSummaryWordCount];
                var expectedFullySolid = new ulong[Region.BlockSummaryWordCount];
                for (int i = 0; i < uniformSolid.Length; i++)
                {
                    SetSummaryBit(expectedOccupied, uniformSolid[i]);
                    SetSummaryBit(expectedFullySolid, uniformSolid[i]);
                }
                SetSummaryBit(expectedOccupied, partial);
                SetSummaryBit(expectedOccupied, fullMixed);
                SetSummaryBit(expectedFullySolid, fullMixed);

                for (int i = 0; i < Region.BlockSummaryWordCount; i++)
                {
                    Assert.AreEqual(expectedOccupied[i], region.OccupiedBlockWords[i],
                        $"Occupied summary word {i} changed semantics.");
                    Assert.AreEqual(expectedFullySolid[i], region.FullySolidBlockWords[i],
                        $"Fully-solid summary word {i} changed semantics.");
                }

                var stopwatch = Stopwatch.StartNew();
                storage.RefreshRegionSummary(ref region);
                stopwatch.Stop();
                TestContext.WriteLine(
                    $"Bulk region summary: {stopwatch.Elapsed.TotalMilliseconds:F3} ms");
                Assert.Less(stopwatch.Elapsed.TotalMilliseconds, 25.0,
                    "The bulk summary catch-up must fit the traversal completion-frame budget.");
            }
            finally
            {
                if (region.IsCreated) region.Dispose();
                if (table.IsCreated) table.Dispose();
                if (pool.IsCreated) pool.Dispose();
            }
        }

        [Test]
        public void MissingRegionAndOutOfRangeReadsReturnFalse()
        {
            var table = new RegionTable(1, Allocator.Persistent);
            var pool = new BrickPool(1, Allocator.Persistent);
            try
            {
                var source = new RegionReadSource(in table, in pool);
                Assert.IsFalse(source.TryAcquireRegion(new int3(2, 0, 0), out _));

                Region region = table.LoadRegion(int3.zero);
                table.CommitRegion(in region);
                Assert.IsTrue(source.TryAcquireRegion(int3.zero, out RegionReadView view));
                Assert.IsFalse(view.TryReadCell(new int3(-1, 0, 0), out _));
                Assert.IsFalse(view.TryReadCell(new int3(VoxelGrid.RegionVoxelEdge, 0, 0), out _));
                Assert.IsFalse(view.TryGetBlock(new int3(64, 0, 0), out _));
                Assert.IsFalse(view.TryWorldBlockContainsEitherMaterial(
                    new int3(64, 0, 0), 11, 16, out _));
            }
            finally
            {
                if (table.IsCreated) table.Dispose();
                if (pool.IsCreated) pool.Dispose();
            }
        }

        private static void SetSummaryBit(ulong[] words, int blockIndex) =>
            words[blockIndex >> 6] |= 1UL << (blockIndex & 63);
    }
}
