using MountingForce.WorldGen;
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
        public void UpperContourConnectsUpperLandingToEastRidgeWithoutChangingPrimaryStreets()
        {
            KentridgeUrbanCirculationPlan plan = KentridgeUrbanCirculation.Build(Seed);
            Assert.AreEqual(1, plan.Connectors.Count);

            KentridgeUrbanConnector connector = plan.Connectors[0];
            Assert.AreEqual("upper-east-contour", connector.Id);
            Assert.AreEqual(KentridgeUrbanBand.UpperWard, connector.Band);
            Assert.IsTrue(connector.IsHorizontal);
            Assert.AreEqual(KentridgeUrbanCirculation.UpperContourZDm, connector.StartDm.Y);
            Assert.AreEqual(KentridgeUrbanCirculation.UpperContourZDm, connector.EndDm.Y);
            Assert.AreEqual(40, connector.WidthDm,
                "The east connection should read as a secondary street, not a narrow alley.");

            int mainEastEdge =
                KentridgeTownPlanner.MainSpineXDm + KentridgeTownPlanner.MainRoadWidthDm / 2;
            int eastLaneWestEdge =
                KentridgeTownPlanner.EastLaneXDm - KentridgeTownPlanner.ServiceRoadWidthDm / 2;

            Assert.AreEqual(mainEastEdge, connector.StartDm.X);
            Assert.AreEqual(eastLaneWestEdge, connector.EndDm.X);
            Assert.Greater(connector.LengthDm, 250,
                "The upper contour should create a meaningful district-to-district connection.");

            KentridgeUrbanSkeletonPlan skeleton = KentridgeUrbanSkeleton.Build(Seed);
            Assert.AreEqual(
                skeleton.Get(KentridgeUrbanNodeId.UpperLanding).CentreDm.Y,
                connector.StartDm.Y,
                "The east branch should leave from the semantic Upper Landing itself.");
            Assert.AreEqual(
                skeleton.Get(KentridgeUrbanNodeId.EastRidgeLanding).CentreDm.Y,
                connector.EndDm.Y,
                "The east-ridge public node should sit on the same cross-town axis.");

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
                Assert.Greater(catalogue.Definitions[0].Footprint.z, 35,
                    "The realised upper connector should carry the widened secondary-street section.");
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
