using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Core.Features;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeUrbanSidewalkTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void StableStreetNetworkReceivesEightSurfaceFollowingPedestrianMargins()
        {
            FeatureCatalogue catalogue = KentridgeUrbanSidewalkCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);
            try
            {
                Assert.AreEqual(8, catalogue.Definitions.Length,
                    "Four authored street corridors should each receive two pedestrian margins.");
                Assert.AreEqual(8, catalogue.ExplicitPlacements.Length);
                for (int i = 0; i < catalogue.Definitions.Length; i++)
                {
                    FeatureDefinition definition = catalogue.Definitions[i];
                    Assert.AreEqual(FeatureKind.Landform, definition.Kind);
                    Assert.AreEqual(KentridgeUrbanSidewalkCatalogue.SidewalkPrecedence,
                        definition.Precedence);
                    Assert.Greater(definition.Footprint.x, 0);
                    Assert.Greater(definition.Footprint.z, 0);
                    Assert.AreEqual(1, catalogue.Rules[i].ExplicitCount);
                }
            }
            finally { catalogue.Dispose(); }
        }

        [Test]
        public void SidewalksStayBelowNamedFrontagePaths()
        {
            Assert.AreEqual(10, KentridgeUrbanSidewalkCatalogue.SidewalkWidthDm);
            Assert.AreEqual(2, KentridgeUrbanSidewalkCatalogue.RoadOverlapDm);
            Assert.Less(KentridgeUrbanSidewalkCatalogue.SidewalkPrecedence, 60,
                "Door approaches must remain free to paint across the continuous sidewalk margin.");
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
