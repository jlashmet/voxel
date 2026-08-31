using System.Collections.Generic;
using Game.Composition.WorldBuilderWorldGen;
using Game.WorldBuilder.Api;
using MountingForce.WorldGen;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class SettlementPlanSiteCandidateFactsTests
    {
        [Test]
        public void ExposesOnlyHonestlyProjectedSitesWithStableOwnership()
        {
            SettlementPlan plan = Plan(
                "test-town",
                new BuildingPlot(10, StructureArchetype.Inn, DistrictKind.Market,
                    new Int2(0, 0), FrontageDirection.South),
                new BuildingPlot(20, StructureArchetype.Shop, DistrictKind.Market,
                    new Int2(300, 0), FrontageDirection.South));

            var projections = new FakeProjectionProvider();
            projections.Add(
                10,
                Projection(
                    SiteArchetype.Unspecified,
                    0, 0, 100, 100,
                    30, 0,
                    new SiteCapabilityOffer(SiteCapabilityKind.Interior),
                    new SiteCapabilityOffer(SiteCapabilityKind.PlayerSpawn, 4)));

            var facts = new SettlementPlanSiteCandidateFacts(
                plan,
                new RegionRef("region-a"),
                new SettlementRef("settlement-a"),
                projections,
                new FakeTraversalFacts());

            Assert.That(facts.Candidates.Count, Is.EqualTo(1));
            SiteCandidate candidate = facts.Candidates[0];
            Assert.That(candidate.Id, Is.EqualTo(
                SettlementPlanSiteCandidateFacts.CandidateId("test-town", 10)));
            Assert.That(candidate.Archetype, Is.EqualTo(SiteArchetype.Unspecified));
            Assert.That(candidate.Capabilities.Count, Is.EqualTo(2));
            Assert.That(facts.IsInRegion(candidate.Id, new RegionRef("region-a")), Is.True);
            Assert.That(facts.IsInRegion(candidate.Id, new RegionRef("region-b")), Is.False);
            Assert.That(facts.IsInSettlement(candidate.Id, new SettlementRef("settlement-a")), Is.True);
            Assert.That(facts.IsInSettlement(candidate.Id, new SettlementRef("settlement-b")), Is.False);
        }

        [Test]
        public void UsesExplicitFootprintAndEntranceGeometryWithoutFrontageInference()
        {
            SettlementPlan plan = Plan(
                "distance-town",
                new BuildingPlot(1, StructureArchetype.Inn, DistrictKind.Market,
                    new Int2(0, 0), FrontageDirection.North),
                new BuildingPlot(2, StructureArchetype.Shop, DistrictKind.Market,
                    new Int2(300, 0), FrontageDirection.West));

            var projections = new FakeProjectionProvider();
            projections.Add(1, Projection(
                SiteArchetype.Pub, 0, 0, 100, 100, 20, 0,
                new SiteCapabilityOffer(SiteCapabilityKind.Interior)));
            projections.Add(2, Projection(
                SiteArchetype.Fort, 300, 0, 400, 100, 380, 0,
                new SiteCapabilityOffer(SiteCapabilityKind.Interior)));

            var facts = new SettlementPlanSiteCandidateFacts(
                plan,
                new RegionRef("region"),
                new SettlementRef("settlement"),
                projections,
                new FakeTraversalFacts());

            ResolvedSiteId a = SettlementPlanSiteCandidateFacts.CandidateId("distance-town", 1);
            ResolvedSiteId b = SettlementPlanSiteCandidateFacts.CandidateId("distance-town", 2);

            Assert.That(facts.BoundaryDistanceMetres(a, b), Is.EqualTo(20));
            Assert.That(facts.PublicEntranceDistanceMetres(a, b), Is.EqualTo(36));
        }

        [Test]
        public void DelegatesTraversalTruthInsteadOfInventingItFromEuclideanDistance()
        {
            SettlementPlan plan = Plan(
                "traversal-town",
                new BuildingPlot(11, StructureArchetype.Inn, DistrictKind.Market,
                    new Int2(0, 0), FrontageDirection.South),
                new BuildingPlot(22, StructureArchetype.Shop, DistrictKind.Market,
                    new Int2(50, 0), FrontageDirection.South));

            var projections = new FakeProjectionProvider();
            projections.Add(11, Projection(
                SiteArchetype.Pub, 0, 0, 100, 100, 50, 0,
                new SiteCapabilityOffer(SiteCapabilityKind.Interior)));
            projections.Add(22, Projection(
                SiteArchetype.Fort, 50, 0, 150, 100, 100, 0,
                new SiteCapabilityOffer(SiteCapabilityKind.Interior)));

            var traversal = new FakeTraversalFacts
            {
                Reachable = false,
                DistanceMetres = 73
            };
            var facts = new SettlementPlanSiteCandidateFacts(
                plan,
                new RegionRef("region"),
                new SettlementRef("settlement"),
                projections,
                traversal);

            ResolvedSiteId a = SettlementPlanSiteCandidateFacts.CandidateId("traversal-town", 11);
            ResolvedSiteId b = SettlementPlanSiteCandidateFacts.CandidateId("traversal-town", 22);

            Assert.That(facts.IsReachable(a, b, TraversalProfile.NormalParty), Is.False);
            Assert.That(
                facts.TraversalDistanceMetres(a, b, TraversalProfile.NormalParty),
                Is.EqualTo(73));
            Assert.That(traversal.LastSubjectRoleId, Is.EqualTo(11));
            Assert.That(traversal.LastTargetRoleId, Is.EqualTo(22));
            Assert.That(traversal.ReachabilityCalls, Is.EqualTo(1));
            Assert.That(traversal.DistanceCalls, Is.EqualTo(1));
        }

        private static SettlementSiteProjection Projection(
            SiteArchetype archetype,
            int minX,
            int minZ,
            int maxX,
            int maxZ,
            int entranceX,
            int entranceZ,
            params SiteCapabilityOffer[] capabilities) =>
            new SettlementSiteProjection(
                archetype,
                new SiteFootprintBoundsDm(minX, minZ, maxX, maxZ),
                new Int2(entranceX, entranceZ),
                capabilities);

        private static SettlementPlan Plan(string id, params BuildingPlot[] plots) =>
            new SettlementPlan(
                id,
                123u,
                new Int2(0, 0),
                Theme(id),
                new List<PlannedStreet>(),
                new PlannedPlaza("plaza", new Int2(0, 0), new Int2(10, 10)),
                new List<BuildingPlot>(plots));

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

        private sealed class FakeProjectionProvider : ISettlementSiteProjectionProvider
        {
            private readonly Dictionary<int, SettlementSiteProjection> _projections =
                new Dictionary<int, SettlementSiteProjection>();

            public void Add(int roleId, SettlementSiteProjection projection) =>
                _projections.Add(roleId, projection);

            public bool TryProject(
                PlannedSite site,
                out SettlementSiteProjection projection) =>
                _projections.TryGetValue(site.RoleId, out projection);
        }

        private sealed class FakeTraversalFacts : ISettlementTraversalFacts
        {
            public bool Reachable { get; set; } = true;
            public int DistanceMetres { get; set; }
            public int LastSubjectRoleId { get; private set; }
            public int LastTargetRoleId { get; private set; }
            public int ReachabilityCalls { get; private set; }
            public int DistanceCalls { get; private set; }

            public bool IsReachable(
                int subjectRoleId,
                int targetRoleId,
                TraversalProfile traversal)
            {
                LastSubjectRoleId = subjectRoleId;
                LastTargetRoleId = targetRoleId;
                ReachabilityCalls++;
                return Reachable;
            }

            public int TraversalDistanceMetres(
                int subjectRoleId,
                int targetRoleId,
                TraversalProfile traversal)
            {
                LastSubjectRoleId = subjectRoleId;
                LastTargetRoleId = targetRoleId;
                DistanceCalls++;
                return DistanceMetres;
            }
        }
    }
}
