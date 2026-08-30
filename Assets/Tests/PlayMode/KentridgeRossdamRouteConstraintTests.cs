using System;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using LegacyKentridgeDefinition = MountingForce.WorldGen.Content.Kentridge.KentridgeDefinition;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class KentridgeRossdamRouteConstraintTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void RossdamLakeKeepsNorthernJunctionDryWhileConstrainingOutboundAndRossdamRoads()
        {
            TopDownWorldLayout layout = MountingForceTopDownWorldDefinition.Build(Seed);
            TopDownWorldPhysicalIntentSpec intent = KentridgeTopDownWorldPhysicalIntent.Build();
            TopDownWorldPhysicalPlan physical = TopDownWorldPhysicalPlanner.Plan(
                layout,
                intent,
                LegacyKentridgeDefinition.TownCentreDm,
                MountingForceTopDownWorldDefinition.CellSizeDm,
                voxelsPerDecimetre: 1);

            Assert.That(
                physical.TryGetRegion(KentridgeTopDownWorldPhysicalIntent.RossdamLake, out TopDownWorldRegionPlan lake),
                Is.True);

            TopDownWorldPhysicalRoutePlan inbound = FindRoute(
                physical,
                KentridgeTopDownWorldLayout.Forest,
                KentridgeTopDownWorldLayout.FightingArea1);
            Int2 junction = FindNodeCentre(physical, KentridgeTopDownWorldLayout.FightingArea1);
            int inboundMargin = inbound.Route.CorridorWidthDm / 2;
            Assert.That(lake.Contains(junction, -inboundMargin), Is.False,
                "A route-around solution cannot start from a road junction engulfed by Rossdam Lake.");
            Assert.That(inbound.GeographyConstrained, Is.False,
                "The inbound forest road should reach the dry fighting-area junction before lake routing begins.");
            AssertDry(lake, inbound, "The inbound forest road corridor must stay dry.");

            TopDownWorldPhysicalRoutePlan north = FindRoute(
                physical,
                KentridgeTopDownWorldLayout.FightingArea1,
                KentridgeTopDownWorldLayout.FightingArea2);
            Assert.That(north.GeographyConstrained, Is.True,
                "The outbound northern road must still physically exercise Rossdam Lake's GoAround semantics.");
            AssertDry(lake, north, "The solved outbound northern road corridor must stay dry.");

            TopDownWorldPhysicalRoutePlan rossdam = FindRoute(
                physical,
                KentridgeTopDownWorldLayout.MoordellCorridor,
                KentridgeTopDownWorldLayout.RossdamApproach);
            Assert.That(rossdam.GeographyConstrained, Is.True,
                "Rossdam Lake must remain a real obstruction on the direct Rossdam approach.");
            AssertDry(lake, rossdam, "The solved Rossdam approach corridor must stay dry.");
        }

        private static TopDownWorldPhysicalRoutePlan FindRoute(
            TopDownWorldPhysicalPlan physical,
            string from,
            string to)
        {
            Assert.That(physical.TryGetRoute(from, to, out TopDownWorldPhysicalRoutePlan route), Is.True,
                "Missing physical hard route " + from + "->" + to);
            return route;
        }

        private static Int2 FindNodeCentre(TopDownWorldPhysicalPlan physical, string nodeId)
        {
            for (var i = 0; i < physical.Nodes.Count; i++)
                if (string.Equals(physical.Nodes[i].Node.Id, nodeId, StringComparison.Ordinal))
                    return physical.Nodes[i].CentreDm;
            Assert.Fail("Missing physical node centre for " + nodeId);
            return default;
        }

        private static void AssertDry(
            TopDownWorldRegionPlan lake,
            TopDownWorldPhysicalRoutePlan route,
            string message)
        {
            int margin = route.Route.CorridorWidthDm / 2;
            for (var i = 0; i < route.Tiles.Count; i++)
                Assert.That(lake.Contains(route.Tiles[i], -margin), Is.False, message);
        }
    }
}