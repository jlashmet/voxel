using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Collision;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Asserts that visual and collision representations agree for the same world state.
    /// Both paths derive from the same authoritative storage data and shared DDA traversal.
    /// </summary>
    public sealed class VisualCollisionParityTests
    {
        private const int PoolCapacity = 2048;

        private BrickPool _pool;
        private RegionTable _table;
        private RegionReadSource _readSource;

        [SetUp]
        public void SetUp()
        {
            _pool = new BrickPool(PoolCapacity, Allocator.Persistent);
            _table = new RegionTable(4, Allocator.Persistent);
            _readSource = new RegionReadSource(in _table, in _pool);
        }

        [TearDown]
        public void TearDown()
        {
            _readSource = null;
            _table.Dispose();
            _pool.Dispose();
        }

        [Test]
        public void SingleBrickRaycastMatchesRaymarch()
        {
            var regionCoord = int3.zero;
            _table.LoadRegion(regionCoord);

            int3 targetBrick = new int3(0, 0, 0);
            int bx = 0, by = 0, bz = 0;
            regionCoord = int3.zero;

            var region = _table.LoadRegion(regionCoord);
            int brickIdx = Region.BrickIndex(bx, by, bz);
            region.BrickRefs[brickIdx] = BrickRef.Uniform(5);
            _table.CommitRegion(region);

            float3 origin = new float3(-10f, 5f, 5f);
            float3 direction = new float3(1f, -0.5f, -0.5f);

            bool hitColl = VoxelRaycast.Raycast(_readSource, origin, direction, out var collHit);
            (int3 hitBrick, int3 hitNormal, bool hitRender) = RaymarchHit(origin, direction);

            Assert.AreEqual(hitColl, hitRender,
                "Collision raycast and render raymarch must agree on whether a brick is hit.");

            if (hitColl && hitRender)
            {
                Assert.IsTrue(collHit.IsHit);
                int3 collBrick = collHit.Position;
                Assert.AreEqual(targetBrick.x, collBrick.x, "Collision hit X matches expected brick.");
                Assert.AreEqual(targetBrick.y, collBrick.y, "Collision hit Y matches expected brick.");
                Assert.AreEqual(targetBrick.z, collBrick.z, "Collision hit Z matches expected brick.");
                Assert.IsTrue(math.all(collHit.Position == hitBrick),
                    $"C-004 violation: collision hit {collHit.Position}, render hit {hitBrick}.");
            }
        }

        [Test]
        public void MultiBrickRaycastParity()
        {
            var regionCoord = int3.zero;
            var region = _table.LoadRegion(regionCoord);

            for (int z = 0; z < 2; z++)
            {
                int bx = z & 63;
                int by = (z >> 1) & 63;
                int bz = z >> 2;
                if (bx >= 64 || by >= 64 || bz >= 64) continue;

                int idx = Region.BrickIndex(bx, by, bz);
                region.BrickRefs[idx] = BrickRef.Uniform((byte)(z + 1));
            }

            int mixIdx = Region.BrickIndex(2 & 63, 2 & 63, 2 & 63);
            int poolSlot = _pool.Allocate();
            for (int v = 0; v < VoxelDimensions.VoxelsPerBrick; v++)
                _pool.SetVoxel(poolSlot, v, 7);
            region.BrickRefs[mixIdx] = BrickRef.FromPoolIndex(poolSlot);
            _table.CommitRegion(region);

            float3 origin = new float3(0.5f, 0.5f, 0.5f);
            float3 direction = new float3(1f, 1f, 1f);

            bool hitColl = VoxelRaycast.Raycast(_readSource, origin, direction, out var collHit);
            (int3 hitBrick, int3 hitNormal, bool hitRender) = RaymarchHit(origin, direction);

            Assert.IsTrue(hitColl && hitRender, "Both paths must find a brick.");
            Assert.IsTrue(collHit.IsHit);
            Assert.IsTrue(math.all(collHit.Position == hitBrick),
                $"C-004 violation: collision hit {collHit.Position}, render hit {hitBrick}.");
        }

        [Test]
        public void EmptyWorldBothMiss()
        {
            _table.LoadRegion(int3.zero);

            float3 origin = new float3(0f, 0f, 0f);
            float3 direction = new float3(1f, 0f, 0f);

            bool hitColl = VoxelRaycast.Raycast(_readSource, origin, direction, out var collHit);
            (int3 hitBrick, int3 hitNormal, bool hitRender) = RaymarchHit(origin, direction);

            Assert.IsFalse(hitColl);
            Assert.IsFalse(collHit.IsHit);
            Assert.IsFalse(hitRender);
        }

        [Test]
        public void NonResidentRegionsBothTreatAsEmpty()
        {
            _table.LoadRegion(int3.zero);

            float3 origin = new float3(64f * 100f - 0.5f, 64f * 50f - 0.5f, 64f * 50f - 0.5f);
            float3 direction = new float3(1f, 0f, 0f);

            bool hitColl = VoxelRaycast.Raycast(_readSource, origin, direction, out var collHit);
            (int3 hitBrick, int3 hitNormal, bool hitRender) = RaymarchHit(origin, direction);

            Assert.IsFalse(hitColl);
            Assert.IsFalse(collHit.IsHit);
            Assert.IsFalse(hitRender);
        }

        private (int3 brick, int3 entryNormal, bool hitFound) RaymarchHit(float3 origin, float3 direction)
        {
            int3 startBrick = new int3(
                (int)math.floor(origin.x),
                (int)math.floor(origin.y),
                (int)math.floor(origin.z));

            float3 normDir = math.normalize(direction);
            const float maxDistance = 10000f;

            int3 endBrick = new int3(
                (int)math.round(origin.x + normDir.x * maxDistance),
                (int)math.round(origin.y + normDir.y * maxDistance),
                (int)math.round(origin.z + normDir.z * maxDistance));

            var cursor = DdaTraversal.Cursor.Between(startBrick, endBrick);
            while (cursor.MoveNext())
            {
                if (IsBrickSolidAt(cursor.Current))
                    return (cursor.Current, cursor.EntryNormal, true);
            }

            return (int3.zero, int3.zero, false);
        }

        private bool IsBrickSolidAt(int3 brickCoord)
        {
            if (!_table.TryGetRegion(
                new int3(brickCoord.x >> VoxelDimensions.RegionEdgeLog2,
                         brickCoord.y >> VoxelDimensions.RegionEdgeLog2,
                         brickCoord.z >> VoxelDimensions.RegionEdgeLog2), out var region))
                return false;

            int bx = brickCoord.x & VoxelDimensions.RegionEdgeMask;
            int by = brickCoord.y & VoxelDimensions.RegionEdgeMask;
            int bz = brickCoord.z & VoxelDimensions.RegionEdgeMask;

            int idx = Region.BrickIndex(bx, by, bz);
            var brickRef = region.BrickRefs[idx];

            if (brickRef.IsUniform)
                return brickRef.UniformMaterial != VoxelDimensions.MaterialEmpty;

            if (!brickRef.IsMixed)
                return false;

            int occOffset = _pool.OccupancyOffset(brickRef.PoolIndex);
            ulong acc = 0UL;
            for (int w = 0; w < VoxelDimensions.OccupancyWordsPerBrick; w++)
                acc |= _pool.Occupancy[occOffset + w];

            return acc != 0UL;
        }
    }
}
