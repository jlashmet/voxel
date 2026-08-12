using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Core.Features;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeUrbanCirculationTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void UpperContourConnectsCentralAscentToEastRidgeWithoutChangingPrimaryStreets()
        {
            KentridgeUrbanCirculationPlan plan = KentridgeUrbanCirculation.Build(Seed);
            Assert.AreEqual(1, plan.Connectors.Count);

            KentridgeUrbanConnector connector = plan.Connectors[0];
            Assert.AreEqual("upper-east-contour", connector.Id);
            Assert.AreEqual(KentridgeUrbanBand.UpperWard, connector.Band);
            Assert.IsTrue(connector.IsHorizontal);
            Assert.AreEqual(KentridgeUrbanCirculation.UpperContourZDm, connector.StartDm.Y);
            Assert.AreEqual(KentridgeUrbanCirculation.UpperContourZDm, connector.EndDm.Y);

            int mainEastEdge =
                KentridgeTownPlanner.MainSpineXDm + KentridgeTownPlanner.MainRoadWidthDm / 2;
            int eastLaneWestEdge =
                KentridgeTownPlanner.EastLaneXDm - KentridgeTownPlanner.ServiceRoadWidthDm / 2;

            Assert.AreEqual(mainEastEdge, connector.StartDm.X);
            Assert.AreEqual(eastLaneWestEdge, connector.EndDm.X);
            Assert.Greater(connector.LengthDm, 250,
                "The upper contour should create a meaningful district-to-district connection.");

            SettlementPlan stablePlan = KentridgeDefinition.Build(Seed);
            Assert.AreEqual(4, stablePlan.Streets.Count,
                "Secondary urban circulation must not mutate the four stable gameplay streets.");
        }

        [Test]
        public void UpperContourHasOneSmoothLandformRealization()
        {
            FeatureCatalogue catalogue = KentridgeUrbanCirculationCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);
            try
            {
                Assert.AreEqual(1, catalogue.Definitions.Length);
                Assert.AreEqual(1, catalogue.ExplicitPlacements.Length);
                Assert.AreEqual(FeatureKind.Landform, catalogue.Definitions[0].Kind);
                Assert.AreEqual(23, catalogue.Definitions[0].Precedence);
                Assert.Greater(catalogue.Definitions[0].Footprint.x, 250);
                Assert.Greater(catalogue.Definitions[0].Footprint.z, 20);
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
