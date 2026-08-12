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
            Assert.AreEqual(7, minRise);
            Assert.AreEqual(13, maxRise);
        }

        [Test]
        public void GalleryCatalogueCreatesSixHardSecondLevelWalksInsideAuthoredFrontageBounds()
        {
            KentridgeVerticalGalleryPlan plan = KentridgeVerticalGalleryPlanner.Build(Seed);
            FeatureCatalogue catalogue = KentridgeVerticalGalleryCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);
            try
            {
                Assert.AreEqual(6, catalogue.Definitions.Length);
                Assert.AreEqual(6, catalogue.ExplicitPlacements.Length);
                for (int i = 0; i < catalogue.Definitions.Length; i++)
                {
                    KentridgeVerticalGalleryRoute route = plan.Routes[i];
                    FeatureDefinition definition = catalogue.Definitions[i];
                    ExplicitPlacement placement = catalogue.ExplicitPlacements[i];

                    Assert.AreEqual(FeatureKind.Infrastructure, definition.Kind);
                    Assert.AreEqual(KentridgeVerticalGalleryCatalogue.GalleryPrecedence,
                        definition.Precedence);
                    Assert.AreEqual(route.LengthDm, definition.Footprint.x,
                        "Gallery and corner stair must stay inside the authored block frontage width.");
                    Assert.AreEqual(
                        KentridgeVerticalGalleryPlanner.GalleryDepthDm,
                        definition.Footprint.z,
                        "Test settings use one voxel per decimetre.");
                    Assert.AreEqual(route.MinXDm, placement.Position.x,
                        "Gallery must begin exactly at its authored frontage edge.");
                    Assert.AreEqual(route.MaxXDm,
                        placement.Position.x + definition.Footprint.x,
                        "Gallery must not protrude beyond the opposite frontage edge.");
                    Assert.Greater(definition.ProgramLength, 40,
                        "Each gallery must include deck, parapet spans and a real inward corner stair.");
                    Assert.AreEqual(1, catalogue.Rules[i].ExplicitCount);
                }

                Assert.Greater(KentridgeVerticalGalleryCatalogue.GalleryPrecedence, 86);
                Assert.Less(KentridgeVerticalGalleryCatalogue.GalleryPrecedence, 89);
                Assert.Less(KentridgeVerticalGalleryCatalogue.GalleryPrecedence, 94);
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
