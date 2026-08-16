using System;
using System.Linq;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class WorldHierarchyBlueprintTests
    {
        [Test]
        public void StartingSettlementAndSitesCompileToExplicitHierarchyDependencies()
        {
            var game = Campaign.Create("hierarchy-test");

            RegionHandle region = game.World.Region(
                "starting-region",
                value => value.Biome(BiomeFamily.TemperateForest));

            RouteHandle route = region.TradeRoad(
                "main-route",
                value => value.Importance(RouteImportance.Primary));

            SettlementHandle town = region.Town(
                "starting-town",
                value => value
                    .Population(150, 450)
                    .ConnectTo(route, new DistanceRangeMetres(0, 80)));

            SiteHandle pub = town.Pub(
                "starting-pub",
                site => site
                    .RequireCapability(SiteCapability.Interior)
                    .RequireCapability(SiteCapability.PublicExit));

            SiteHandle destination = region.Site(
                "first-destination",
                SiteArchetype.Ruin,
                site => site
                    .DifferentSiteFrom(pub)
                    .ReachableFrom(pub, TraversalProfile.NormalParty));

            CampaignBlueprint blueprint = game.Build();
            BlueprintValidationResult validation = BlueprintValidator.Validate(blueprint);
            Assert.That(validation.IsValid, Is.True);
            Assert.That(validation.Diagnostics.Any(d => d.Code == "WB1402"), Is.False);

            SettlementRouteAccessSpec access = blueprint.Hierarchy.RouteAccess.Single();
            Assert.That(access.Settlement, Is.EqualTo(town.Ref));
            Assert.That(access.Route, Is.EqualTo(route.Ref));
            Assert.That(access.ConnectorLengthMetres.Minimum, Is.EqualTo(0));
            Assert.That(access.ConnectorLengthMetres.Maximum, Is.EqualTo(80));

            PlanningGraph graph = BlueprintCompiler.Compile(blueprint);
            PlanningNode routeNode = graph.Nodes.Single(n => n.Id == "route:main-route");
            PlanningNode townNode = graph.Nodes.Single(n => n.Id == "settlement:starting-town");
            PlanningNode pubNode = graph.Nodes.Single(n => n.Id == "site:starting-pub");
            PlanningNode destinationNode = graph.Nodes.Single(n => n.Id == "site:first-destination");

            Assert.That(routeNode.Dependencies, Is.EquivalentTo(new[] { "region:starting-region" }));
            Assert.That(townNode.Dependencies, Does.Contain("region:starting-region"));
            Assert.That(townNode.Dependencies, Does.Contain("route:main-route"));
            Assert.That(pubNode.Dependencies, Is.EquivalentTo(new[] { "settlement:starting-town" }));
            Assert.That(destinationNode.Dependencies, Is.EquivalentTo(new[] { "region:starting-region" }));

            WorldRegionPlan regionPlan = graph.HierarchyPlan.Regions.Single(value => value.Region.Equals(region.Ref));
            Assert.That(regionPlan.Biome, Is.EqualTo(BiomeFamily.TemperateForest));
            Assert.That(regionPlan.Routes, Is.EquivalentTo(new[] { route.Ref }));
            Assert.That(regionPlan.Settlements, Is.EquivalentTo(new[] { town.Ref }));
            Assert.That(regionPlan.RegionOwnedSites, Is.EquivalentTo(new[] { destination.Ref }));

            WorldRoutePlan routePlan = graph.HierarchyPlan.Routes.Single(value => value.Route.Equals(route.Ref));
            Assert.That(routePlan.Region, Is.EqualTo(region.Ref));
            Assert.That(routePlan.Kind, Is.EqualTo(RouteKind.TradeRoad));
            Assert.That(routePlan.Importance, Is.EqualTo(RouteImportance.Primary));
            WorldRouteAccessPlan routeAccess = routePlan.SettlementAccess.Single();
            Assert.That(routeAccess.Settlement, Is.EqualTo(town.Ref));
            Assert.That(routeAccess.ConnectorLengthMetres.Minimum, Is.EqualTo(0));
            Assert.That(routeAccess.ConnectorLengthMetres.Maximum, Is.EqualTo(80));

            WorldSettlementPlan townPlan = graph.HierarchyPlan.Settlements.Single(value => value.Settlement.Equals(town.Ref));
            Assert.That(townPlan.Region, Is.EqualTo(region.Ref));
            Assert.That(townPlan.Archetype, Is.EqualTo(SettlementArchetype.Town));
            Assert.That(townPlan.HasPopulationRange, Is.True);
            Assert.That(townPlan.Population.Minimum, Is.EqualTo(150));
            Assert.That(townPlan.Population.Maximum, Is.EqualTo(450));
            Assert.That(townPlan.Sites, Is.EquivalentTo(new[] { pub.Ref }));
            Assert.That(townPlan.RouteAccess.Single(), Is.SameAs(routeAccess),
                "Route and settlement plans must share the same compiled access requirement object.");

            WorldSitePlacementPlan pubPlacement = graph.HierarchyPlan.SitePlacements
                .Single(value => value.Site.Equals(pub.Ref));
            Assert.That(pubPlacement.Kind, Is.EqualTo(SitePlacementKind.Settlement));
            Assert.That(pubPlacement.Settlement, Is.EqualTo(town.Ref));

            WorldSitePlacementPlan destinationPlacement = graph.HierarchyPlan.SitePlacements
                .Single(value => value.Site.Equals(destination.Ref));
            Assert.That(destinationPlacement.Kind, Is.EqualTo(SitePlacementKind.Region));
            Assert.That(destinationPlacement.Region, Is.EqualTo(region.Ref));
        }

        [Test]
        public void TypedSettlementRejectsRouteFromAnotherRegionBeforeBuild()
        {
            var game = Campaign.Create("typed-invalid-hierarchy-test");

            RegionHandle north = game.World.Region("north");
            RouteHandle road = north.Road("north-road");
            RegionHandle south = game.World.Region("south");

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
                south.Town(
                    "south-town",
                    town => town.ConnectTo(road, new DistanceRangeMetres(0, 50))));

            Assert.That(error.Message, Does.Contain("north-road"));
            Assert.That(error.Message, Does.Contain("north"));
            Assert.That(error.Message, Does.Contain("south"));
        }

        [Test]
        public void TypedAuthoringSurfaceDoesNotExposeOwnershipMutationOrRawReferenceRelations()
        {
            Assert.That(typeof(RegionHandle).GetConstructors(), Is.Empty);
            Assert.That(typeof(RouteHandle).GetConstructors(), Is.Empty);
            Assert.That(typeof(SettlementHandle).GetConstructors(), Is.Empty);
            Assert.That(typeof(SiteHandle).GetConstructors(), Is.Empty);
            Assert.That(typeof(NpcHandle).GetConstructors(), Is.Empty);

            Assert.That(typeof(RouteAuthoringBuilder).GetMethod("InRegion"), Is.Null);
            Assert.That(typeof(SettlementAuthoringBuilder).GetMethod("InRegion"), Is.Null);
            Assert.That(typeof(NpcAuthoringBuilder).GetMethod("PlaceAt"), Is.Null);
            Assert.That(typeof(CutsceneAuthoringBuilder).GetMethod("At"), Is.Null);

            Assert.That(
                typeof(SiteAuthoringBuilder)
                    .GetMethod("DifferentSiteFrom")
                    .GetParameters()
                    .Single()
                    .ParameterType,
                Is.EqualTo(typeof(SiteHandle)));

            Type[] cutsceneBindTargets = typeof(CutsceneAuthoringBuilder)
                .GetMethods()
                .Where(method => method.Name == "Bind")
                .Select(method => method.GetParameters()[1].ParameterType)
                .ToArray();
            Assert.That(cutsceneBindTargets, Has.Member(typeof(NpcHandle)));
            Assert.That(cutsceneBindTargets, Has.Member(typeof(PlayerSlot)));
            Assert.That(cutsceneBindTargets, Has.No.Member(typeof(CutsceneActorTargetSpec)));
        }

        [Test]
        public void SettlementCannotConnectToRouteFromAnotherRegion()
        {
            var game = Campaign.Create("invalid-hierarchy-test");

            RegionRef north = game.World.RequireRegion("north", _ => { });
            RegionRef south = game.World.RequireRegion("south", _ => { });
            RouteRef road = game.World.RequireRoute("north-road", value => value
                .InRegion(north)
                .Kind(RouteKind.Road));

            game.World.RequireSettlement("south-town", value => value
                .InRegion(south)
                .Archetype(SettlementArchetype.Town)
                .ConnectTo(road, new DistanceRangeMetres(0, 50)));

            BlueprintValidationResult validation = BlueprintValidator.Validate(game.Build());

            Assert.That(validation.IsValid, Is.False);
            Assert.That(validation.Diagnostics.Any(d => d.Code == "WB2406"), Is.True);
        }
    }
}
