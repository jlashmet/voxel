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
        public void SecondaryNetworkAddsEastContourAndTwoIndependentWestAscentChoices()
        {
            KentridgeUrbanCirculationPlan plan = KentridgeUrbanCirculation.Build(Seed);
            Assert.AreEqual(4, plan.Connectors.Count);

            KentridgeUrbanConnector upperEast = plan.Connectors[0];
            Assert.AreEqual("upper-east-contour", upperEast.Id);
            Assert.AreEqual(KentridgeUrbanConnectorKind.ContourLane, upperEast.Kind);
            Assert.IsTrue(upperEast.IsHorizontal);
            Assert.AreEqual(40, upperEast.WidthDm);

            int mainEastEdge = KentridgeTownPlanner.MainSpineXDm + KentridgeTownPlanner.MainRoadWidthDm / 2;
            int mainWestEdge = KentridgeTownPlanner.MainSpineXDm - KentridgeTownPlanner.MainRoadWidthDm / 2;
            int eastLaneWestEdge = KentridgeTownPlanner.EastLaneXDm - KentridgeTownPlanner.ServiceRoadWidthDm / 2;
            Assert.AreEqual(mainEastEdge, upperEast.StartDm.X);
            Assert.AreEqual(eastLaneWestEdge, upperEast.EndDm.X);

            KentridgeUrbanConnector lowerWest = plan.Connectors[1];
            Assert.AreEqual("lower-west-stair-street", lowerWest.Id);
            Assert.AreEqual(KentridgeUrbanConnectorKind.StairStreet, lowerWest.Kind);
            Assert.IsTrue(lowerWest.IsVertical);
            Assert.AreEqual(KentridgeTownPlanner.ResidentialStreetZDm, lowerWest.StartDm.Y);
            Assert.AreEqual(22, lowerWest.WidthDm);
            Assert.Greater(lowerWest.LengthDm, 300);

            KentridgeUrbanConnector westUpperStair = plan.Connectors[2];
            Assert.AreEqual("west-upper-stair-street", westUpperStair.Id);
            Assert.AreEqual(KentridgeUrbanConnectorKind.StairStreet, westUpperStair.Kind);
            Assert.IsTrue(westUpperStair.IsVertical);
            Assert.AreEqual(KentridgeTownPlanner.MarketStreetZDm, westUpperStair.StartDm.Y);
            Assert.AreEqual(KentridgeUrbanCirculation.UpperContourZDm, westUpperStair.EndDm.Y);
            Assert.AreEqual(KentridgeUrbanCirculation.WestUpperStairXDm, westUpperStair.StartDm.X);
            Assert.AreEqual(22, westUpperStair.WidthDm);
            Assert.Greater(westUpperStair.LengthDm, 150);

            KentridgeUrbanConnector westUpperContour = plan.Connectors[3];
            Assert.AreEqual("west-upper-contour", westUpperContour.Id);
            Assert.AreEqual(KentridgeUrbanConnectorKind.ContourLane, westUpperContour.Kind);
            Assert.IsTrue(westUpperContour.IsHorizontal);
            Assert.AreEqual(KentridgeUrbanCirculation.WestUpperStairXDm, westUpperContour.StartDm.X);
            Assert.AreEqual(mainWestEdge, westUpperContour.EndDm.X);
            Assert.AreEqual(KentridgeUrbanCirculation.UpperContourZDm, westUpperContour.StartDm.Y);
            Assert.AreEqual(22, westUpperContour.WidthDm);
            Assert.Greater(westUpperContour.LengthDm, 400);

            Assert.Less(lowerWest.StartDm.X + lowerWest.WidthDm / 2, mainWestEdge);
            Assert.Less(westUpperStair.StartDm.X + westUpperStair.WidthDm / 2, 850,
                "The upper west stair must remain west of the upper-west block.");

            KentridgeUrbanSkeletonPlan skeleton = KentridgeUrbanSkeleton.Build(Seed);
            Assert.AreEqual(skeleton.Get(KentridgeUrbanNodeId.WestMarketJunction).CentreDm.X, westUpperStair.StartDm.X);
            Assert.AreEqual(skeleton.Get(KentridgeUrbanNodeId.WestUpperLanding).CentreDm.Y, westUpperContour.StartDm.Y);

            SettlementPlan stablePlan = KentridgeDefinition.Build(Seed);
            Assert.AreEqual(4, stablePlan.Streets.Count);
        }

        [Test]
        public void CirculationCatalogueKeepsContoursSmoothAndBothWestStairsHardAndStepped()
        {
            FeatureCatalogue catalogue = KentridgeUrbanCirculationCatalogue.Build(Seed, BuildSettings(), Allocator.Temp);
            try
            {
                Assert.AreEqual(4, catalogue.Definitions.Length);
                Assert.AreEqual(4, catalogue.ExplicitPlacements.Length);

                FeatureDefinition upperEast = catalogue.Definitions[0];
                FeatureDefinition lowerWest = catalogue.Definitions[1];
                FeatureDefinition westUpperStair = catalogue.Definitions[2];
                FeatureDefinition westUpperContour = catalogue.Definitions[3];

                Assert.AreEqual(FeatureKind.Landform, upperEast.Kind);
                Assert.AreEqual(23, upperEast.Precedence);
                Assert.AreEqual(FeatureKind.Infrastructure, lowerWest.Kind);
                Assert.AreEqual(89, lowerWest.Precedence);
                Assert.AreEqual(FeatureKind.Infrastructure, westUpperStair.Kind);
                Assert.AreEqual(89, westUpperStair.Precedence);
                Assert.AreEqual(FeatureKind.Landform, westUpperContour.Kind);
                Assert.AreEqual(23, westUpperContour.Precedence);

                Assert.Greater(lowerWest.Footprint.z, 300);
                Assert.Greater(westUpperStair.Footprint.z, 150);
                Assert.Greater(westUpperContour.Footprint.x, 400);

                Assert.GreaterOrEqual(CountBoxes(catalogue, lowerWest), 20);
                Assert.GreaterOrEqual(CountBoxes(catalogue, westUpperStair), 18);
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        private static int CountBoxes(FeatureCatalogue catalogue, FeatureDefinition definition)
        {
            int boxes = 0;
            int pc = definition.ProgramOffset;
            int end = pc + definition.ProgramLength;
            while (pc < end)
            {
                ShapeOp op = (ShapeOp)catalogue.Program[pc];
                int instruction = ShapeOps.InstructionLength(op);
                Assert.Greater(instruction, 0);
                if (op == ShapeOp.End) break;
                if (op == ShapeOp.EmitBox) boxes++;
                pc += instruction;
            }
            return boxes;
        }

        private static VoxelWorldGenSettings BuildSettings()
        {
            var materials = new VoxelMaterialMap(foundationStone: 1, masonry: 1, darkMasonry: 6, timber: 2, glass: 4, warmWindow: 15, roofTile: 8, slate: 7, cloth: 9, moss: 14, water: 11, roadSurface: 13);
            return new VoxelWorldGenSettings(1, materials);
        }
    }
}
