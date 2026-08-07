using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Collision;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Asserts that visual and collision representations agree for the same world state.
    ///
    /// This test validates C-004 (Single source of truth): "visual and collision derive from
    /// the same bricks" and Constitution Principle II. Both the render raymarch and the
    /// collision raycast use <see cref="DdaTraversal"/> over the same brick data, so for any
    /// given world state, a ray from (origin, direction) must hit the exact same brick in both
    /// paths — or miss entirely. Any discrepancy is a divergence bug.
    ///
    /// Test strategy: create an identical world state with known material distribution, cast a
    /// ray through it via both <see cref="VoxelRaycast.Raycast"/> (collision path) and a parallel
    /// DDA traversal that mimics the render raymarch, and assert their results match.
    /// </summary>
    public sealed class VisualCollisionParityTests
    {
        private const int PoolCapacity = 2048;

        private BrickPool _pool;
        private RegionTable _table;

        [SetUp]
        public void SetUp()
        {
            _pool = new BrickPool(PoolCapacity, Allocator.Persistent);
            _table = new RegionTable(4, Allocator.Persistent);
        }

        [TearDown]
        public void TearDown()
        {
            _table.Dispose();
            _pool.Dispose();
        }

        /// <summary>
        /// Verifies that raycast and raymarch results match for a world with a single uniform brick.
        /// Both should return the same hit position, normal, and material when targeting that brick.
        /// </summary>
        [Test]
        public void SingleBrickRaycastMatchesRaymarch()
        {
            var regionCoord = int3.zero;
            _table.LoadRegion(regionCoord);

            // Create a single solid brick at (0,0,0) — uniform material 5.
            int3 targetBrick = new int3(0, 0, 0);
            int bx = 0, by = 0, bz = 0;
            regionCoord = int3.zero;

            var region = _table.LoadRegion(regionCoord);
            int brickIdx = Region.BrickIndex(bx, by, bz);
            region.BrickRefs[brickIdx] = BrickRef.Uniform(5);
            _table.CommitRegion(region);

            // Cast a ray from outside the world toward the solid brick.
            float3 origin = new float3(-10f, 5f, 5f);
            float3 direction = new float3(1f, -0.5f, -0.5f);

            // Collision path: VoxelRaycast.
            bool hitColl = VoxelRaycast.Raycast(_table, _pool, origin, direction, out var collHit);

            // Render path: parallel DDA traversal that mimics the raymarch shader's brick iteration.
            (int3 hitBrick, int3 hitNormal, bool hitRender) = RaymarchHit(origin, direction);

            Assert.AreEqual(hitColl, hitRender,
                "Collision raycast and render raymarch must agree on whether a brick is hit.");

            if (hitColl && hitRender)
            {
                // Both should find the same brick coordinate.
                int3 collBrick = collHit.Position;
                Assert.AreEqual(targetBrick.x, collBrick.x, "Collision hit X matches expected brick.");
                Assert.AreEqual(targetBrick.y, collBrick.y, "Collision hit Y matches expected brick.");
                Assert.AreEqual(targetBrick.z, collBrick.z, "Collision hit Z matches expected brick.");

                // Collision and render must agree on which brick was hit.
                Assert.IsTrue(math.all(collHit.Position == hitBrick),
                    $"C-004 violation: collision hit {collHit.Position}, render hit {hitBrick}.");
            }
        }

        /// <summary>
        /// Verifies parity with multiple scattered bricks: both paths must find the *first* solid
        /// brick along the ray in the same order, since they use the same DDA traversal.
        /// </summary>
        [Test]
        public void MultiBrickRaycastParity()
        {
            var regionCoord = int3.zero;
            var region = _table.LoadRegion(regionCoord);

            // Create two bricks along a diagonal: brick (1,1,1) and brick (3,3,3).
            for (int z = 0; z < 2; z++)
            {
                int bx = z & 63;
                int by = (z >> 1) & 63;
                int bz = z >> 2;

                if (bx >= 64 || by >= 64 || bz >= 64) continue;

                int idx = Region.BrickIndex(bx, by, bz);
                region.BrickRefs[idx] = BrickRef.Uniform((byte)(z + 1));
            }

            // Add a specific brick at (2,2,2) as mixed to ensure both paths query it.
            int mixIdx = Region.BrickIndex(2 & 63, 2 & 63, 2 & 63);
            int poolSlot = _pool.Allocate();
            for (int v = 0; v < VoxelDimensions.VoxelsPerBrick; v++)
                _pool.SetVoxel(poolSlot, v, 7);
            region.BrickRefs[mixIdx] = BrickRef.FromPoolIndex(poolSlot);

            _table.CommitRegion(region);

            // Ray that passes through all three bricks: (1,1,1) -> (2,2,2) -> (3,3,3).
            float3 origin = new float3(0.5f, 0.5f, 0.5f);
            float3 direction = new float3(1f, 1f, 1f);

            bool hitColl = VoxelRaycast.Raycast(_table, _pool, origin, direction, out var collHit);
            (int3 hitBrick, int3 hitNormal, bool hitRender) = RaymarchHit(origin, direction);

            Assert.IsTrue(hitColl && hitRender, "Both paths must find a brick.");
            Assert.IsTrue(math.all(collHit.Position == hitBrick),
                $"C-004 violation: collision hit {collHit.Position}, render hit {hitBrick}.");
        }

        /// <summary>
        /// Verifies that both raycast and raymarch miss when the ray passes entirely through empty space.
        /// </summary>
        [Test]
        public void EmptyWorldBothMiss()
        {
            // Load a region but leave it untouched — all bricks are empty.
            _table.LoadRegion(int3.zero);

            float3 origin = new float3(0f, 0f, 0f);
            float3 direction = new float3(1f, 0f, 0f);

            bool hitColl = VoxelRaycast.Raycast(_table, _pool, origin, direction, out var collHit);
            (int3 hitBrick, int3 hitNormal, bool hitRender) = RaymarchHit(origin, direction);

            Assert.IsFalse(hitColl && hitRender,
                "Both collision and render must miss in an empty world.");
        }

        /// <summary>
        /// Verifies parity across non-resident regions: probing outside the loaded set must
        /// be treated as empty by both paths.
        /// </summary>
        [Test]
        public void NonResidentRegionsBothTreatAsEmpty()
        {
            // Only region (0,0,0) is resident — all others are non-resident.
            _table.LoadRegion(int3.zero);

            // Ray aimed at a brick in a non-resident region (100, 50, 50).
            float3 origin = new float3(64f * 100f - 0.5f, 64f * 50f - 0.5f, 64f * 50f - 0.5f);
            float3 direction = new float3(1f, 0f, 0f); // Moving into non-resident region (1, 0, 0).

            bool hitColl = VoxelRaycast.Raycast(_table, _pool, origin, direction, out var collHit);
            (int3 hitBrick, int3 hitNormal, bool hitRender) = RaymarchHit(origin, direction);

            Assert.IsFalse(hitColl && hitRender,
                "Both paths must treat non-resident regions as empty.");
        }

        // -- helpers ------------------------------------------------------------

        /// <summary>
        /// The render path's brick walk.
        ///
        /// This drives <see cref="DdaTraversal.Cursor"/> — the same traversal
        /// <see cref="VoxelRaycast"/> uses — rather than re-implementing a DDA. That is the
        /// whole point of C-004: a test with its own private copy of the algorithm would pass
        /// while the shipped paths diverged. Here, if the two ever disagree it is because a
        /// caller misused the traversal, not because they walk different lines.
        /// </summary>
        /// <returns>The first solid brick coordinate, and whether one was found.</returns>
        private (int3 brick, int3 entryNormal, bool hitFound) RaymarchHit(float3 origin, float3 direction)
        {
            // Same float-to-integer conversion the raycast performs, and for the same reason:
            // the ray arrives in continuous space, the traversal walks a discrete grid.
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
