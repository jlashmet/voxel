using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Guards the composition boundary between the showcase castle and the generated Mountain
    /// Dragon landmark. ShowcaseWorld deliberately suppresses generic catalogue features in every
    /// castle-owned region, so placing any part of the mountain footprint in that set silently
    /// drops authored path/floor content from the baked world.
    /// </summary>
    public sealed class MountainDragonCastleRegionOwnershipTests
    {
        private const uint Seed = 0x5EED1234;

        [Test]
        public void MountainFootprintDoesNotEnterCastleOwnedFeatureSuppressionRegions()
        {
            CastlePlan castle = StructuresComposition.PlanCastle(
                new int3(ShowcaseWorld.LandmarkCentreX, 0, ShowcaseWorld.LandmarkCentreZ),
                Seed);

            int regionEdge = ShowcaseWorld.RegionVoxelEdge;
            int castleReach = math.max(
                castle.PlateauRadius + castle.CliffDrop + 8,
                regionEdge);
            int castleMinX = FloorDiv(castle.Centre.x - castleReach, regionEdge);
            int castleMaxX = FloorDiv(castle.Centre.x + castleReach, regionEdge);
            int castleMinZ = FloorDiv(castle.Centre.z - castleReach, regionEdge);
            int castleMaxZ = FloorDiv(castle.Centre.z + castleReach, regionEdge);

            int mountainMinX = FloorDiv(ShowcaseMountainDragonLayout.OriginX, regionEdge);
            int mountainMaxX = FloorDiv(
                ShowcaseMountainDragonLayout.OriginX + ShowcaseMountainDragonLayout.FootprintEdge - 1,
                regionEdge);
            int mountainMinZ = FloorDiv(ShowcaseMountainDragonLayout.OriginZ, regionEdge);
            int mountainMaxZ = FloorDiv(
                ShowcaseMountainDragonLayout.OriginZ + ShowcaseMountainDragonLayout.FootprintEdge - 1,
                regionEdge);

            bool overlapsX = mountainMinX <= castleMaxX && mountainMaxX >= castleMinX;
            bool overlapsZ = mountainMinZ <= castleMaxZ && mountainMaxZ >= castleMinZ;

            Assert.That(overlapsX && overlapsZ, Is.False,
                $"Mountain Dragon footprint regions x={mountainMinX}..{mountainMaxX}, "
                + $"z={mountainMinZ}..{mountainMaxZ} overlap castle-owned feature-suppression "
                + $"regions x={castleMinX}..{castleMaxX}, z={castleMinZ}..{castleMaxZ}. "
                + "Generic mountain features in those regions are deferred and intentionally "
                + "discarded after castle authoring, which removes switchback floor/headroom content "
                + "from the fresh startup bake.");
        }

        private static int FloorDiv(int value, int divisor)
        {
            int quotient = value / divisor;
            if (value % divisor != 0 && value < 0) quotient--;
            return quotient;
        }
    }
}
