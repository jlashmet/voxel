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
    public sealed class KentridgeMacroWorldPhysicalRealizationTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void MacroGraphRealizesSettlementsTerrainAwareRoadsAndGeographyThroughProductionWorldBuilder()
        {
            TopDownWorldLayout layout = MountingForceTopDownWorldDefinition.Build(Seed);
            TopDownWorldPhysicalIntentSpec intent = KentridgeTopDownWorldPhysicalIntent.Build();
            VoxelWorldGenSettings settings = Settings();

            TopDownWorldPhysicalPlan first = TopDownWorldPhysicalVoxelCatalogue.Plan(
                layout,
                intent,
                LegacyKentridgeDefinition.TownCentreDm,
                MountingForceTopDownWorldDefinition.CellSizeDm,
                settings);
            TopDownWorldPhysicalPlan replay = TopDownWorldPhysicalVoxelCatalogue.Plan(
                layout,
                intent,
                LegacyKentridgeDefinition.TownCentreDm,
                MountingForceTopDownWorldDefinition.CellSizeDm,
                settings);

            Assert.That(first.Nodes.Count, Is.EqualTo(layout.Nodes.Count));
            Assert.That(first.Routes.Count, Is.EqualTo(20));
            Assert.That(first.Settlements.Count, Is.EqualTo(6));
            Assert.That(first.Regions.Count, Is.EqualTo(6));
            Assert.That(first.BuildingCount, Is.EqualTo(16),
                "Four currently-unrealized settlements require four reusable blockout buildings each.");
            Assert.That(first.GeographyConstrainedRouteCount, Is.GreaterThanOrEqualTo(3));
            Assert.That(first.RouteSolveSteps, Is.GreaterThan(first.RouteTileCount));

            AssertDeterministic(first, replay);
            AssertEverySettlementRealized(layout, first);
            AssertEveryHardRouteContinuousAndGroundedToSettlements(layout, first);
            AssertRegionalGeographyAndConstraints(first);
            AssertExistingRichSettlementsPreserved(first);
            AssertBlockedHardRouteRequiresSemanticSolution(settings);

            FeatureCatalogue catalogue = default;
            try
            {
                catalogue = TopDownWorldPhysicalVoxelCatalogue.Build(
                    layout,
                    intent,
                    LegacyKentridgeDefinition.TownCentreDm,
                    MountingForceTopDownWorldDefinition.CellSizeDm,
                    settings,
                    Allocator.Temp);
                Assert.That(catalogue.IsCreated, Is.True);
                Assert.That(catalogue.Definitions.Length, Is.GreaterThanOrEqualTo(46),
                    "Physical macro output must include regions, every road, four street plans, and sixteen buildings.");
                Assert.That(catalogue.ExplicitPlacements.Length, Is.GreaterThan(first.RouteTileCount + first.BuildingCount));
                Assert.That(catalogue.Definitions.Length, Is.LessThan(FeatureBudget.MaxDefinitions));
            }
            finally
            {
                if (catalogue.IsCreated) catalogue.Dispose();
            }

            Assert.That(layout.CanReach(layout.RootId, MountingForceTopDownWorldDefinition.Moordell), Is.True);
            Assert.That(layout.CanReach(layout.RootId, MountingForceTopDownWorldDefinition.Rossdam), Is.True);
            Assert.That(layout.CanReach(layout.RootId, MountingForceTopDownWorldDefinition.FairyVillage), Is.True);
            Assert.That(layout.CanReach(layout.RootId, MountingForceTopDownWorldDefinition.OrcVillage), Is.True);
            Assert.That(layout.CanReach(layout.RootId, MountingForceTopDownWorldDefinition.Hightown), Is.True);

            TestContext.WriteLine(
                "MACRO_PHYSICAL " +
                $"regions={first.Regions.Count} settlements={first.Settlements.Count} buildings={first.BuildingCount} " +
                $"hardRoutes={first.Routes.Count} routeTiles={first.RouteTileCount} " +
                $"constrainedRoutes={first.GeographyConstrainedRouteCount} solveSteps={first.RouteSolveSteps}");
        }

        private static void AssertDeterministic(
            TopDownWorldPhysicalPlan first,
            TopDownWorldPhysicalPlan replay)
        {
            Assert.That(replay.Regions.Count, Is.EqualTo(first.Regions.Count));
            for (var i = 0; i < first.Regions.Count; i++)
            {
                Assert.That(replay.Regions[i].Spec.Id, Is.EqualTo(first.Regions[i].Spec.Id));
                AssertPoint(replay.Regions[i].CentreDm, first.Regions[i].CentreDm);
                Assert.That(replay.Regions[i].HalfExtentXDm, Is.EqualTo(first.Regions[i].HalfExtentXDm));
                Assert.That(replay.Regions[i].HalfExtentZDm, Is.EqualTo(first.Regions[i].HalfExtentZDm));
                Assert.That(replay.Regions[i].ElevationDeltaDm, Is.EqualTo(first.Regions[i].ElevationDeltaDm));
            }

            Assert.That(replay.Routes.Count, Is.EqualTo(first.Routes.Count));
            for (var i = 0; i < first.Routes.Count; i++)
            {
                Assert.That(replay.Routes[i].Route.Key, Is.EqualTo(first.Routes[i].Route.Key));
                Assert.That(replay.Routes[i].Tiles.Count, Is.EqualTo(first.Routes[i].Tiles.Count));
                for (var p = 0; p < first.Routes[i].Tiles.Count; p++)
                    AssertPoint(replay.Routes[i].Tiles[p], first.Routes[i].Tiles[p]);
            }
        }

        private static void AssertEverySettlementRealized(
            TopDownWorldLayout layout,
            TopDownWorldPhysicalPlan physical)
        {
            for (var i = 0; i < layout.Nodes.Count; i++)
            {
                TopDownWorldNodeSpec node = layout.Nodes[i].Node;
                if (node.Kind != TopDownWorldNodeKind.Settlement) continue;
                Assert.That(physical.TryGetSettlement(node.Id, out TopDownWorldSettlementPlan settlement), Is.True,
                    "Every semantic settlement must have one physical realization plan: " + node.Id);

                if (settlement.RealizationKind == TopDownWorldSettlementRealizationKind.ExistingRichGeneration)
                    continue;

                Assert.That(settlement.Buildings.Count, Is.GreaterThanOrEqualTo(4));
                for (var b = 0; b < settlement.Buildings.Count; b++)
                {
                    TopDownWorldBuildingBlockoutPlan building = settlement.Buildings[b];
                    Assert.That(
                        Math.Abs(building.CentreDm.X - settlement.CentreDm.X) + building.HalfExtentXDm,
                        Is.LessThanOrEqualTo(node.EnvelopeHalfExtentDm));
                    Assert.That(
                        Math.Abs(building.CentreDm.Y - settlement.CentreDm.Y) + building.HalfExtentZDm,
                        Is.LessThanOrEqualTo(node.EnvelopeHalfExtentDm));
                    for (var other = 0; other < b; other++)
                        Assert.That(building.Overlaps(settlement.Buildings[other], 24), Is.False,
                            "Generic blockout buildings must retain circulation/clearance.");
                }
            }
        }

        private static void AssertEveryHardRouteContinuousAndGroundedToSettlements(
            TopDownWorldLayout layout,
            TopDownWorldPhysicalPlan physical)
        {
            var hardRoutes = 0;
            for (var i = 0; i < layout.Routes.Count; i++)
            {
                TopDownWorldRouteSpec semantic = layout.Routes[i];
                if (!semantic.IsHard) continue;
                hardRoutes++;
                Assert.That(physical.TryGetRoute(semantic.FromId, semantic.ToId, out TopDownWorldPhysicalRoutePlan route), Is.True);
                Assert.That(route.Tiles.Count, Is.GreaterThan(1));
                Int2 from = FindNodeCentre(physical, semantic.FromId);
                Int2 to = FindNodeCentre(physical, semantic.ToId);
                AssertPoint(route.Tiles[0], from);
                AssertPoint(route.Tiles[route.Tiles.Count - 1], to);
                for (var p = 1; p < route.Tiles.Count; p++)
                {
                    int dx = Math.Abs(route.Tiles[p].X - route.Tiles[p - 1].X);
                    int dz = Math.Abs(route.Tiles[p].Y - route.Tiles[p - 1].Y);
                    Assert.That(dx == 0 || dz == 0, Is.True);
                    Assert.That(dx + dz, Is.InRange(1, TopDownWorldPhysicalPlanner.RouteTileStepDm));
                }
            }
            Assert.That(hardRoutes, Is.EqualTo(physical.Routes.Count));
        }

        private static void AssertRegionalGeographyAndConstraints(TopDownWorldPhysicalPlan physical)
        {
            Assert.That(
                physical.TryGetRegion(KentridgeTopDownWorldPhysicalIntent.RossdamLake, out TopDownWorldRegionPlan lake),
                Is.True);
            Assert.That(lake.Spec.Kind, Is.EqualTo(TopDownWorldRegionKind.WaterBody));
            Assert.That(lake.HalfExtentXDm * 2, Is.GreaterThanOrEqualTo(900), "The lake must read as a macro landmark.");
            Assert.That(lake.HalfExtentZDm * 2, Is.GreaterThanOrEqualTo(450));

            Assert.That(
                physical.TryGetRegion(KentridgeTopDownWorldPhysicalIntent.SouthernRidge, out TopDownWorldRegionPlan ridge),
                Is.True);
            Assert.That(ridge.Spec.Kind, Is.EqualTo(TopDownWorldRegionKind.MountainRidge));
            Assert.That(ridge.ElevationDeltaDm, Is.GreaterThanOrEqualTo(90));
            Assert.That(
                physical.TryGetRegion(KentridgeTopDownWorldPhysicalIntent.SouthernPass, out TopDownWorldRegionPlan pass),
                Is.True);
            Assert.That(pass.Spec.Kind, Is.EqualTo(TopDownWorldRegionKind.ValleyPass));
            Assert.That(ridge.Contains(pass.CentreDm), Is.True);

            TopDownWorldPhysicalRoutePlan rossdam = FindRoute(
                physical,
                MountingForceTopDownWorldDefinition.MoordellCorridor,
                MountingForceTopDownWorldDefinition.RossdamApproach);
            Assert.That(rossdam.GeographyConstrained, Is.True);
            for (var i = 0; i < rossdam.Tiles.Count; i++)
                Assert.That(lake.Contains(rossdam.Tiles[i], -(rossdam.Route.CorridorWidthDm / 2)), Is.False,
                    "Route-around solution must keep the road corridor out of the lake.");

            TopDownWorldPhysicalRoutePlan north = FindRoute(
                physical,
                MountingForceTopDownWorldDefinition.FightingArea1,
                MountingForceTopDownWorldDefinition.FightingArea2);
            Assert.That(north.GeographyConstrained, Is.True);
            for (var i = 0; i < north.Tiles.Count; i++)
                Assert.That(lake.Contains(north.Tiles[i], -(north.Route.CorridorWidthDm / 2)), Is.False);

            TopDownWorldPhysicalRoutePlan logan = FindRoute(
                physical,
                MountingForceTopDownWorldDefinition.SouthFightingArea,
                MountingForceTopDownWorldDefinition.LoganApproach);
            Assert.That(logan.GeographyConstrained, Is.True);
            var enteredRidge = false;
            for (var i = 0; i < logan.Tiles.Count; i++)
            {
                if (!ridge.Contains(logan.Tiles[i], -(logan.Route.CorridorWidthDm / 2))) continue;
                enteredRidge = true;
                Assert.That(pass.Contains(logan.Tiles[i], -(logan.Route.CorridorWidthDm / 2)), Is.True,
                    "The ridge may only be crossed inside the authored pass envelope.");
            }
            Assert.That(enteredRidge, Is.True, "The Logan route must physically exercise its ridge pass.");

            Assert.That(physical.TryGetRegion(KentridgeTopDownWorldPhysicalIntent.KentridgeMeadow, out var meadow), Is.True);
            Assert.That(meadow.Spec.Kind, Is.EqualTo(TopDownWorldRegionKind.PlainsMeadow));
            Assert.That(physical.TryGetRegion(KentridgeTopDownWorldPhysicalIntent.NorthernWoodland, out var woodland), Is.True);
            Assert.That(woodland.Spec.Kind, Is.EqualTo(TopDownWorldRegionKind.ForestWoodland));
            Assert.That(physical.TryGetRegion(KentridgeTopDownWorldPhysicalIntent.RossdamCountryside, out var country), Is.True);
            Assert.That(country.Spec.Kind, Is.EqualTo(TopDownWorldRegionKind.Generic));
        }

        private static void AssertExistingRichSettlementsPreserved(TopDownWorldPhysicalPlan physical)
        {
            Assert.That(physical.TryGetSettlement(MountingForceTopDownWorldDefinition.Kentridge, out var kentridge), Is.True);
            Assert.That(kentridge.RealizationKind, Is.EqualTo(TopDownWorldSettlementRealizationKind.ExistingRichGeneration));
            Assert.That(kentridge.Buildings.Count, Is.Zero,
                "The macro pass must not replace the richer existing Kentridge generator with generic blockouts.");
            AssertPoint(kentridge.CentreDm, LegacyKentridgeDefinition.TownCentreDm);

            Assert.That(physical.TryGetSettlement(MountingForceTopDownWorldDefinition.Hightown, out var hightown), Is.True);
            Assert.That(hightown.RealizationKind, Is.EqualTo(TopDownWorldSettlementRealizationKind.ExistingRichGeneration));
            Assert.That(hightown.Buildings.Count, Is.Zero);
            AssertPoint(hightown.CentreDm, LegacyHightownDefinition.TownCentreDm);
        }

        private static void AssertBlockedHardRouteRequiresSemanticSolution(VoxelWorldGenSettings settings)
        {
            var a = new TopDownWorldNodeSpec("a", "A", TopDownWorldNodeKind.Settlement, 600, "test graph");
            var b = new TopDownWorldNodeSpec("b", "B", TopDownWorldNodeKind.Settlement, 600, "test graph");
            var route = new TopDownWorldRouteSpec(
                "a",
                "b",
                new TopDownWorldGridPoint(2, 0),
                TopDownWorldEvidenceKind.VerifiedTransition,
                "test verified route",
                "test physical placement",
                36);
            var layout = new TopDownWorldLayout(
                "a",
                Seed,
                new[]
                {
                    new TopDownWorldNodePlacement(a, new TopDownWorldGridPoint(0, 0)),
                    new TopDownWorldNodePlacement(b, new TopDownWorldGridPoint(2, 0))
                },
                new[] { route });
            var water = new TopDownWorldRegionSpec(
                "barrier-water",
                "Barrier Water",
                TopDownWorldRegionKind.WaterBody,
                TopDownWorldRegionRelationKind.Between,
                "a",
                "b",
                300,
                300,
                -40,
                source: "test blocker");
            var settlements = new[]
            {
                new TopDownWorldSettlementPhysicalSpec("a", TopDownWorldSettlementRealizationKind.GenericBlockout),
                new TopDownWorldSettlementPhysicalSpec("b", TopDownWorldSettlementRealizationKind.GenericBlockout)
            };
            var unsolved = new TopDownWorldPhysicalIntentSpec(
                new[] { water },
                Array.Empty<TopDownWorldRouteRegionConstraintSpec>(),
                settlements);

            Assert.That(
                TopDownWorldPhysicalPlanner.TryPlan(
                    layout,
                    unsolved,
                    new Int2(0, 0),
                    800,
                    settings.VoxelsPerDecimetre,
                    out _,
                    out string blockedError),
                Is.False);
            StringAssert.Contains("no authored", blockedError.ToLowerInvariant());

            var solved = new TopDownWorldPhysicalIntentSpec(
                new[] { water },
                new[]
                {
                    new TopDownWorldRouteRegionConstraintSpec(
                        "a",
                        "b",
                        "barrier-water",
                        TopDownWorldRouteRegionSolutionKind.GoAround,
                        clearanceDm: 60,
                        source: "test route-around")
                },
                settlements);
            TopDownWorldPhysicalPlan detour = TopDownWorldPhysicalPlanner.Plan(
                layout,
                solved,
                new Int2(0, 0),
                800,
                settings.VoxelsPerDecimetre);
            Assert.That(detour.Routes[0].GeographyConstrained, Is.True);
            Assert.That(detour.Routes[0].Tiles.Count, Is.GreaterThan(2));
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

        private static void AssertPoint(Int2 actual, Int2 expected)
        {
            Assert.That(actual.X, Is.EqualTo(expected.X));
            Assert.That(actual.Y, Is.EqualTo(expected.Y));
        }

        private static VoxelWorldGenSettings Settings()
        {
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
            return new VoxelWorldGenSettings(1, materials);
        }
    }
}
