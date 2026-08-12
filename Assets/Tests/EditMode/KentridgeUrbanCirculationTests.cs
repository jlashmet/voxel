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
        public void SecondaryNetworkAddsUpperCrossStreetAndIndependentLowerMarketAscent()
        {
            KentridgeUrbanCirculationPlan plan = KentridgeUrbanCirculation.Build(Seed);
            Assert.AreEqual(2, plan.Connectors.Count);

            KentridgeUrbanConnector upper = plan.Connectors[0];
            Assert.AreEqual("upper-east-contour", upper.Id);
            Assert.AreEqual(KentridgeUrbanConnectorKind.ContourLane, upper.Kind);
            Assert.AreEqual(KentridgeUrbanBand.UpperWard, upper.Band);
            Assert.IsTrue(upper.IsHorizontal);
            Assert.AreEqual(KentridgeUrbanCirculation.UpperContourZDm, upper.StartDm.Y);
            Assert.AreEqual(KentridgeUrbanCirculation.UpperContourZDm, upper.EndDm.Y);
            Assert.AreEqual(40, upper.WidthDm,
                "The east connection should read as a secondary street, not a narrow alley.");

            int mainEastEdge =
                KentridgeTownPlanner.MainSpineXDm + KentridgeTownPlanner.MainRoadWidthDm / 2;
            int eastLaneWestEdge =
                KentridgeTownPlanner.EastLaneXDm - KentridgeTownPlanner.ServiceRoadWidthDm / 2;
            Assert.AreEqual(mainEastEdge, upper.StartDm.X);
            Assert.AreEqual(eastLaneWestEdge, upper.EndDm.X);
            Assert.Greater(upper.LengthDm, 250);

            KentridgeUrbanSkeletonPlan skeleton = KentridgeUrbanSkeleton.Build(Seed);
            Assert.AreEqual(
                skeleton.Get(KentridgeUrbanNodeId.UpperLanding).CentreDm.Y,
                upper.StartDm.Y,
                "The east branch should leave from the semantic Upper Landing itself.");
            Assert.AreEqual(
                skeleton.Get(KentridgeUrbanNodeId.EastRidgeLanding).CentreDm.Y,
                upper.EndDm.Y,
                "The east-ridge public node should sit on the same cross-town axis.");

            KentridgeUrbanConnector lower = plan.Connectors[1];
            Assert.AreEqual("lower-west-stair-street", lower.Id);
            Assert.AreEqual(KentridgeUrbanConnectorKind.StairStreet, lower.Kind);
            Assert.IsTrue(lower.IsVertical);
            Assert.AreEqual(KentridgeUrbanCirculation.LowerWestStairXDm, lower.StartDm.X);
            Assert.AreEqual(KentridgeUrbanCirculation.LowerWestStairXDm, lower.EndDm.X);
            Assert.AreEqual(KentridgeTownPlanner.ResidentialStreetZDm, lower.StartDm.Y,
                "The alternate ascent should begin directly on the residential level.");
            Assert.AreEqual(KentridgeUrbanCirculation.LowerWestStairNorthZDm, lower.EndDm.Y);
            Assert.AreEqual(22, lower.WidthDm,
                "The second ascent should remain pedestrian-scale beside the main road.");
            Assert.Greater(lower.LengthDm, 300,
                "The route should provide a genuine independent lower-to-market ascent.");

            int roadWestEdge =
                KentridgeTownPlanner.MainSpineXDm - KentridgeTownPlanner.MainRoadWidthDm / 2;
            Assert.Less(lower.StartDm.X + lower.WidthDm / 2, roadWestEdge,
                "The west stair street must remain spatially separate from the central vehicular spine.");

            SettlementPlan stablePlan = KentridgeDefinition.Build(Seed);
            Assert.AreEqual(4, stablePlan.Streets.Count,
                "Secondary urban circulation must not mutate the four stable gameplay streets.");
        }

        [Test]
        public void CirculationCatalogueKeepsContourSmoothButMakesStairStreetHardAndStepped()
        {
            FeatureCatalogue catalogue = KentridgeUrbanCirculationCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);
            try
            {
                Assert.AreEqual(2, catalogue.Definitions.Length);
                Assert.AreEqual(2, catalogue.ExplicitPlacements.Length);

                FeatureDefinition upper = catalogue.Definitions[0];
                FeatureDefinition lower = catalogue.Definitions[1];
                Assert.AreEqual(FeatureKind.Landform, upper.Kind);
                Assert.AreEqual(23, upper.Precedence);
                Assert.AreEqual(FeatureKind.Infrastructure, lower.Kind);
                Assert.AreEqual(89, lower.Precedence);

                Assert.Greater(upper.Footprint.x, 250);
                Assert.Greater(upper.Footprint.z, 35,
                    "The upper connector should carry the widened secondary-street section.");
                Assert.Greater(lower.Footprint.z, 300,
                    "The lower stair-street realization should span residential-to-market levels.");
                Assert.That(lower.Footprint.x, Is.InRange(20, 24));

                int stepBoxes = 0;
                int pc = lower.ProgramOffset;
                int end = pc + lower.ProgramLength;
                while (pc < end)
                {
                    ShapeOp op = (ShapeOp)catalogue.Program[pc];
                    int instruction = ShapeOps.InstructionLength(op);
                    Assert.Greater(instruction, 0);
                    if (op == ShapeOp.End) break;
                    if (op == ShapeOp.EmitBox) stepBoxes++;
                    pc += instruction;
                }

                Assert.GreaterOrEqual(stepBoxes, 20,
                    "The alternate ascent should compile to visible stair treads rather than one smooth ramp.");
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
