using VoxelEngine.Edits.Api;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Edits.Runtime;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Storage.Api;
using VoxelEngine.Net.Runtime.Client;

namespace VoxelEngine.Tests.Parity
{
    /// <summary>
    /// Acceptance test for Scenario 1: two players observing the same wall see the same
    /// section removed and can move through the gap.
    ///
    /// This is US1's independent test: identical geometry AND traversability across two
    /// clients. Geometry parity ensures both see the same hole; traversability ensures
    /// collision agrees (C-004, Constitution Principle II).
    /// </summary>
    public sealed class SharedDestructionTests
    {
        [Test]
        [Category("US1")]
        [Category("Scenario 1")]
        public void IdenticalWallProducesIdenticalHoleAcrossTwoClients()
        {
            // Setup: two clients with identical worlds (a flat wall of material 5).
            var poolA = new BrickPool(4096, Allocator.Persistent);
            var poolB = new BrickPool(4096, Allocator.Persistent);

            var tableA = new RegionTable(1, Allocator.Persistent);
            var tableB = new RegionTable(1, Allocator.Persistent);

            // Build a wall at Z=256 spanning X: 200–300, Y: 100–400. The upper bound has to
            // match the solidity assertion below: built to 300 while asserted to 400, the wall
            // is simply absent above y≈304 (brick-granular fill carries it a little past the
            // build bound) and the test fails before reaching the destruction it exists to test.
            BuildWall(ref poolA, ref tableA, new int3(200, 100, 256), 100, 400);
            BuildWall(ref poolB, ref tableB, new int3(200, 100, 256), 100, 400);
            var storageA = new RegionMutationStore(in tableA, in poolA);
            var storageB = new RegionMutationStore(in tableB, in poolB);

            // The wall must be solid: every voxel on the wall is material 5.
            for (int x = 200; x < 300; x++)
            {
                for (int y = 100; y < 400; y++)
                {
                    Assert.AreEqual(5, VoxelAccess.GetVoxel(ref tableA, in poolA, new int3(x, y, 256)));
                    Assert.AreEqual(5, VoxelAccess.GetVoxel(ref tableA, in poolA, new int3(x, y, 256)));
                }
            }

            // Simulate the same destruction event on both clients.
            var evt = new AlterationEvent
            {
                tick = 1u,
                kind = (byte)VoxelEngine.Edits.Api.AlterationEventKind.Explosion,
                origin = new int3(256, 250, 256),
                shapeData = 30,
                material = VoxelDimensions.MaterialEmpty,
                seed = 42u,
                playerId = 1, sequence = 1,
            };

            var resultA = EventApplication.Apply(new DeterministicAlterationApplier(), storageA, in evt, out _);
            var resultB = EventApplication.Apply(new DeterministicAlterationApplier(), storageB, in evt, out _);

            // Both must report changes (the explosion hits the wall).
            Assert.IsTrue(resultA, "Client A: destruction should have changed the world.");
            Assert.IsTrue(resultB, "Client B: destruction should have changed the world.");

            // Geometry parity: every voxel in the affected region must be identical.
            int3 boundMin = evt.origin - new int3(evt.Radius(), evt.Radius(), evt.Radius());
            int3 boundMax = evt.origin + new int3(evt.Radius(), evt.Radius(), evt.Radius());

            for (int x = boundMin.x; x <= boundMax.x; x++)
            {
                for (int y = boundMin.y; y <= boundMax.y; y++)
                {
                    for (int z = boundMin.z; z <= boundMax.z; z++)
                    {
                        var voxelA = VoxelAccess.GetVoxel(ref tableA, in poolA, new int3(x, y, z));
                        var voxelB = VoxelAccess.GetVoxel(ref tableB, in poolB, new int3(x, y, z));
                        Assert.AreEqual(voxelA, voxelB,
                            $"Voxel({x},{y},{z}) differs: A={voxelA}, B={voxelB}");
                    }
                }
            }

            // Traversability: the gap in the wall must be passable for both clients.
            int3 startLeft = new int3(200, 250, 256);
            int3 endRight = new int3(300, 250, 256);

            Assert.IsFalse(WouldCollide(ref tableA, in poolA, startLeft, endRight),
                "Client A: gap must be traversable.");
            Assert.IsFalse(WouldCollide(ref tableB, in poolB, startLeft, endRight),
                "Client B: gap must be traversable (identical to A).");

            // Verify the hole exists at the explosion origin.
            int3 holeCenter = new int3(evt.origin.x, evt.origin.y, evt.origin.z);
            Assert.AreEqual(VoxelDimensions.MaterialEmpty,
                VoxelAccess.GetVoxel(ref tableA, in poolA, holeCenter));
            Assert.AreEqual(VoxelDimensions.MaterialEmpty,
                VoxelAccess.GetVoxel(ref tableB, in poolB, holeCenter));
        }

