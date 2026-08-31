using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// Physical road decomposition must be invisible. These fixtures deliberately sample inside
    /// overlapping endpoint-clamped corridor fields where independent primitive writes disagree.
    /// A continuous road field chooses the closest presentation segment first, so write order and
    /// bounded-piece partitioning cannot change the resulting surface.
    /// </summary>
    public sealed class WorldRoadContinuousFieldRegressionTests
    {
        private const uint Seed = 0x524F4144u;
        private static readonly int3 Probe = new int3(46, 0, 21);

        [Test]
        public void InternalTurnPiecesMustNotChangeSurfaceWithWriteOrder()
        {
            Primitive approach = Corridor(
                new int3(16, 15, 16),
                new int3(56, 15, 16));
            Primitive departure = Corridor(
                new int3(56, 15, 16),
                new int3(56, 15, 56));

            Assert.IsTrue(TerrainCorridorRasteriser.TrySample(
                in approach, Probe.x, Probe.z, out TerrainCorridorSample approachSample));
            Assert.IsTrue(TerrainCorridorRasteriser.TrySample(
                in departure, Probe.x, Probe.z, out TerrainCorridorSample departureSample));
            Assert.AreNotEqual(approachSample.TargetHeightVoxels, departureSample.TargetHeightVoxels,
                "The fixture must discriminate the closest road segment from the other segment's endpoint cap.");

            int approachThenDeparture = RasterisedTopAtProbe(approach, departure);
            int departureThenApproach = RasterisedTopAtProbe(departure, approach);

            Assert.AreEqual(departureThenApproach, approachThenDeparture,
                "Bounded physical pieces are execution partitions only; reversing two pieces of the same continuous turn must not alter the road surface.");
        }

        [Test]
        public void SharedVertexJunctionMustNotChangeSurfaceWithIncidentPieceOrder()
        {
            Primitive west = Corridor(
                new int3(16, 15, 16),
                new int3(56, 15, 16));
            Primitive east = Corridor(
                new int3(56, 15, 16),
                new int3(96, 15, 16));
            Primitive north = Corridor(
                new int3(56, 15, 16),
                new int3(56, 15, 56));

            int westLast = RasterisedTopAtProbe(north, east, west);
            int northLast = RasterisedTopAtProbe(west, east, north);
            int eastLast = RasterisedTopAtProbe(north, west, east);

            Assert.AreEqual(westLast, northLast,
                "A real shared-vertex junction must be evaluated as one topology-aware field, not by whichever incident primitive writes last.");
            Assert.AreEqual(westLast, eastLast,
                "Changing incident-piece order at a junction must not expose independent endpoint cross-sections.");
        }

        private static int RasterisedTopAtProbe(params Primitive[] corridors)
        {
            var table = new RegionTable(expectedResident: 4, Allocator.Temp);
            var pool = new BrickPool(capacity: 16, Allocator.Temp);
            var batch = new NativeArray<Primitive>(corridors, Allocator.Temp);
            try
            {
                var mutations = new RegionMutationStore(in table, in pool);
                var reads = new RegionReadSource(in table, in pool);
                var terrain = new VoxelCell { BaseMaterialId = 2 };
                int3 probeBlock = new int3(Probe.x >> 3, 1, Probe.z >> 3);
                Assert.IsTrue(mutations.SetWholeCellBlock(probeBlock, in terrain, false));

                int3 minimum = new int3(Probe.x, -32, Probe.z);
                int3 maximum = new int3(Probe.x + 1, 64, Probe.z + 1);
                ContinuousTerrainCorridorRasteriser.Rasterise(
                    batch, minimum, maximum, reads, mutations);

                Assert.IsTrue(reads.TryFindTopSolid(
                    Probe.x, Probe.z, -32, 64, out int top, out _));
                return top;
            }
            finally
            {
                batch.Dispose();
                table.Dispose();
                pool.Dispose();
            }
        }

        private static Primitive Corridor(int3 a, int3 b)
        {
            return new Primitive
            {
                Shape = PrimitiveShape.TerrainCorridor,
                Mode = PrimitiveMode.TerrainCorridor,
                Material = 13,
                A = a,
                B = b,
                InnerRadius = 12,
                Radius = 24,
                C = new int3(20, 4, 24),
                D = new int3(0, unchecked((int)Seed), 1),
            };
        }
    }
}
