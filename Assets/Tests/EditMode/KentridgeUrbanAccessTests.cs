using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Core.Features;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeUrbanAccessTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void AccessPlanConnectsEveryBlockFacadeToItsCourt()
        {
            KentridgeUrbanAccessPlan plan = KentridgeUrbanAccessPlanner.Build(Seed);
            Assert.AreEqual(7, plan.Routes.Count);

            int westReturns = 0;
            int eastReturns = 0;
            int shallowDrops = 0;
            int deepDrops = 0;

            for (int i = 0; i < plan.Routes.Count; i++)
            {
                KentridgeUrbanAccessRoute route = plan.Routes[i];
                Assert.Greater(route.SouthLengthDm, 100, route.Id);
                Assert.Greater(route.ReturnLengthDm, 80, route.Id);
                Assert.That(route.CourtWidthDm, Is.InRange(20, 32), route.Id);
                Assert.Greater(route.StairLengthDm, 0, route.Id);
                Assert.Greater(route.StairSteps, 4, route.Id);

                if (route.ReturnSide == KentridgeUrbanReturnSide.West) westReturns++;
                else eastReturns++;

                if (route.DoorLevelBelowShelfDm < 20)
                {
                    shallowDrops++;
                    Assert.AreEqual(11, route.DoorLevelBelowShelfDm);
                    Assert.AreEqual(36, route.StairLengthDm);
                }
                else
                {
                    deepDrops++;
                    Assert.AreEqual(49, route.DoorLevelBelowShelfDm,
                        "Embedded upper fabric should share the 4.9 m door-to-shelf climb.");
                    Assert.AreEqual(72, route.StairLengthDm);
                    Assert.AreEqual(25, route.StairSteps);
                }
            }

            Assert.AreEqual(4, westReturns);
            Assert.AreEqual(3, eastReturns);
            Assert.AreEqual(1, shallowDrops);
            Assert.AreEqual(6, deepDrops);
        }

        [Test]
        public void AccessCatalogueBuildsSevenHardNavigableInterfaces()
        {
            FeatureCatalogue catalogue = KentridgeUrbanAccessCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);
            try
            {
                Assert.AreEqual(7, catalogue.Definitions.Length);
                Assert.AreEqual(7, catalogue.Rules.Length);
                Assert.AreEqual(7, catalogue.ExplicitPlacements.Length);
                Assert.AreEqual(0, catalogue.Anchors.Length);

                for (int i = 0; i < catalogue.Definitions.Length; i++)
                {
                    FeatureDefinition definition = catalogue.Definitions[i];
                    PlacementRule rule = catalogue.Rules[i];
                    Assert.AreEqual(FeatureKind.Infrastructure, definition.Kind);
                    Assert.AreEqual(94, definition.Precedence);
                    Assert.Greater(definition.Footprint.x, 100);
                    Assert.Greater(definition.Footprint.y, 40);
                    Assert.Greater(definition.Footprint.z, 80);
                    Assert.Greater(definition.ProgramLength, 100,
                        "Each route should include carve, contour walks, stair flight, landing, and gateway.");
                    Assert.AreEqual(i, rule.DefinitionId);
                    Assert.AreEqual(i, rule.ExplicitOffset);
                    Assert.AreEqual(1, rule.ExplicitCount);
                }
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
