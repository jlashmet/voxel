using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Collision;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CollisionStorageBoundaryTests
    {
        [Test]
        public void RaycastMixedBlockUsesOccupancyWithoutExposingPoolIdentity()
        {
            var table = new RegionTable(1, Allocator.Persistent);
            var pool = new BrickPool(2, Allocator.Persistent);
            try
            {
                Region region = table.LoadRegion(int3.zero);
                int slot = pool.Allocate();
                pool.SetVoxel(slot, 0, 7);
                region.BrickRefs[Region.BrickIndex(0, 0, 0)] = BrickRef.FromPoolIndex(slot);
                table.CommitRegion(in region);

                var source = new RegionReadSource(in table, in pool);
                bool found = VoxelRaycast.Raycast(
                    source,
                    new float3(0.1f, 0.1f, 0.1f),
                    new float3(1f, 0f, 0f),
                    out HitInfo hit);

                Assert.IsTrue(found);
                Assert.IsTrue(hit.IsHit);
                Assert.AreEqual(int3.zero, hit.Position);
            }
            finally
            {
                if (table.IsCreated) table.Dispose();
                if (pool.IsCreated) pool.Dispose();
            }
        }

        [Test]
        public void HullOverloadsPreserveExistingMixedBlockRules()
        {
            var table = new RegionTable(1, Allocator.Persistent);
            var pool = new BrickPool(2, Allocator.Persistent);
            try
            {
                Region region = table.LoadRegion(int3.zero);
                int slot = pool.Allocate();
                region.BrickRefs[Region.BrickIndex(0, 0, 0)] = BrickRef.FromPoolIndex(slot);
                table.CommitRegion(in region);

                var source = new RegionReadSource(in table, in pool);
                Assert.IsTrue(source.TryAcquireRegion(int3.zero, out var view));

                using (NativeArray<float3> fullRegion = HullExport.ExportHulls(in view, Allocator.Temp))
                {
                    Assert.AreEqual(8, fullRegion.Length,
                        "Whole-region export historically treats any allocated mixed block as solid.");
                }

                using (NativeArray<float3> rangedEmpty = HullExport.ExportHulls(
                           source, int3.zero, int3.zero, Allocator.Temp))
                {
                    Assert.AreEqual(0, rangedEmpty.Length,
                        "Ranged export historically confirms mixed occupancy before including it.");
                }

                pool.SetVoxel(slot, 0, 9);
                using (NativeArray<float3> rangedOccupied = HullExport.ExportHulls(
                           source, int3.zero, int3.zero, Allocator.Temp))
                {
                    Assert.AreEqual(8, rangedOccupied.Length);
                }
            }
            finally
            {
                if (table.IsCreated) table.Dispose();
                if (pool.IsCreated) pool.Dispose();
            }
        }

        [Test]
        public void SweptAabbPreservesCurrentCoordinateMapping()
        {
            var table = new RegionTable(1, Allocator.Persistent);
            var pool = new BrickPool(1, Allocator.Persistent);
            try
            {
                Region region = table.LoadRegion(int3.zero);
                table.CommitRegion(in region);
                var source = new RegionReadSource(in table, in pool);

                CollisionResult empty = SweptAabb.Sweep(
                    source,
                    new float3(0f, 0f, 0f),
                    new float3(0.9f, 0.9f, 0.9f),
                    new float3(0.1f, 0f, 0f));
                Assert.AreEqual(0, empty.BlockedCount);

                region = table.LoadRegion(int3.zero);
                region.BrickRefs[Region.BrickIndex(0, 0, 0)] = BrickRef.Uniform(3);
                table.CommitRegion(in region);

                CollisionResult blocked = SweptAabb.Sweep(
                    source,
                    new float3(0f, 0f, 0f),
                    new float3(0.9f, 0.9f, 0.9f),
                    new float3(0.1f, 0f, 0f));

                Assert.AreEqual(1, blocked.BlockedCount,
                    "Architecture cutover must preserve the existing axis-by-axis sweep result.");
                Assert.IsTrue(blocked.BlockedX);
                Assert.IsFalse(blocked.BlockedY);
                Assert.IsFalse(blocked.BlockedZ);
                Assert.AreEqual(new float3(-1f, 0f, 0f), blocked.NormalX);
            }
            finally
            {
                if (table.IsCreated) table.Dispose();
                if (pool.IsCreated) pool.Dispose();
            }
        }
    }
}
