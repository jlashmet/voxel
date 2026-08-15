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

            RegionRef region = game.World.RequireRegion("starting-region", value => value
                .Biome(BiomeFamily.TemperateForest));

            RouteRef route = game.World.RequireRoute("main-route", value => value
                .InRegion(region)
                .Kind(RouteKind.TradeRoad)
                .Importance(RouteImportance.Primary));

            SettlementRef town = game.World.RequireSettlement("starting-town", value => value
                .InRegion(region)
                .Archetype(SettlementArchetype.Town)
                .Population(150, 450)
                .ConnectTo(route, new DistanceRangeMetres(0, 80)));

            SiteRef pub = game.World.RequireSite("starting-pub", town, site => site
                .Archetype(SiteArchetype.Pub)
                .RequireCapability(SiteCapability.Interior)
                .RequireCapability(SiteCapability.PublicExit));

            SiteRef destination = game.World.RequireSite("first-destination", region, site => site
                .Archetype(SiteArchetype.Ruin)
                .DifferentSiteFrom(pub)
                .ReachableFrom(pub, TraversalProfile.NormalParty));

            CampaignBlueprint blueprint = game.Build();
            BlueprintValidationResult validation = BlueprintValidator.Validate(blueprint);
            Assert.That(validation.IsValid, Is.True);
            Assert.That(validation.Diagnostics.Any(d => d.Code == "WB1402"), Is.False);

            SettlementRouteAccessSpec access = blueprint.Hierarchy.RouteAccess.Single();
            Assert.That(access.Settlement, Is.EqualTo(town));
            Assert.That(access.Route, Is.EqualTo(route));
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

            WorldRegionPlan regionPlan = graph.HierarchyPlan.Regions.Single(value => value.Region.Equals(region));
            Assert.That(regionPlan.Biome, Is.EqualTo(BiomeFamily.TemperateForest));
            Assert.That(regionPlan.Routes, Is.EquivalentTo(new[] { route }));
            Assert.That(regionPlan.Settlements, Is.EquivalentTo(new[] { town }));
            Assert.That(regionPlan.RegionOwnedSites, Is.EquivalentTo(new[] { destination }));

            WorldRoutePlan routePlan = graph.HierarchyPlan.Routes.Single(value => value.Route.Equals(route));
            Assert.That(routePlan.Region, Is.EqualTo(region));
            Assert.That(routePlan.Kind, Is.EqualTo(RouteKind.TradeRoad));
            Assert.That(routePlan.Importance, Is.EqualTo(RouteImportance.Primary));
            WorldRouteAccessPlan routeAccess = routePlan.SettlementAccess.Single();
            Assert.That(routeAccess.Settlement, Is.EqualTo(town));
            Assert.That(routeAccess.ConnectorLengthMetres.Minimum, Is.EqualTo(0));
            Assert.That(routeAccess.ConnectorLengthMetres.Maximum, Is.EqualTo(80));

            WorldSettlementPlan townPlan = graph.HierarchyPlan.Settlements.Single(value => value.Settlement.Equals(town));
            Assert.That(townPlan.Region, Is.EqualTo(region));
            Assert.That(townPlan.Archetype, Is.EqualTo(SettlementArchetype.Town));
            Assert.That(townPlan.HasPopulationRange, Is.True);
            Assert.That(townPlan.Population.Minimum, Is.EqualTo(150));
            Assert.That(townPlan.Population.Maximum, Is.EqualTo(450));
            Assert.That(townPlan.Sites, Is.EquivalentTo(new[] { pub }));
            Assert.That(townPlan.RouteAccess.Single(), Is.SameAs(routeAccess),
                "Route and settlement plans must share the same compiled access requirement object.");

            WorldSitePlacementPlan pubPlacement = graph.HierarchyPlan.SitePlacements.Single(value => value.Site.Equals(pub));
            Assert.That(pubPlacement.Kind, Is.EqualTo(SitePlacementKind.Settlement));
            Assert.That(pubPlacement.Settlement, Is.EqualTo(town));

            WorldSitePlacementPlan destinationPlacement = graph.HierarchyPlan.SitePlacements.Single(value => value.Site.Equals(destination));
            Assert.That(destinationPlacement.Kind, Is.EqualTo(SitePlacementKind.Region));
            Assert.That(destinationPlacement.Region, Is.EqualTo(region));
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
