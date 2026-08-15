using System;
using System.Collections.Generic;
using System.Linq;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class SiteRoleResolverTests
    {
        [Test]
        public void ResolvesConstraintMatchedDestinationWithoutArchetype()
        {
            var game = Campaign.Create("resolve-destination");
            RegionRef region = game.World.RequireRegion("region", value => value.Biome(BiomeFamily.TemperateForest));
            SiteRef pub = game.World.RequireSite("pub", region, site => site
                .Archetype(SiteArchetype.Pub)
                .RequireCapability(SiteCapability.Interior));
            SiteRef destination = game.World.RequireSite("destination", region, site => site
                .DifferentSiteFrom(pub)
                .ReachableFrom(pub, TraversalProfile.NormalParty));
            game.World.RequireNpc("guide", npc => npc.PlaceAt(destination).RequireConversation());

            PlanningGraph graph = BlueprintCompiler.Compile(game.Build());
            var facts = new FakeFacts(
                    Candidate("pub-generated", SiteArchetype.Pub, new SiteCapabilityOffer(SiteCapabilityKind.Interior)),
                    Candidate("destination-generated", SiteArchetype.Unspecified, new SiteCapabilityOffer(SiteCapabilityKind.ConversationSpace)))
                .InRegion("pub-generated", region)
                .InRegion("destination-generated", region)
                .Reachable("destination-generated", "pub-generated", TraversalProfile.NormalParty);

            SiteResolutionResult result = SiteRoleResolver.Resolve(graph, facts);

            Assert.That(result.IsResolved, Is.True);
            Assert.That(result.Bindings.Single(binding => binding.Role.Equals(pub)).Site.Value, Is.EqualTo("pub-generated"));
            Assert.That(result.Bindings.Single(binding => binding.Role.Equals(destination)).Site.Value, Is.EqualTo("destination-generated"));
        }

        [Test]
        public void UnclassifiedCandidateDoesNotSatisfyRequiredArchetype()
        {
            var game = Campaign.Create("required-archetype");
            SiteRef pub = game.World.RequireSite("pub", site => site.Archetype(SiteArchetype.Pub));
            PlanningGraph graph = BlueprintCompiler.Compile(game.Build());

            var facts = new FakeFacts(Candidate("unclassified", SiteArchetype.Unspecified));

            SiteResolutionResult result = SiteRoleResolver.Resolve(graph, facts);

            Assert.That(result.IsResolved, Is.False);
            Assert.That(result.Diagnostics.Single().Kind, Is.EqualTo(SiteResolutionDiagnosticKind.ArchetypeUnsatisfied));
            Assert.That(result.Diagnostics.Single().Role, Is.EqualTo(pub));
        }

        [Test]
        public void RejectsCandidateMissingDerivedConversationCapacity()
        {
            var game = Campaign.Create("missing-conversation");
            RegionRef region = game.World.RequireRegion("region", null);
            SiteRef destination = game.World.RequireSite("destination", region, null);
            game.World.RequireNpc("guide", npc => npc.PlaceAt(destination).RequireConversation());

            PlanningGraph graph = BlueprintCompiler.Compile(game.Build());
            var facts = new FakeFacts(Candidate("ruin", SiteArchetype.Ruin))
                .InRegion("ruin", region);

            SiteResolutionResult result = SiteRoleResolver.Resolve(graph, facts);

            Assert.That(result.IsResolved, Is.False);
            Assert.That(result.Diagnostics.Single().Kind, Is.EqualTo(SiteResolutionDiagnosticKind.CapabilityUnsatisfied));
            Assert.That(result.Diagnostics.Single().Role, Is.EqualTo(destination));
        }

        [Test]
        public void DifferentSiteConstraintPreventsRoleAliasing()
        {
            var game = Campaign.Create("different-sites");
            RegionRef region = game.World.RequireRegion("region", null);
            SiteRef first = game.World.RequireSite("first", region, null);
            game.World.RequireSite("second", region, site => site.DifferentSiteFrom(first));

            PlanningGraph graph = BlueprintCompiler.Compile(game.Build());
            var facts = new FakeFacts(Candidate("shared", SiteArchetype.Camp))
                .InRegion("shared", region);

            SiteResolutionResult result = SiteRoleResolver.Resolve(graph, facts);

            Assert.That(result.IsResolved, Is.False);
            Assert.That(result.Diagnostics.Single().Kind, Is.EqualTo(SiteResolutionDiagnosticKind.DifferentSiteUnsatisfied));
        }

        [Test]
        public void RolesMayAliasWhenNoDifferentSiteConstraintExists()
        {
            var game = Campaign.Create("alias-allowed");
            RegionRef region = game.World.RequireRegion("region", null);
            SiteRef first = game.World.RequireSite("first", region, null);
            SiteRef second = game.World.RequireSite("second", region, null);

            PlanningGraph graph = BlueprintCompiler.Compile(game.Build());
            var facts = new FakeFacts(Candidate("shared", SiteArchetype.Camp))
                .InRegion("shared", region);

            SiteResolutionResult result = SiteRoleResolver.Resolve(graph, facts);

            Assert.That(result.IsResolved, Is.True);
            Assert.That(result.Bindings.Single(binding => binding.Role.Equals(first)).Site.Value, Is.EqualTo("shared"));
            Assert.That(result.Bindings.Single(binding => binding.Role.Equals(second)).Site.Value, Is.EqualTo("shared"));
        }

        [Test]
        public void ReportsReachabilityUnsatisfied()
        {
            var game = Campaign.Create("unreachable");
            RegionRef region = game.World.RequireRegion("region", null);
            SiteRef pub = game.World.RequireSite("pub", region, site => site.Archetype(SiteArchetype.Pub));
            game.World.RequireSite("destination", region, site => site
                .Archetype(SiteArchetype.Ruin)
                .ReachableFrom(pub, TraversalProfile.NormalParty));

            PlanningGraph graph = BlueprintCompiler.Compile(game.Build());
            var facts = new FakeFacts(
                    Candidate("pub-generated", SiteArchetype.Pub),
                    Candidate("ruin-generated", SiteArchetype.Ruin))
                .InRegion("pub-generated", region)
                .InRegion("ruin-generated", region);

            SiteResolutionResult result = SiteRoleResolver.Resolve(graph, facts);

            Assert.That(result.IsResolved, Is.False);
            Assert.That(result.Diagnostics.Single().Kind, Is.EqualTo(SiteResolutionDiagnosticKind.ReachabilityUnsatisfied));
        }

        [Test]
        public void UsesExactDistanceMetric()
        {
            var game = Campaign.Create("distance-metric");
            RegionRef region = game.World.RequireRegion("region", null);
            SiteRef first = game.World.RequireSite("first", region, site => site.Archetype(SiteArchetype.Pub));
            game.World.RequireSite("second", region, site => site
                .Archetype(SiteArchetype.Ruin)
                .EntranceDistanceFrom(first, new DistanceRangeMetres(10, 20)));

            PlanningGraph graph = BlueprintCompiler.Compile(game.Build());
            var facts = new FakeFacts(
                    Candidate("pub-generated", SiteArchetype.Pub),
                    Candidate("ruin-generated", SiteArchetype.Ruin))
                .InRegion("pub-generated", region)
                .InRegion("ruin-generated", region)
                .BoundaryDistance("ruin-generated", "pub-generated", 999)
                .EntranceDistance("ruin-generated", "pub-generated", 15)
                .TraversalDistance("ruin-generated", "pub-generated", TraversalProfile.NormalParty, 777);

            SiteResolutionResult result = SiteRoleResolver.Resolve(graph, facts);

            Assert.That(result.IsResolved, Is.True,
                "The entrance-distance constraint must use public-entrance distance, not boundary or traversal distance.");
        }

        [Test]
        public void ResolutionIsDeterministicByStableCandidateId()
        {
            var game = Campaign.Create("deterministic");
            SiteRef role = game.World.RequireSite("role", null);
            PlanningGraph graph = BlueprintCompiler.Compile(game.Build());

            var facts = new FakeFacts(
                Candidate("z-site", SiteArchetype.Ruin),
                Candidate("a-site", SiteArchetype.Camp));

            SiteResolutionResult first = SiteRoleResolver.Resolve(graph, facts);
            SiteResolutionResult second = SiteRoleResolver.Resolve(graph, facts);

            Assert.That(first.IsResolved, Is.True);
            Assert.That(first.Bindings.Single(binding => binding.Role.Equals(role)).Site.Value, Is.EqualTo("a-site"));
            Assert.That(second.Bindings.Single(binding => binding.Role.Equals(role)).Site.Value, Is.EqualTo("a-site"));
        }

        [Test]
        public void HierarchyOwnerFiltersCandidatesBeforeRelationalSolving()
        {
            var game = Campaign.Create("hierarchy-owner");
            RegionRef region = game.World.RequireRegion("target-region", null);
            SiteRef role = game.World.RequireSite("role", region, null);
            PlanningGraph graph = BlueprintCompiler.Compile(game.Build());

            var facts = new FakeFacts(
                    Candidate("a-wrong-region", SiteArchetype.Camp),
                    Candidate("z-right-region", SiteArchetype.Camp))
                .InRegion("z-right-region", region);

            SiteResolutionResult result = SiteRoleResolver.Resolve(graph, facts);

            Assert.That(result.IsResolved, Is.True);
            Assert.That(result.Bindings.Single(binding => binding.Role.Equals(role)).Site.Value, Is.EqualTo("z-right-region"));
        }

        private static SiteCandidate Candidate(
            string id,
            SiteArchetype archetype,
            params SiteCapabilityOffer[] capabilities) =>
            new SiteCandidate(new ResolvedSiteId(id), archetype, capabilities);

        private sealed class FakeFacts : ISiteCandidateFacts
        {
            private readonly SiteCandidate[] _candidates;
            private readonly HashSet<string> _regions = new HashSet<string>(StringComparer.Ordinal);
            private readonly HashSet<string> _settlements = new HashSet<string>(StringComparer.Ordinal);
            private readonly HashSet<string> _reachable = new HashSet<string>(StringComparer.Ordinal);
            private readonly Dictionary<string, int> _boundaryDistances = new Dictionary<string, int>(StringComparer.Ordinal);
            private readonly Dictionary<string, int> _entranceDistances = new Dictionary<string, int>(StringComparer.Ordinal);
            private readonly Dictionary<string, int> _traversalDistances = new Dictionary<string, int>(StringComparer.Ordinal);

            public IReadOnlyList<SiteCandidate> Candidates => _candidates;

            public FakeFacts(params SiteCandidate[] candidates) =>
                _candidates = candidates ?? Array.Empty<SiteCandidate>();

            public FakeFacts InRegion(string candidate, RegionRef region)
            {
                _regions.Add(candidate + "@" + region.Id);
                return this;
            }

            public FakeFacts InSettlement(string candidate, SettlementRef settlement)
            {
                _settlements.Add(candidate + "@" + settlement.Id);
                return this;
            }

            public FakeFacts Reachable(string subject, string target, TraversalProfile traversal)
            {
                _reachable.Add(RelationKey(subject, target, traversal));
                return this;
            }

            public FakeFacts BoundaryDistance(string subject, string target, int metres)
            {
                _boundaryDistances[PairKey(subject, target)] = metres;
                return this;
            }

            public FakeFacts EntranceDistance(string subject, string target, int metres)
            {
                _entranceDistances[PairKey(subject, target)] = metres;
                return this;
            }

            public FakeFacts TraversalDistance(
                string subject,
                string target,
                TraversalProfile traversal,
                int metres)
            {
                _traversalDistances[RelationKey(subject, target, traversal)] = metres;
                return this;
            }

            public bool IsInRegion(ResolvedSiteId candidate, RegionRef region) =>
                _regions.Contains(candidate.Value + "@" + region.Id);

            public bool IsInSettlement(ResolvedSiteId candidate, SettlementRef settlement) =>
                _settlements.Contains(candidate.Value + "@" + settlement.Id);

            public bool IsReachable(
                ResolvedSiteId subject,
                ResolvedSiteId target,
                TraversalProfile traversal) =>
                _reachable.Contains(RelationKey(subject.Value, target.Value, traversal));

            public int BoundaryDistanceMetres(ResolvedSiteId subject, ResolvedSiteId target) =>
                Distance(_boundaryDistances, PairKey(subject.Value, target.Value));

            public int PublicEntranceDistanceMetres(ResolvedSiteId subject, ResolvedSiteId target) =>
                Distance(_entranceDistances, PairKey(subject.Value, target.Value));

            public int TraversalDistanceMetres(
                ResolvedSiteId subject,
                ResolvedSiteId target,
                TraversalProfile traversal) =>
                Distance(_traversalDistances, RelationKey(subject.Value, target.Value, traversal));

            private static int Distance(Dictionary<string, int> values, string key)
            {
                int value;
                return values.TryGetValue(key, out value) ? value : int.MaxValue;
            }

            private static string PairKey(string subject, string target) => subject + "->" + target;

            private static string RelationKey(
                string subject,
                string target,
                TraversalProfile traversal) =>
                PairKey(subject, target) + ":" + traversal;
        }
    }
}
