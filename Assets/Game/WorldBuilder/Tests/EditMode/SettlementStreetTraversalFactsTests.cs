using System.Collections.Generic;
using Game.Composition.WorldBuilderWorldGen;
using Game.WorldBuilder.Api;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class SettlementStreetTraversalFactsTests
    {
        [Test]
        public void FindsShortestPathAcrossAuthoredStreetIntersections()
        {
            SettlementPlan plan = KentridgeDefinition.Build(123u);
            var projections = new ProjectionProvider()
                .Add(KentridgeRole.Pub, new Int2(KentridgeTownPlanner.MainSpineXDm, 760))
                .Add(KentridgeRole.MagicShop, new Int2(1050, KentridgeTownPlanner.MarketStreetZDm));
            var traversal = new SettlementStreetTraversalFacts(plan, projections);

            Assert.That(traversal.IsReachable(
                (int)KentridgeRole.Pub,
                (int)KentridgeRole.MagicShop,
                TraversalProfile.NormalParty), Is.True);
            Assert.That(traversal.TraversalDistanceMetres(
                (int)KentridgeRole.Pub,
                (int)KentridgeRole.MagicShop,
                TraversalProfile.NormalParty), Is.EqualTo(36));
        }

        [Test]
        public void IncludesResolvedEntranceToNetworkConnectorDistance()
        {
            SettlementPlan plan = KentridgeDefinition.Build(456u);
            var projections = new ProjectionProvider()
                .Add(KentridgeRole.Pub, new Int2(1190, 760))
                .Add(KentridgeRole.Inn, new Int2(1140, 340));
            var traversal = new SettlementStreetTraversalFacts(plan, projections);

            Assert.That(traversal.TraversalDistanceMetres(
                (int)KentridgeRole.Pub,
                (int)KentridgeRole.Inn,
                TraversalProfile.NormalParty), Is.EqualTo(47));
        }

        [Test]
        public void MissingSiteAccessFailsClosed()
        {
            var streets = new List<PlannedStreet>
            {
                new PlannedStreet(
                    "road",
                    StreetKind.MainRoad,
                    40,
                    new Int2(0, 0),
                    new Int2(100, 0))
            };
            var plots = new List<BuildingPlot>
            {
                new BuildingPlot(1, StructureArchetype.Inn, DistrictKind.Market,
                    new Int2(0, 20), FrontageDirection.South),
                new BuildingPlot(2, StructureArchetype.Shop, DistrictKind.Market,
                    new Int2(80, 20), FrontageDirection.South)
            };
            var plan = new SettlementPlan(
                "missing-access",
                1u,
                new Int2(50, 0),
                Theme("missing-access"),
                streets,
                new PlannedPlaza("plaza", new Int2(50, 50), new Int2(20, 20)),
                plots);
            var projections = new ProjectionProvider()
                .Add(1, new Int2(0, 20))
                .Add(2, new Int2(80, 20));
            var traversal = new SettlementStreetTraversalFacts(plan, projections);

            Assert.That(traversal.IsReachable(1, 2, TraversalProfile.NormalParty), Is.False);
            Assert.That(
                traversal.TraversalDistanceMetres(1, 2, TraversalProfile.NormalParty),
                Is.EqualTo(int.MaxValue));
        }

        private static ArchitectureTheme Theme(string id) =>
            new ArchitectureTheme(
                id,
                MaterialRole.FoundationStone,
                MaterialRole.Masonry,
                MaterialRole.Timber,
                MaterialRole.Glass,
                MaterialRole.RoofTile,
                MaterialRole.DarkMasonry,
                1,
                1,
                1,
                1,
                1,
                1,
                1,
                1,
                1,
                1,
                1);

        private sealed class ProjectionProvider : ISettlementSiteProjectionProvider
        {
            private readonly Dictionary<int, SettlementSiteProjection> _projections =
                new Dictionary<int, SettlementSiteProjection>();

            public ProjectionProvider Add(KentridgeRole role, Int2 entranceDm) =>
                Add((int)role, entranceDm);

            public ProjectionProvider Add(int roleId, Int2 entranceDm)
            {
                _projections[roleId] = new SettlementSiteProjection(
                    SiteArchetype.Unspecified,
                    new SiteFootprintBoundsDm(
                        entranceDm.X - 1,
                        entranceDm.Y - 1,
                        entranceDm.X + 1,
                        entranceDm.Y + 1),
                    entranceDm,
                    new SiteCapabilityOffer(SiteCapabilityKind.PublicExit));
                return this;
            }

            public bool TryProject(
                PlannedSite site,
                out SettlementSiteProjection projection) =>
                _projections.TryGetValue(site.RoleId, out projection);
        }
    }
}
