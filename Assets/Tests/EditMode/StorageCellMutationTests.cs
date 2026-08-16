using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class StorageCellMutationTests
    {
        [Test]
        public void RegionReadViewPreservesAuthoredBoundaryOnEmptyCell()
        {
            var table = new RegionTable(1, Allocator.TempJob);
            var pool = new BrickPool(8, Allocator.TempJob);
            try
            {
                table.LoadRegion(int3.zero);
                int3 voxel = new int3(1, 2, 3);
                var authored = new VoxelCell
                {
                    BaseMaterialId = VoxelGrid.MaterialEmpty,
                    Surface = new VoxelSurfaceSemantics
                    {
                        StyleId = SurfaceStyles.Planar,
                        CoatingId = Coatings.Moss,
                        Detail = 7,
                    },
                    Boundary = VoxelBoundarySample.FromSignedQ4(-10, 1),
                };

                Assert.That(VoxelAccess.SetCell(ref table, ref pool, voxel, in authored), Is.True);

                var source = new RegionReadSource(in table, in pool);
                Assert.That(source.TryAcquireRegion(int3.zero, out RegionReadView view), Is.True);
                Assert.That(view.TryReadCell(voxel, out VoxelCell read), Is.True);
                Assert.That(read.BaseMaterialId, Is.EqualTo(VoxelGrid.MaterialEmpty));
                Assert.That(read.Surface.Packed, Is.Zero,
                    "Empty cells normalize presentation surface metadata away.");
                Assert.That(read.Boundary, Is.EqualTo(authored.Boundary),
                    "Authored signed boundary survives on the empty side of a surface.");
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }

        [Test]
        public void FullCellBlockMutationRoundTripsAllLogicalPayload()
        {
            var table = new RegionTable(1, Allocator.TempJob);
            var pool = new BrickPool(8, Allocator.TempJob);
            try
            {
                table.LoadRegion(int3.zero);
                var storage = new RegionMutationStore(in table, in pool);
                Assert.That(storage.TryBeginCellBlock(int3.zero, true, out VoxelBlockMutation mutation), Is.True);

                int voxelIndex = 1 | (2 << VoxelReadGrid.BlockEdgeLog2)
                                   | (3 << (VoxelReadGrid.BlockEdgeLog2 * 2));
                var authored = new VoxelCell
                {
                    BaseMaterialId = 5,
                    Surface = new VoxelSurfaceSemantics
                    {
                        StyleId = SurfaceStyles.Planar,
                        CoatingId = Coatings.Moss,
                        Flags = VoxelSurfaceFlags.PreserveFeature,
                        Detail = 9,
                    },
                    Boundary = VoxelBoundarySample.FromSignedQ4(14, 2),
                };

                Assert.That(mutation.SetCell(voxelIndex, in authored), Is.True);
                Assert.That(mutation.GetCell(voxelIndex), Is.EqualTo(authored));
                Assert.That(storage.CompletePartialBlock(ref mutation, true), Is.True);
                Assert.That(pool.AllocatedCount, Is.EqualTo(1),
                    "Authored surface/boundary payload keeps the logical block mixed.");

                VoxelCell read = VoxelAccess.GetCell(ref table, in pool, new int3(1, 2, 3));
                Assert.That(read, Is.EqualTo(authored));
                Assert.That(table.TryGetRegion(int3.zero, out Region region), Is.True);
                Assert.That(region.IsHardSurfaceBrick(0), Is.True);
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }

        [Test]
        public void BulkStoragePayloadCopyPreservesMixedBlockAndOccupancy()
        {
            var table = new RegionTable(1, Allocator.TempJob);
            var pool = new BrickPool(8, Allocator.TempJob);
            using var materials = new NativeArray<byte>(
                VoxelReadGrid.VoxelsPerBlock, Allocator.TempJob);
            using var surfaces = new NativeArray<ushort>(
                VoxelReadGrid.VoxelsPerBlock, Allocator.TempJob);
            using var boundaries = new NativeArray<byte>(
                VoxelReadGrid.VoxelsPerBlock, Allocator.TempJob);
            try
            {
                table.LoadRegion(int3.zero);
                var authoredSurface = new VoxelSurfaceSemantics
                {
                    StyleId = SurfaceStyles.Planar,
                    CoatingId = Coatings.Moss,
                    Flags = VoxelSurfaceFlags.PreserveFeature,
                    Detail = 11,
                };
                ushort packedSurface = authoredSurface.PackedStorage;

                for (int i = 0; i < VoxelReadGrid.VoxelsPerBlock; i++)
                {
                    bool solid = (i % 5) != 0;
                    materials[i] = solid ? (byte)7 : VoxelGrid.MaterialEmpty;
                    surfaces[i] = solid ? packedSurface : (ushort)0;
                    boundaries[i] = (byte)((i * 13) & 0xFF);
                }

                var storage = new RegionMutationStore(in table, in pool);
                Assert.That(storage.TryBeginCellBlock(
                    int3.zero, true, out VoxelBlockMutation mutation), Is.True);
                Assert.That(mutation.CopyStoragePayload(
                    materials, surfaces, boundaries, 0), Is.True);
                Assert.That(storage.CompletePartialBlock(ref mutation, true), Is.True);

                var source = new RegionReadSource(in table, in pool);
                Assert.That(source.TryPinWorldBlock(
                    int3.zero, out PinnedVoxelReadBlock block), Is.True);
                Assert.That(block.Kind, Is.EqualTo(VoxelReadBlockKind.Mixed));
                Assert.That(block.HasPinnedPayload, Is.True);
                try
                {
                    for (int i = 0; i < VoxelReadGrid.VoxelsPerBlock; i++)
                    {
                        int offset = block.MixedOffset + i;
                        Assert.That(block.MixedVoxels[offset], Is.EqualTo(materials[i]));
                        Assert.That(block.MixedSurfaceSemantics[offset], Is.EqualTo(surfaces[i]));
                        Assert.That(block.MixedBoundarySamples[offset], Is.EqualTo(boundaries[i]));
                    }
                }
                finally
                {
                    source.ReleasePinnedWorldBlock(in block.Pin);
                }

                using var occupied = new NativeArray<ulong>(
                    VoxelReadGrid.BlockSummaryWordCount, Allocator.TempJob);
                using var fullySolid = new NativeArray<ulong>(
                    VoxelReadGrid.BlockSummaryWordCount, Allocator.TempJob);
                Assert.That(source.TryCopyBlockSummary(
                    int3.zero, occupied, fullySolid, out _), Is.True);
                Assert.That((occupied[0] & 1UL) != 0UL, Is.True,
                    "Bulk copy must rebuild the block occupancy summary.");
                Assert.That((fullySolid[0] & 1UL) == 0UL, Is.True,
                    "Sparse empty cells must keep the mixed block from becoming fully solid.");
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }

        [Test]
        public void EmptyBoundaryKeepsBlockMixedUntilBoundaryIsRemoved()
        {
            var table = new RegionTable(1, Allocator.TempJob);
            var pool = new BrickPool(8, Allocator.TempJob);
            try
            {
                table.LoadRegion(int3.zero);
                var storage = new RegionMutationStore(in table, in pool);
                int voxelIndex = 4;

                Assert.That(storage.TryBeginCellBlock(int3.zero, false, out VoxelBlockMutation author), Is.True);
                var emptyBoundary = new VoxelCell
                {
                    BaseMaterialId = VoxelGrid.MaterialEmpty,
                    Boundary = VoxelBoundarySample.FromSignedQ4(-6),
                };
                Assert.That(author.SetCell(voxelIndex, in emptyBoundary), Is.True);
                Assert.That(storage.CompletePartialBlock(ref author, true), Is.True);
                Assert.That(pool.AllocatedCount, Is.EqualTo(1));

                Assert.That(storage.TryBeginCellBlock(int3.zero, false, out VoxelBlockMutation clear), Is.True);
                Assert.That(clear.GetCell(voxelIndex).Boundary.IsAuthored, Is.True);
                Assert.That(clear.SetCell(voxelIndex, default), Is.True);
                Assert.That(storage.CompletePartialBlock(ref clear, true), Is.True);
                Assert.That(pool.AllocatedCount, Is.Zero,
                    "Removing the last authored semantic payload allows uniform-empty collapse.");

                Assert.That(table.TryGetRegion(int3.zero, out Region region), Is.True);
                Assert.That(region.GetBrick(0, 0, 0).IsEmpty, Is.True);
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }
    }
}
