using System;
using System.Collections.Generic;
using Game.WorldBuilder.Api;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Architecture;

namespace Game.Composition.WorldBuilderWorldGen
{
    /// <summary>
    /// Post-site-resolution translation from WorldBuilder secret-generation intent into the generic
    /// WorldGen hidden-space contract. Multiple required/policy requests for one physical site are
    /// aggregated before crossing the boundary so architecture receives exactly one request per role.
    /// </summary>
    public static class KentridgeHiddenSpaceRequestComposer
    {
        private sealed class Aggregate
        {
            public int Minimum;
            public int Target;
        }

        public static IReadOnlyList<SiteHiddenSpaceRequest> Compose(
            PlanningGraph graph,
            SiteResolutionResult sites,
            SettlementPlan plan)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (sites == null) throw new ArgumentNullException(nameof(sites));
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (!sites.IsResolved)
                throw new InvalidOperationException(
                    "Hidden-space generation cannot be composed before site roles resolve successfully.");

            var aggregateBySite = new Dictionary<SiteRef, Aggregate>();
            for (var i = 0; i < graph.SecretCandidates.Count; i++)
            {
                SecretCandidatePlan candidate = graph.SecretCandidates[i]
                    ?? throw new InvalidOperationException(
                        "Planning graph contains a null secret-candidate plan at index " + i + ".");
                RequireFalseWall(candidate.AllowedEntrances, candidate.Policy.ToString());
                Aggregate aggregate = GetOrAdd(aggregateBySite, candidate.Site);
                aggregate.Minimum += candidate.MinimumCandidateCount;
                aggregate.Target += candidate.PreferredCandidateCount;
            }

            for (var i = 0; i < graph.RequiredSecrets.Count; i++)
            {
                RequiredSecretCandidatePlan required = graph.RequiredSecrets[i]
                    ?? throw new InvalidOperationException(
                        "Planning graph contains a null required-secret plan at index " + i + ".");
                if (required.Entrance != SecretEntranceType.DestroyableFalseWall)
                    throw new InvalidOperationException(
                        "Required secret '" + required.Secret + "' uses an entrance unsupported by Kentridge hidden-space generation.");
                Aggregate aggregate = GetOrAdd(aggregateBySite, required.Site);
                aggregate.Minimum++;
                aggregate.Target++;
            }

            var resolvedByRole = new Dictionary<SiteRef, ResolvedSiteId>();
            for (var i = 0; i < sites.Bindings.Count; i++)
            {
                SiteRoleBinding binding = sites.Bindings[i]
                    ?? throw new InvalidOperationException(
                        "Site resolution contains a null binding at index " + i + ".");
                if (!resolvedByRole.TryAdd(binding.Role, binding.Site))
                    throw new InvalidOperationException(
                        "Site resolution binds authored role '" + binding.Role + "' more than once.");
            }

            var roleByResolvedSite = new Dictionary<ResolvedSiteId, int>();
            for (var i = 0; i < plan.Sites.Count; i++)
            {
                PlannedSite planned = plan.Sites[i];
                ResolvedSiteId id = SettlementPlanSiteCandidateFacts.CandidateId(plan.Id, planned.RoleId);
                if (!roleByResolvedSite.TryAdd(id, planned.RoleId))
                    throw new InvalidOperationException(
                        "Settlement plan exposes duplicate resolved site id '" + id + "'.");
            }

