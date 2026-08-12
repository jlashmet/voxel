using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Core.Features;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeUpperSkybridgeTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void UpperSkybridgeConnectsProtectedCourtsAcrossFourMetresOfRoadClearance()
        {
            KentridgeUpperSkybridgePlan plan = KentridgeUpperSkybridgePlanner.Build(Seed);

            Assert.AreEqual(1088, plan.WestXDm);
            Assert.AreEqual(1252, plan.EastXDm);
            Assert.AreEqual(164, plan.LengthDm);
            Assert.AreEqual(460, plan.CentreZDm);
            Assert.AreEqual(451, plan.SouthZDm);
            Assert.AreEqual(469, plan.NorthZDm);
            Assert.AreEqual(18, plan.DepthDm);
            Assert.AreEqual(KentridgeProcessionalClimb.UpperLandingOffsetDm,
                plan.ShelfOffsetDm);
            Assert.AreEqual(KentridgeProcessionalClimb.MarketOffsetDm,
                plan.RoadOffsetDm);
            Assert.AreEqual(40, plan.ClearanceDm,
                "The upper court street should cross exactly four metres above the still-Market-level main ascent.");

            SettlementPlan stable = KentridgeDefinition.Build(Seed);
            Assert.AreEqual(4, stable.Streets.Count,
                "The overpass belongs to secondary urban circulation and must not mutate stable gameplay streets.");
        }

        [Test]
        public void SkybridgeCatalogueCreatesOneOpenHardUpperStreetWithoutRoadPiers()
        {
            KentridgeUpperSkybridgePlan plan = KentridgeUpperSkybridgePlanner.Build(Seed);
            FeatureCatalogue catalogue = KentridgeUpperSkybridgeCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);
            try
            {
                Assert.AreEqual(1, catalogue.Definitions.Length);
                Assert.AreEqual(1, catalogue.ExplicitPlacements.Length);
                FeatureDefinition definition = catalogue.Definitions[0];
                ExplicitPlacement placement = catalogue.ExplicitPlacements[0];

                Assert.AreEqual(FeatureKind.Infrastructure, definition.Kind);
                Assert.AreEqual(KentridgeUpperSkybridgeCatalogue.BridgePrecedence,
                    definition.Precedence);
                Assert.AreEqual(plan.LengthDm, definition.Footprint.x,
                    "Test settings use one voxel per decimetre.");
                Assert.AreEqual(plan.DepthDm, definition.Footprint.z);
                Assert.AreEqual(plan.WestXDm, placement.Position.x);
                Assert.AreEqual(plan.SouthZDm, placement.Position.z);
                Assert.AreEqual(1, catalogue.Rules[0].ExplicitCount);

                int roadY = KentridgeVerticalProfile.SurfaceYAtDm(
                    KentridgeTownPlanner.MainSpineXDm,
                    plan.CentreZDm,
                    Seed,
                    1);
                Assert.AreEqual(40, placement.Position.y - roadY,
                    "Bridge underside must preserve the semantic four-metre road clearance.");

                int boxes = 0;
                int pc = definition.ProgramOffset;
                int end = pc + definition.ProgramLength;
                while (pc < end)
                {
                    ShapeOp op = (ShapeOp)catalogue.Program[pc];
                    int instruction = ShapeOps.InstructionLength(op);
                    Assert.Greater(instruction, 0);
                    if (op == ShapeOp.End) break;
                    if (op == ShapeOp.EmitBox)
                    {
                        boxes++;
                        Assert.GreaterOrEqual(catalogue.Program[pc + 3], 0,
                            "The open upper street must not emit support geometry downward into the road clearance.");
                    }
                    pc += instruction;
                }

                Assert.GreaterOrEqual(boxes, 12,
                    "Bridge should include threshold steps, deck, parapets, and sparse open framing.");
                Assert.Less(KentridgeUpperSkybridgeCatalogue.BridgePrecedence, 94,
                    "Local block access remains the final circulation authority.");
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
