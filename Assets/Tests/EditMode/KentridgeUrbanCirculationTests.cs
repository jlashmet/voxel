using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Core.Features;

using VoxelEngine.Structures.Api;

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

            int mainWestEdge = KentridgeTownPlanner.MainSpineXDm - KentridgeTownPlanner.MainRoadWidthDm / 2;
            KentridgeUrbanConnector upperEast = plan.Connectors[0];
            KentridgeUrbanConnector lowerWest = plan.Connectors[1];
            KentridgeUrbanConnector westUpperStair = plan.Connectors[2];
            KentridgeUrbanConnector westUpperContour = plan.Connectors[3];

            Assert.AreEqual("upper-east-contour", upperEast.Id);
            Assert.AreEqual(KentridgeUrbanConnectorKind.ContourLane, upperEast.Kind);
            Assert.IsTrue(upperEast.IsHorizontal);

            Assert.AreEqual("lower-west-stair-street", lowerWest.Id);
            Assert.AreEqual(KentridgeUrbanConnectorKind.StairStreet, lowerWest.Kind);
            Assert.IsTrue(lowerWest.IsVertical);
            Assert.Greater(lowerWest.LengthDm, 300);

            Assert.AreEqual("west-upper-stair-street", westUpperStair.Id);
            Assert.AreEqual(KentridgeUrbanConnectorKind.StairStreet, westUpperStair.Kind);
            Assert.IsTrue(westUpperStair.IsVertical);
            Assert.AreEqual(810, westUpperStair.StartDm.X,
                "The upper alternate should be tucked into the west-side urban fabric rather than the world edge.");
            Assert.AreEqual(KentridgeTownPlanner.MarketStreetZDm, westUpperStair.StartDm.Y);
            Assert.AreEqual(KentridgeUrbanCirculation.UpperContourZDm, westUpperStair.EndDm.Y);
            Assert.AreEqual(22, westUpperStair.WidthDm);

            // The upper-west anonymous block starts at x=850. A 2.2 m stair centred at x=810
            // leaves a deliberate pedestrian-scale gap rather than touching the building envelope.
            Assert.Less(westUpperStair.StartDm.X + westUpperStair.WidthDm / 2, 850);
            Assert.Greater(westUpperStair.StartDm.X, 770,
                "The stair should remain close enough to the market frontage to read as an urban alley stair.");

            Assert.AreEqual("west-upper-contour", westUpperContour.Id);
            Assert.AreEqual(KentridgeUrbanConnectorKind.ContourLane, westUpperContour.Kind);
            Assert.IsTrue(westUpperContour.IsHorizontal);
            Assert.AreEqual(KentridgeUrbanCirculation.WestUpperStairXDm, westUpperContour.StartDm.X);
            Assert.AreEqual(mainWestEdge, westUpperContour.EndDm.X);
            Assert.Greater(westUpperContour.LengthDm, 300);
            Assert.Less(westUpperContour.LengthDm, 400,
                "The inward stair placement should avoid a long detached edge-of-town contour strip.");

            KentridgeUrbanSkeletonPlan skeleton = KentridgeUrbanSkeleton.Build(Seed);
            Assert.AreEqual(skeleton.Get(KentridgeUrbanNodeId.WestMarketJunction).CentreDm.X, westUpperStair.StartDm.X);
            Assert.AreEqual(skeleton.Get(KentridgeUrbanNodeId.WestUpperLanding).CentreDm.X, westUpperContour.StartDm.X);

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
                Assert.AreEqual(FeatureKind.Landform, catalogue.Definitions[0].Kind);
                Assert.AreEqual(FeatureKind.Infrastructure, catalogue.Definitions[1].Kind);
                Assert.AreEqual(FeatureKind.Infrastructure, catalogue.Definitions[2].Kind);
                Assert.AreEqual(FeatureKind.Landform, catalogue.Definitions[3].Kind);
                Assert.GreaterOrEqual(CountBoxes(catalogue, catalogue.Definitions[1]), 20);
                Assert.GreaterOrEqual(CountBoxes(catalogue, catalogue.Definitions[2]), 18);
                Assert.Greater(catalogue.Definitions[3].Footprint.x, 300);
                Assert.Less(catalogue.Definitions[3].Footprint.x, 400);
            }
            finally { catalogue.Dispose(); }
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
