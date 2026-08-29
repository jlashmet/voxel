using System;
using System.Collections.Generic;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Structures.Api;
using LegacyHightownDefinition = MountingForce.WorldGen.Content.Hightown.HightownDefinition;
using LegacyKentridgeDefinition = MountingForce.WorldGen.Content.Kentridge.KentridgeDefinition;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class KentridgeTopDownWorldLayoutTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void SourceBackedWorldLayoutIsDeterministicPhysicallyRealizedAndRejectsSeveredHardRoute()
        {
            TopDownWorldLayoutSpec spec = MountingForceTopDownWorldDefinition.BuildSpec();

            Assert.That(
                TopDownWorldLayoutPlanner.TryPlan(spec, Seed, out TopDownWorldLayout first, out string firstError),
                Is.True,
                firstError);
            Assert.That(
                TopDownWorldLayoutPlanner.TryPlan(spec, Seed, out TopDownWorldLayout second, out string secondError),
                Is.True,
                secondError);

            Assert.That(first.Nodes.Count, Is.EqualTo(21));
            Assert.That(first.Routes.Count, Is.EqualTo(20));
            Assert.That(first.RootId, Is.EqualTo(MountingForceTopDownWorldDefinition.Kentridge));

            var occupied = new HashSet<TopDownWorldGridPoint>();
            for (var i = 0; i < first.Nodes.Count; i++)
            {
                TopDownWorldNodePlacement node = first.Nodes[i];
                Assert.That(second.TryGetPosition(node.Node.Id, out TopDownWorldGridPoint replay), Is.True);
                Assert.That(replay, Is.EqualTo(node.Position),
                    "Identical source graph + seed must replay to identical top-down positions.");
                Assert.That(occupied.Add(node.Position), Is.True,
                    $"Macro destinations must not overlap: {node.Node.Id} at {node.Position}.");
                Assert.That(node.Node.EnvelopeHalfExtentDm, Is.GreaterThan(0));
                StringAssert.Contains("world-procgen-clusters.yaml", node.Node.Source);
            }

            AssertHardRoute(spec, MountingForceTopDownWorldDefinition.Kentridge, MountingForceTopDownWorldDefinition.Overworld);
            AssertHardRoute(spec, MountingForceTopDownWorldDefinition.Kentridge, MountingForceTopDownWorldDefinition.Mountains);
            AssertHardRoute(spec, MountingForceTopDownWorldDefinition.Kentridge, MountingForceTopDownWorldDefinition.RadcliffeMansion);
            AssertHardRoute(spec, MountingForceTopDownWorldDefinition.Overworld, MountingForceTopDownWorldDefinition.Forest);
            AssertHardRoute(spec, MountingForceTopDownWorldDefinition.Overworld, MountingForceTopDownWorldDefinition.StanleyHouse);
            AssertHardRoute(spec, MountingForceTopDownWorldDefinition.FightingArea2, MountingForceTopDownWorldDefinition.Hightown);
            AssertHardRoute(spec, MountingForceTopDownWorldDefinition.FightingArea1, MountingForceTopDownWorldDefinition.BanditHideout);
            AssertHardRoute(spec, MountingForceTopDownWorldDefinition.MoordellCorridor, MountingForceTopDownWorldDefinition.Moordell);
            AssertHardRoute(spec, MountingForceTopDownWorldDefinition.RossdamRegion, MountingForceTopDownWorldDefinition.Rossdam);
            AssertHardRoute(spec, MountingForceTopDownWorldDefinition.SouthFightingArea, MountingForceTopDownWorldDefinition.FairyVillage);
            AssertHardRoute(spec, MountingForceTopDownWorldDefinition.SouthFightingArea, MountingForceTopDownWorldDefinition.OrcVillage);
            AssertHardRoute(spec, MountingForceTopDownWorldDefinition.LoganApproach, MountingForceTopDownWorldDefinition.LoganCastle);

            Assert.That(first.CanReach(
                MountingForceTopDownWorldDefinition.Kentridge,
                MountingForceTopDownWorldDefinition.Hightown), Is.True);
            Assert.That(first.CanReach(
                MountingForceTopDownWorldDefinition.Kentridge,
                MountingForceTopDownWorldDefinition.Rossdam), Is.True);
            Assert.That(first.CanReach(
                MountingForceTopDownWorldDefinition.Kentridge,
                MountingForceTopDownWorldDefinition.LoganCastle), Is.True);

            TopDownWorldVoxelPlan physical = TopDownWorldVoxelCatalogue.Plan(
                first,
                LegacyKentridgeDefinition.TownCentreDm,
                MountingForceTopDownWorldDefinition.CellSizeDm);
            Assert.That(physical.Nodes.Count, Is.EqualTo(first.Nodes.Count));
            Assert.That(physical.Routes.Count, Is.EqualTo(first.Routes.Count));
            Assert.That(physical.RouteTileCount, Is.GreaterThan(first.Routes.Count));
            Assert.That(
                physical.TryGetNodeCentre(MountingForceTopDownWorldDefinition.Hightown, out Int2 hightownCentre),
                Is.True);
            Assert.That(hightownCentre.X, Is.EqualTo(LegacyHightownDefinition.TownCentreDm.X),
                "Macro Hightown must resolve to the already-generated Hightown anchor, not a second location.");
            Assert.That(hightownCentre.Y, Is.EqualTo(LegacyHightownDefinition.TownCentreDm.Y));

            TopDownWorldVoxelRoutePlan kentridgeExit = FindPhysicalRoute(
                physical,
                MountingForceTopDownWorldDefinition.Kentridge,
                MountingForceTopDownWorldDefinition.Overworld);
            Assert.That(kentridgeExit.Tiles.Count, Is.GreaterThan(1));
            Assert.That(kentridgeExit.Tiles[0].X, Is.EqualTo(LegacyKentridgeDefinition.TownCentreDm.X));
            Assert.That(kentridgeExit.Tiles[0].Y, Is.EqualTo(LegacyKentridgeDefinition.TownCentreDm.Y));
            AssertContinuousTiles(kentridgeExit.Tiles);

            var materials = new VoxelMaterialMap(
                foundationStone: 1,
                masonry: 2,
                darkMasonry: 3,
                timber: 4,
                glass: 5,
                warmWindow: 6,
                roofTile: 7,
                slate: 8,
                cloth: 9,
                moss: 10,
                water: 11,
                roadSurface: 12);
            FeatureCatalogue catalogue = default;
            try
            {
                catalogue = TopDownWorldVoxelCatalogue.Build(
                    first,
                    LegacyKentridgeDefinition.TownCentreDm,
                    MountingForceTopDownWorldDefinition.CellSizeDm,
                    new VoxelWorldGenSettings(1, materials),
                    Allocator.Temp);
                Assert.That(catalogue.IsCreated, Is.True);
                Assert.That(catalogue.Definitions.Length, Is.EqualTo(41),
                    "20 verified route definitions + 21 neutral destination markers are production output.");
                Assert.That(catalogue.ExplicitPlacements.Length, Is.GreaterThan(41));
            }
            finally
            {
                if (catalogue.IsCreated) catalogue.Dispose();
            }

            // Remove one verified leaf edge without removing its destination. The planner must
            // reject the severed story/traversal route rather than quietly place a plausible marker.
            var severedRoutes = new List<TopDownWorldRouteSpec>();
            for (var i = 0; i < spec.Routes.Count; i++)
            {
                TopDownWorldRouteSpec route = spec.Routes[i];
                if (string.Equals(route.FromId, MountingForceTopDownWorldDefinition.LoganApproach, StringComparison.Ordinal)
                    && string.Equals(route.ToId, MountingForceTopDownWorldDefinition.LoganCastle, StringComparison.Ordinal))
                    continue;
                severedRoutes.Add(route);
            }
            var severed = new TopDownWorldLayoutSpec(spec.RootId, spec.Nodes, severedRoutes);
            Assert.That(
                TopDownWorldLayoutPlanner.TryPlan(severed, Seed, out _, out string severedError),
                Is.False,
                "A hard route cannot be silently severed by placement/layout changes.");
            StringAssert.Contains("unreachable", severedError.ToLowerInvariant());

            TestContext.WriteLine(
                $"TOPDOWNLAYOUT nodes={first.Nodes.Count} hardRoutes={first.Routes.Count} " +
                $"routeTiles={physical.RouteTileCount} hightown=({hightownCentre.X},{hightownCentre.Y}) " +
                $"kentridgeExitTiles={kentridgeExit.Tiles.Count}");
        }

        private static void AssertHardRoute(TopDownWorldLayoutSpec spec, string from, string to)
        {
            for (var i = 0; i < spec.Routes.Count; i++)
            {
                TopDownWorldRouteSpec route = spec.Routes[i];
                if (!string.Equals(route.FromId, from, StringComparison.Ordinal)
                    || !string.Equals(route.ToId, to, StringComparison.Ordinal))
                    continue;

                Assert.That(route.IsHard, Is.True);
                Assert.That(route.EvidenceKind, Is.EqualTo(TopDownWorldEvidenceKind.VerifiedTransition));
                StringAssert.Contains("world-procgen-clusters.yaml", route.Evidence);
                Assert.That(route.PlacementEvidence, Is.Not.Empty);
                Assert.That(route.CorridorWidthDm, Is.GreaterThan(TopDownWorldVoxelCatalogue.RouteTileStepDm));
                return;
            }

            Assert.Fail($"Missing verified legacy traversal relationship {from}->{to}.");
        }

        private static TopDownWorldVoxelRoutePlan FindPhysicalRoute(
            TopDownWorldVoxelPlan plan,
            string from,
            string to)
        {
            for (var i = 0; i < plan.Routes.Count; i++)
            {
                TopDownWorldVoxelRoutePlan route = plan.Routes[i];
                if (string.Equals(route.Route.FromId, from, StringComparison.Ordinal)
                    && string.Equals(route.Route.ToId, to, StringComparison.Ordinal))
                    return route;
            }
            Assert.Fail($"Missing physical macro route {from}->{to}.");
            return null;
        }

        private static void AssertContinuousTiles(IReadOnlyList<Int2> tiles)
        {
            for (var i = 1; i < tiles.Count; i++)
            {
                int dx = Math.Abs(tiles[i].X - tiles[i - 1].X);
                int dz = Math.Abs(tiles[i].Y - tiles[i - 1].Y);
                Assert.That(dx == 0 || dz == 0, Is.True, "A physical route segment must stay axis aligned.");
                Assert.That(dx + dz, Is.LessThanOrEqualTo(TopDownWorldVoxelCatalogue.RouteTileStepDm));
                Assert.That(dx + dz, Is.GreaterThan(0));
            }
        }
    }
}