        [Test]
        [Category("US1")]
        public void HoleSizeMatchesExplosionRadius()
        {
            var pool = new BrickPool(4096, Allocator.Persistent);
            var table = new RegionTable(1, Allocator.Persistent);

            // Build a thick wall (3 bricks deep) at Z=256.
            for (int zOff = -1; zOff <= 1; zOff++)
            {
                BuildWall(ref pool, ref table, new int3(200, 100, 256 + zOff), 100, 300);
            }
            var storage = new RegionMutationStore(in table, in pool);

            var evt = new AlterationEvent
            {
                tick = 1u,
                kind = (byte)VoxelEngine.Edits.Api.AlterationEventKind.Explosion,
                origin = new int3(256, 250, 256),
                shapeData = 20, material = VoxelDimensions.MaterialEmpty,
                seed = 42u, playerId = 1, sequence = 1,
            };

            EventApplication.Apply(new DeterministicAlterationApplier(), storage, in evt, out _);

            // The hole's radius (in voxels) should be approximately ShapeRadius * BrickEdge.
            int expectedVoxelRadius = evt.Radius() * VoxelDimensions.BrickEdge;
            int3 center = new int3(evt.origin.x, evt.origin.y, evt.origin.z);

            int leftmostEmpty = center.x;
            int rightmostEmpty = center.x;

            for (int dx = -expectedVoxelRadius; dx <= expectedVoxelRadius; dx++)
            {
                var testPos = new int3(center.x + dx, center.y, center.z);
                if (VoxelAccess.GetVoxel(ref table, in pool, testPos) == VoxelDimensions.MaterialEmpty)
                {
                    leftmostEmpty = math.min(leftmostEmpty, center.x + dx);
                    rightmostEmpty = math.max(rightmostEmpty, center.x + dx);
                }
            }

            Assert.GreaterOrEqual(rightmostEmpty - leftmostEmpty, expectedVoxelRadius - 2,
                "Hole must be approximately the declared explosion radius.");
        }

        private static void BuildWall(ref BrickPool pool, ref RegionTable table, int3 minCorner, int minY, int maxY)
        {
            var region = table.LoadRegion(int3.zero);
            for (int x = minCorner.x; x < minCorner.x + 100; x++)
            {
                for (int y = minY; y < maxY; y++)
                {
                    // Find the brick containing this voxel.
                    var brickInRegion = new int3(
                        x >> VoxelDimensions.BrickEdgeLog2,
                        y >> VoxelDimensions.BrickEdgeLog2,
                        minCorner.z >> VoxelDimensions.BrickEdgeLog2);

                    int brickIdx = Region.BrickIndex(brickInRegion.x, brickInRegion.y, brickInRegion.z);
                    if (!region.BrickRefs[brickIdx].IsMixed)
                    {
                        var mixed = pool.Allocate();
                        pool.FillBrick(mixed, 5);
                        region.BrickRefs[brickIdx] = BrickRef.FromPoolIndex(mixed);
                    }
                }
            }

            table.CommitRegion(region);
        }

        /// <summary>Check if an AABB would collide moving from start to end through the voxel grid.</summary>
        private static bool WouldCollide(ref RegionTable table, in BrickPool pool, int3 start, int3 end)
        {
            // Simple check: is there a solid voxel between start and end?
            int steps = math.max(math.abs(end.x - start.x), math.max(math.abs(end.y - start.y), math.abs(end.z - start.z)));
            if (steps == 0) return VoxelAccess.IsSolid(ref table, in pool, start);

            for (int s = 1; s < steps; s++)
            {
                float t = (float)s / steps;
                int x = (int)math.lerp(start.x, end.x, t);
                int y = (int)math.lerp(start.y, end.y, t);
                int z = (int)math.lerp(start.z, end.z, t);

                if (VoxelAccess.IsSolid(ref table, in pool, new int3(x, y, z)))
                    return true; // Collision found.
            }

            return false; // Path is clear.
        }
    }
}