            var siteRoles = new List<SiteRef>(aggregateBySite.Keys);
            siteRoles.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));
            var result = new List<SiteHiddenSpaceRequest>(siteRoles.Count);
            for (var i = 0; i < siteRoles.Count; i++)
            {
                SiteRef siteRole = siteRoles[i];
                ResolvedSiteId resolved;
                if (!resolvedByRole.TryGetValue(siteRole, out resolved))
                    throw new InvalidOperationException(
                        "Secret generation targets site role '" + siteRole + "', but that role has no resolved site.");

                int roleId;
                if (!roleByResolvedSite.TryGetValue(resolved, out roleId))
                    throw new InvalidOperationException(
                        "Resolved site '" + resolved + "' for role '" + siteRole +
                        "' does not belong to the supplied settlement plan.");

                Aggregate aggregate = aggregateBySite[siteRole];
                result.Add(new SiteHiddenSpaceRequest(
                    "worldbuilder-hidden/" + siteRole.Id,
                    roleId,
                    aggregate.Minimum,
                    aggregate.Target,
                    HiddenSpaceEntranceKind.BreakableMatchingWall));
            }

            return result;
        }

        public static IReadOnlyList<KentridgeHiddenSpaceGeometry> ResolveArchitecture(
            PlanningGraph graph,
            SiteResolutionResult sites,
            SettlementPlan plan) =>
            KentridgeHiddenSpaceBatchPlanner.Resolve(plan, Compose(graph, sites, plan));

        private static Aggregate GetOrAdd(
            Dictionary<SiteRef, Aggregate> values,
            SiteRef site)
        {
            Aggregate aggregate;
            if (!values.TryGetValue(site, out aggregate))
            {
                aggregate = new Aggregate();
                values.Add(site, aggregate);
            }
            return aggregate;
        }

        private static void RequireFalseWall(
            IReadOnlyList<SecretEntranceType> entrances,
            string source)
        {
            for (var i = 0; i < entrances.Count; i++)
                if (entrances[i] == SecretEntranceType.DestroyableFalseWall)
                    return;

            throw new InvalidOperationException(
                "Secret generation source '" + source +
                "' has no entrance supported by Kentridge hidden-space generation.");
        }
    }

    /// <summary>
    /// WorldBuilder-facing candidate provider backed only by physical WorldGen realizations. It never
    /// invents rooms or topology flags: every SecretCandidate property is copied from generator facts.
    /// </summary>
    public sealed class KentridgeHiddenSpaceSecretCandidateProvider : ISecretCandidateProvider
    {
        private readonly Dictionary<SiteRef, IReadOnlyList<SecretCandidate>> _candidates =
            new Dictionary<SiteRef, IReadOnlyList<SecretCandidate>>();

        public KentridgeHiddenSpaceSecretCandidateProvider(
            SettlementPlan plan,
            SiteResolutionResult sites,
            IReadOnlyList<KentridgeHiddenSpaceGeometry> realizations)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (sites == null) throw new ArgumentNullException(nameof(sites));
            if (realizations == null) throw new ArgumentNullException(nameof(realizations));
            if (!sites.IsResolved)
                throw new InvalidOperationException(
                    "Secret candidates cannot be projected before site roles resolve successfully.");

            var rolesByWorldGenRole = new Dictionary<int, List<SiteRef>>();
            for (var i = 0; i < plan.Sites.Count; i++)
            {
                PlannedSite planned = plan.Sites[i];
                ResolvedSiteId resolved = SettlementPlanSiteCandidateFacts.CandidateId(plan.Id, planned.RoleId);
                for (var j = 0; j < sites.Bindings.Count; j++)
                {
                    SiteRoleBinding binding = sites.Bindings[j];
                    if (binding != null && binding.Site.Equals(resolved))
                    {
                        List<SiteRef> roles;
                        if (!rolesByWorldGenRole.TryGetValue(planned.RoleId, out roles))
                        {
                            roles = new List<SiteRef>();
                            rolesByWorldGenRole.Add(planned.RoleId, roles);
                        }
                        roles.Add(binding.Role);
                    }
                }
            }

            var mutable = new Dictionary<SiteRef, List<SecretCandidate>>();
            for (var i = 0; i < realizations.Count; i++)
            {
                KentridgeHiddenSpaceGeometry geometry = realizations[i]
                    ?? throw new InvalidOperationException(
                        "Hidden-space realization collection contains null at index " + i + ".");
                SiteHiddenSpaceRealization realization = geometry.Realization;

                List<SiteRef> siteRoles;
                if (!rolesByWorldGenRole.TryGetValue(realization.RoleId, out siteRoles))
                    continue;

                SecretCandidate candidate = ToCandidate(realization, siteRoles[0]);
                for (var j = 0; j < siteRoles.Count; j++)
                {
                    SiteRef siteRole = siteRoles[j];
                    List<SecretCandidate> list;
                    if (!mutable.TryGetValue(siteRole, out list))
                    {
                        list = new List<SecretCandidate>();
                        mutable.Add(siteRole, list);
                    }

                    // SecretCandidate carries the authored SiteRef, so alias roles receive equivalent
                    // physical candidates with their own semantic site identity.
                    list.Add(j == 0 ? candidate : ToCandidate(realization, siteRole));
                }
            }

            foreach (KeyValuePair<SiteRef, List<SecretCandidate>> pair in mutable)
                _candidates.Add(pair.Key, pair.Value.ToArray());
        }

        public IReadOnlyList<SecretCandidate> GetCandidates(SiteRef site)
        {
            IReadOnlyList<SecretCandidate> candidates;
            return _candidates.TryGetValue(site, out candidates)
                ? candidates
                : Array.Empty<SecretCandidate>();
        }

        private static SecretCandidate ToCandidate(
            SiteHiddenSpaceRealization realization,
            SiteRef site)
        {
            HiddenSpaceEntranceRealization entrance = realization.Entrance;
            if (entrance.Kind != HiddenSpaceEntranceKind.BreakableMatchingWall)
                throw new InvalidOperationException(
                    "Kentridge realized unsupported hidden-space entrance kind '" + entrance.Kind + "'.");

            SecretSpaceKind spaceKind = realization.Kind == HiddenSpaceVolumeKind.SideCavity
                ? SecretSpaceKind.CavityBehindWall
                : SecretSpaceKind.HiddenRoom;
            var projectedEntrance = new SecretEntranceCandidate(
                entrance.Id,
                SecretEntranceType.DestroyableFalseWall,
                entrance.SeparatesHiddenSpaceBeforeOpen,
                entrance.GrantsNormalTraversalAfterOpen,
                entrance.IsStructurallyCritical,
                entrance.SupportsRemoval,
                entrance.MatchesHostSurface);
            return new SecretCandidate(
                new SecretCandidateId(realization.CandidateId),
                site,
                spaceKind,
                realization.HiddenFromNormalTraversal,
                realization.QualityBasisPoints,
                new[] { projectedEntrance });
        }
    }
}
