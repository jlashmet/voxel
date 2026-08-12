using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Core.Features;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeVerticalGalleryTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void DownhillGalleriesJoinEveryDenseUndercroftToExistingBlockAccess()
        {
            KentridgeVerticalGalleryPlan plan = KentridgeVerticalGalleryPlanner.Build(Seed);
            Assert.AreEqual(6, plan.Routes.Count);

            int west = 0;
            int east = 0;
            int minRise = int.MaxValue;
            int maxRise = int.MinValue;

            for (int i = 0; i < plan.Routes.Count; i++)
            {
                KentridgeVerticalGalleryRoute route = plan.Routes[i];
                Assert.Greater(route.LengthDm, route.GapWidthDm, route.Id);
                Assert.That(route.RiseDm, Is.InRange(1, 16), route.Id);
                Assert.Greater(route.FrontZDm, 0, route.Id);
                Assert.AreNotEqual(KentridgeUrbanBand.LowerWard, route.Band);

                if (route.ReturnSide == KentridgeUrbanReturnSide.West) west++;
                else east++;
                minRise = System.Math.Min(minRise, route.RiseDm);
                maxRise = System.Math.Max(maxRise, route.RiseDm);
            }

            Assert.AreEqual(3, west);
            Assert.AreEqual(3, east);
            Assert.AreEqual(7, minRise,
                "Civic galleries should need only a short corner stair from the existing contour walk.");
            Assert.AreEqual(13, maxRise,
                "The market arcade should remain within one compact stair flight of its existing contour walk.");
        }

        [Test]
        public void GalleryCatalogueCreatesSixHardSecondLevelWalksBelowBlockAccess()
        {
            FeatureCatalogue catalogue = KentridgeVerticalGalleryCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);
            try
            {
                Assert.AreEqual(6, catalogue.Definitions.Length);
                Assert.AreEqual(6, catalogue.ExplicitPlacements.Length);
                for (int i = 0; i < catalogue.Definitions.Length; i++)
                {
                    FeatureDefinition definition = catalogue.Definitions[i];
                    Assert.AreEqual(FeatureKind.Infrastructure, definition.Kind);
                    Assert.AreEqual(KentridgeVerticalGalleryCatalogue.GalleryPrecedence,
                        definition.Precedence);
                    Assert.AreEqual(
                        KentridgeVerticalGalleryPlanner.GalleryDepthDm,
                        definition.Footprint.z,
                        "Test settings use one voxel per decimetre.");
                    Assert.Greater(definition.ProgramLength, 40,
                        "Each gallery must include deck, parapet spans and a real corner stair.");
                    Assert.AreEqual(1, catalogue.Rules[i].ExplicitCount);
                }

                Assert.Greater(KentridgeVerticalGalleryCatalogue.GalleryPrecedence, 86,
                    "Gallery walks should remain visible outside anonymous fabric.");
                Assert.Less(KentridgeVerticalGalleryCatalogue.GalleryPrecedence, 89,
                    "Major secondary stair streets must still win where circulation overlaps.");
                Assert.Less(KentridgeVerticalGalleryCatalogue.GalleryPrecedence, 94,
                    "Block access remains the final local circulation authority.");
            }
            finally
            {
                catalogue.Dispose();
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
