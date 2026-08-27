using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class KentridgeUrbanFabricSpacingPlayModeTests
    {
        private const uint Seed = 0x4B454E54u;
        private const int MinimumClearanceDm = 20;

        [Test]
        public void ProductionAnonymousFrontagesLeavePedestrianClearanceBetweenHouses()
        {
            VoxelWorldGenSettings settings = BuildSettings();
            FeatureCatalogue catalogue = KentridgeFrontageAlignedUrbanFabricCatalogue.Build(
                Seed, settings, Allocator.Temp);

            try
            {
                Assert.Greater(catalogue.Definitions.Length, 0,
                    "Production Kentridge anonymous fabric must remain populated.");
                Assert.AreEqual(catalogue.Definitions.Length, catalogue.ExplicitPlacements.Length,
                    "Each anonymous fabric definition should keep its production explicit placement.");

                int minimumClearance = MinimumClearanceDm * settings.VoxelsPerDecimetre;
                int comparedNeighbours = 0;

                for (int a = 0; a < catalogue.Definitions.Length; a++)
                {
                    BoundsXZ boundsA = Bounds(
                        in catalogue.Definitions[a], in catalogue.ExplicitPlacements[a]);

                    for (int b = a + 1; b < catalogue.Definitions.Length; b++)
                    {
                        ExplicitPlacement placementA = catalogue.ExplicitPlacements[a];
                        ExplicitPlacement placementB = catalogue.ExplicitPlacements[b];
                        if ((placementA.Orientation & 3) != (placementB.Orientation & 3))
                            continue;

                        BoundsXZ boundsB = Bounds(in catalogue.Definitions[b], in placementB);
                        bool horizontalFrontage = (placementA.Orientation & 1) == 0;
                        int clearance;

                        if (horizontalFrontage)
                        {
                            if (!Overlaps(boundsA.MinZ, boundsA.MaxZ, boundsB.MinZ, boundsB.MaxZ))
                                continue;
                            clearance = IntervalClearance(
                                boundsA.MinX, boundsA.MaxX, boundsB.MinX, boundsB.MaxX);
                        }
                        else
                        {
                            if (!Overlaps(boundsA.MinX, boundsA.MaxX, boundsB.MinX, boundsB.MaxX))
                                continue;
                            clearance = IntervalClearance(
                                boundsA.MinZ, boundsA.MaxZ, boundsB.MinZ, boundsB.MaxZ);
                        }

                        comparedNeighbours++;
                        Assert.GreaterOrEqual(clearance, minimumClearance,
                            $"Anonymous fabric {a} and {b} leave only {clearance} voxels of lateral " +
                            $"clearance; production Kentridge frontage requires at least " +
                            $"{minimumClearance} voxels ({MinimumClearanceDm} dm at scale 1)." );
                    }
                }

                Assert.Greater(comparedNeighbours, 0,
                    "Regression must exercise at least one pair of neighbouring production houses.");
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        private static BoundsXZ Bounds(
            in FeatureDefinition definition,
            in ExplicitPlacement placement)
        {
            bool quarterTurn = (placement.Orientation & 1) != 0;
            int width = quarterTurn ? definition.Footprint.z : definition.Footprint.x;
            int depth = quarterTurn ? definition.Footprint.x : definition.Footprint.z;
            return new BoundsXZ(
                placement.Position.x,
                placement.Position.x + width,
                placement.Position.z,
                placement.Position.z + depth);
        }

        private static bool Overlaps(int minA, int maxA, int minB, int maxB) =>
            maxA > minB && maxB > minA;

        private static int IntervalClearance(int minA, int maxA, int minB, int maxB)
        {
            if (maxA <= minB) return minB - maxA;
            if (maxB <= minA) return minA - maxB;
            return -System.Math.Min(maxA - minB, maxB - minA);
        }

        private readonly struct BoundsXZ
        {
            public readonly int MinX;
            public readonly int MaxX;
            public readonly int MinZ;
            public readonly int MaxZ;

            public BoundsXZ(int minX, int maxX, int minZ, int maxZ)
            {
                MinX = minX;
                MaxX = maxX;
                MinZ = minZ;
                MaxZ = maxZ;
            }
        }

        private static VoxelWorldGenSettings BuildSettings()
        {
            var materials = new VoxelMaterialMap(
                foundationStone: 1, masonry: 1, darkMasonry: 6,
                timber: 2, glass: 4, warmWindow: 15,
                roofTile: 8, slate: 7, cloth: 9,
                moss: 14, water: 11, roadSurface: 13);
            return new VoxelWorldGenSettings(1, materials);
        }
    }
}
