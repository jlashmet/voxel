using System;
using System.Collections.Generic;
using System.Linq;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class SiteRoleResolverCompiledHierarchyTests
    {
        [Test]
        public void CompiledHierarchyPlanControlsSiteOwnershipFiltering()
        {
            var game = Campaign.Create("compiled-hierarchy-owner");
            RegionRef authoredRegion = game.World.RequireRegion("authored-region", null);
            SiteRef role = game.World.RequireSite("role", authoredRegion, null);
            PlanningGraph compiled = BlueprintCompiler.Compile(game.Build());

            var compiledRegion = new RegionRef("compiled-region");
            var hierarchyPlan = new WorldHierarchyPlan(
                null,
                null,
                null,
                null,
                new[] { new WorldSitePlacementPlan(role, compiledRegion) });
            var graph = new PlanningGraph(
                compiled.Nodes.ToArray(),
                compiled.SiteRoles.ToArray(),
                compiled.Hierarchy,
                compiled.SpatialConstraints.ToArray(),
                compiled.NpcPlacements.ToArray(),
                compiled.CutsceneStages.ToArray(),
                compiled.SecretCandidates.ToArray(),
                compiled.RequiredSecrets.ToArray(),
                hierarchyPlan);

            var facts = new FakeFacts(
                    Candidate("a-authored-owner"),
                    Candidate("z-compiled-owner"))
                .InRegion("a-authored-owner", authoredRegion)
                .InRegion("z-compiled-owner", compiledRegion);

            SiteResolutionResult result = SiteRoleResolver.Resolve(graph, facts);

            Assert.That(result.IsResolved, Is.True);
            Assert.That(
                result.Bindings.Single(binding => binding.Role.Equals(role)).Site.Value,
                Is.EqualTo("z-compiled-owner"),
                "The solver must consume PlanningGraph.HierarchyPlan instead of re-reading raw hierarchy authoring.");
        }

        private static SiteCandidate Candidate(string id) =>
            new SiteCandidate(
                new ResolvedSiteId(id),
                SiteArchetype.Unspecified,
                Array.Empty<SiteCapabilityOffer>());

        private sealed class FakeFacts : ISiteCandidateFacts
        {
            private readonly SiteCandidate[] _candidates;
            private readonly HashSet<string> _regions = new HashSet<string>(StringComparer.Ordinal);

            public IReadOnlyList<SiteCandidate> Candidates => _candidates;

            public FakeFacts(params SiteCandidate[] candidates) =>
                _candidates = candidates ?? Array.Empty<SiteCandidate>();

            public FakeFacts InRegion(string candidate, RegionRef region)
            {
                _regions.Add(candidate + "@" + region.Id);
                return this;
            }

            public bool IsInRegion(ResolvedSiteId candidate, RegionRef region) =>
                _regions.Contains(candidate.Value + "@" + region.Id);

            public bool IsInSettlement(ResolvedSiteId candidate, SettlementRef settlement) => false;

            public bool IsReachable(
                ResolvedSiteId subject,
                ResolvedSiteId target,
                TraversalProfile traversal) => throw new NotSupportedException();

            public int BoundaryDistanceMetres(
                ResolvedSiteId subject,
                ResolvedSiteId target) => throw new NotSupportedException();

            public int PublicEntranceDistanceMetres(
                ResolvedSiteId subject,
                ResolvedSiteId target) => throw new NotSupportedException();

            public int TraversalDistanceMetres(
                ResolvedSiteId subject,
                ResolvedSiteId target,
                TraversalProfile traversal) => throw new NotSupportedException();
        }
    }
}
