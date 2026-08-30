using Game.WorldBuilder.Voxel;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ShowcaseBakeExplicitStructureCoverageTests
    {
        private const uint Seed = 0x5EED1234u;
        private const int RegionVoxelEdgeLog2 = 9;

        [Test]
        public void PlannerIncludesUpperDragonStructureLayerWithoutExpandingMountainSky()
        {
            MountainLandmarkSpec spec = ShowcaseMountainDragonLayout.CreateLandmark(Seed);
            FeatureCatalogue mountain = WorldBuilderMountainLandmarkCatalogue.Build(
                in spec,
                mountainMaterial: 1,
                pathMaterial: 13,
                placeholderMaterial: 9,
                allocator: Allocator.Temp);
            try
            {
                var regions = ShowcaseWorld.PlanExplicitFixedStructureBakeRegions(
                    in mountain,
                    int3.zero,
                    startupRadiusRegions: 8);

                int3 dragonCentre = new int3(
                    spec.Origin.x + spec.CentreLocal,
                    spec.Origin.y + spec.MountainHeight + 1 + spec.PlaceholderSize / 2,
                    spec.Origin.z + spec.CentreLocal);
                int3 dragonRegion = dragonCentre >> RegionVoxelEdgeLog2;

                CollectionAssert.Contains(regions, dragonRegion,
                    "The startup bake must materialise the upper region containing the dragon placeholder.");
                CollectionAssert.Contains(regions, new int3(dragonRegion.x, 0, dragonRegion.z),
                    "A fixed structure crossing the vertical boundary must preserve its lower region too.");
                Assert.AreEqual(2, regions.Count,
                    "Bake coverage must follow the explicit fixed-altitude structure bounds, not materialise unrelated mountain/headroom sky regions.");
            }
            finally
            {
                mountain.Dispose();
            }
        }
    }
}
