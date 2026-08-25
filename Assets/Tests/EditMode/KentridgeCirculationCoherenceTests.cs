using System;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeCirculationCoherenceTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void SecondaryParallelStairStreetsKeepTownSpacingFromMainSpine()
        {
            KentridgeUrbanCirculationPlan plan = KentridgeUrbanCirculation.Build(Seed);
            int roadMinX = KentridgeTownPlanner.MainSpineXDm
                           - KentridgeTownPlanner.MainRoadWidthDm / 2;
            int roadMaxX = KentridgeTownPlanner.MainSpineXDm
                           + KentridgeTownPlanner.MainRoadWidthDm / 2;
            int requiredGap = KentridgeTownPlanner.CompositionPolicy.Density.MinSpacingDm;

            for (int i = 0; i < plan.Connectors.Count; i++)
            {
                KentridgeUrbanConnector connector = plan.Connectors[i];
                if (connector.Kind != KentridgeUrbanConnectorKind.StairStreet
                    || !connector.IsVertical)
                    continue;

                int connectorMinX = connector.StartDm.X - connector.WidthDm / 2;
                int connectorMaxX = connector.StartDm.X + connector.WidthDm / 2;
                int gap = connectorMaxX <= roadMinX
                    ? roadMinX - connectorMaxX
                    : connectorMinX >= roadMaxX
                        ? connectorMinX - roadMaxX
                        : 0;

                Assert.GreaterOrEqual(
                    gap,
                    requiredGap,
                    connector.Id + " runs parallel to the already-walkable main spine with only "
                    + gap + " dm of separation; independent circulation corridors must keep the "
                    + requiredGap + " dm town spacing instead of forming a duplicate stair bundle.");
            }
        }

        [Test]
        public void LowerTownSkeletonDoesNotAdvertiseDuplicateSecondaryStairChain()
        {
            KentridgeUrbanSkeletonPlan plan = KentridgeUrbanSkeleton.Build(Seed);
            bool retainedUpperWestStair = false;

            for (int i = 0; i < plan.Nodes.Count; i++)
            {
                Assert.AreNotEqual(
                    KentridgeUrbanNodeId.WestMarketLanding,
                    plan.Nodes[i].Id,
                    "The retired lower-west stair must not survive as a semantic landing after its "
                    + "geometry is removed; the primary spine already connects Residential Junction "
                    + "to Market Square.");
            }

            for (int i = 0; i < plan.Links.Count; i++)
            {
                KentridgeUrbanLink link = plan.Links[i];
                Assert.IsFalse(
                    link.Kind == KentridgeUrbanLinkKind.SecondaryStair
                    && (link.From == KentridgeUrbanNodeId.ResidentialJunction
                        || link.To == KentridgeUrbanNodeId.ResidentialJunction),
                    link.Id + " duplicates the primary lower-town route with a second stair chain.");

                retainedUpperWestStair |=
                    link.Kind == KentridgeUrbanLinkKind.SecondaryStair
                    && link.From == KentridgeUrbanNodeId.WestMarketJunction
                    && link.To == KentridgeUrbanNodeId.WestUpperLanding;
            }

            Assert.IsTrue(retainedUpperWestStair,
                "Retiring the duplicate lower stair must not erase the coherent upper-west route.");
        }

        [Test]
        public void ShallowLowerWardAccessGradesInsteadOfBuildingHardStairFlight()
        {
            KentridgeUrbanAccessPlan plan = KentridgeUrbanAccessPlanner.Build(Seed);
            KentridgeUrbanAccessRoute lower = FindRoute(plan, "lower-west-neighbourhood-access");
            KentridgeUrbanAccessRoute market = FindRoute(plan, "market-lower-block-access");

            Assert.LessOrEqual(lower.DoorLevelBelowShelfDm, 15,
                "The lower neighbourhood is expected to be a shallow grade transition.");
            Assert.Greater(market.DoorLevelBelowShelfDm, 15,
                "The market block is the control case for a genuinely deep stair transition.");

            FeatureCatalogue catalogue = KentridgeUrbanAccessCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);
            try
            {
                FeatureDefinition lowerDefinition = FindDefinition(
                    catalogue, "kentridge-access-lower-west-neighbourhood-access");
                FeatureDefinition marketDefinition = FindDefinition(
                    catalogue, "kentridge-access-market-lower-block-access");

                Assert.IsTrue(
                    ContainsOp(catalogue, lowerDefinition, ShapeOp.EmitRamp),
                    "A 1.1 m Lower Ward elevation change should grade into its court rather than "
                    + "becoming a monumental hard-stone stair flight beside the main ascent.");
                Assert.IsFalse(
                    ContainsOp(catalogue, marketDefinition, ShapeOp.EmitRamp),
                    "Deep market access should remain a legible stair; grading is only for shallow "
                    + "Lower Ward transitions.");
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        [Test]
        public void VerticalInfrastructureDoesNotOverlayDedicatedStairsOnContinuousMainRoad()
        {
            FeatureCatalogue catalogue = KentridgeVerticalConnectorCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);
            try
            {
                bool retainedWall = false;
                bool retainedCampanile = false;
                for (int i = 0; i < catalogue.Definitions.Length; i++)
                {
                    string name = catalogue.Definitions[i].Name.ToString();
                    Assert.IsFalse(
                        name.StartsWith("kentridge-stair-", StringComparison.Ordinal),
                        name + " duplicates the continuous supported main-road climb already emitted "
                        + "by KentridgeVerticalTownSurfaceCatalogue.");
                    retainedWall |= name.StartsWith(
                        "kentridge-infrastructure-", StringComparison.Ordinal)
                        && name.Contains("retaining");
                    retainedCampanile |= name == "kentridge-infrastructure-civic-campanile";
                }

                Assert.IsTrue(retainedWall,
                    "Removing duplicate road stairs must not remove hillside retaining architecture.");
                Assert.IsTrue(retainedCampanile,
                    "Removing duplicate road stairs must not remove the civic campanile.");
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        private static KentridgeUrbanAccessRoute FindRoute(
            KentridgeUrbanAccessPlan plan,
            string id)
        {
            for (int i = 0; i < plan.Routes.Count; i++)
                if (plan.Routes[i].Id == id) return plan.Routes[i];
            Assert.Fail("Missing Kentridge access route: " + id);
            return default;
        }

        private static FeatureDefinition FindDefinition(
            FeatureCatalogue catalogue,
            string name)
        {
            for (int i = 0; i < catalogue.Definitions.Length; i++)
                if (catalogue.Definitions[i].Name.ToString() == name)
                    return catalogue.Definitions[i];
            Assert.Fail("Missing Kentridge access definition: " + name);
            return default;
        }

        private static bool ContainsOp(
            FeatureCatalogue catalogue,
            FeatureDefinition definition,
            ShapeOp target)
        {
            int cursor = definition.ProgramOffset;
            int end = cursor + definition.ProgramLength;
            while (cursor < end)
            {
                ShapeOp op = (ShapeOp)catalogue.Program[cursor];
                if (op == target) return true;
                int length = ShapeOps.InstructionLength(op);
                Assert.Greater(length, 0, "Invalid shape instruction while scanning " + definition.Name);
                cursor += length;
            }
            Assert.AreEqual(end, cursor, "Shape program ended on a partial instruction.");
            return false;
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
