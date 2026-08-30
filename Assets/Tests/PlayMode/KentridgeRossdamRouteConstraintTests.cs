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
        public void ForestApproachRoutesAroundRossdamLakeOnDryGround()
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
            Assert.That(
                physical.TryGetRoute(
                    KentridgeTopDownWorldLayout.Forest,
                    KentridgeTopDownWorldLayout.FightingArea1,
                    out TopDownWorldPhysicalRoutePlan route),
                Is.True,
                "The source-backed forest -> fighting-area-1 hard route must remain physically realized.");
            Assert.That(route.GeographyConstrained, Is.True,
                "Rossdam's southward lake footprint must be solved by explicit route-around semantics.");

            for (var i = 0; i < route.Tiles.Count; i++)
            {
                Assert.That(
                    lake.Contains(route.Tiles[i], -(route.Route.CorridorWidthDm / 2)),
                    Is.False,
                    "The forest approach road corridor must stay dry instead of silently crossing Rossdam Lake.");
            }
        }
    }
}