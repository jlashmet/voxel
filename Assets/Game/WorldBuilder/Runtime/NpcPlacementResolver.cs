using System;
using System.Collections.Generic;
using Game.WorldBuilder.Api;

namespace Game.WorldBuilder.Runtime
{
    /// <summary>
    /// Binds compiled NPC placement intent to the concrete generated sites selected by SiteRoleResolver.
    /// Physical positions remain the responsibility of the selected site's realization adapter.
    /// </summary>
    public static class NpcPlacementResolver
    {
        public static IReadOnlyList<NpcSiteAssignment> ResolveSites(
            PlanningGraph graph,
            SiteResolutionResult sites)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (sites == null) throw new ArgumentNullException(nameof(sites));
            if (!sites.IsResolved)
                throw new InvalidOperationException(
                    "NPC placement cannot resolve before authored site roles resolve successfully.");

            var sitesByRole = new Dictionary<SiteRef, ResolvedSiteId>();
            for (var i = 0; i < sites.Bindings.Count; i++)
            {
                SiteRoleBinding binding = sites.Bindings[i]
                    ?? throw new InvalidOperationException(
                        "Site resolution contains a null binding at index " + i + ".");
                if (sitesByRole.ContainsKey(binding.Role))
                    throw new InvalidOperationException(
                        "Site resolution binds role '" + binding.Role + "' more than once.");
                sitesByRole.Add(binding.Role, binding.Site);
            }

            var plans = new NpcPlacementPlan[graph.NpcPlacements.Count];
            for (var i = 0; i < plans.Length; i++)
                plans[i] = graph.NpcPlacements[i];
            Array.Sort(plans, (left, right) =>
                StringComparer.Ordinal.Compare(left.Npc.Id, right.Npc.Id));

            var seenNpcs = new HashSet<NpcRef>();
            var result = new List<NpcSiteAssignment>(plans.Length);
            for (var i = 0; i < plans.Length; i++)
            {
                NpcPlacementPlan plan = plans[i]
                    ?? throw new InvalidOperationException(
                        "Planning graph contains a null NPC placement at index " + i + ".");
                if (!seenNpcs.Add(plan.Npc))
                    throw new InvalidOperationException(
                        "Planning graph contains duplicate NPC placement for '" + plan.Npc + "'.");

                ResolvedSiteId resolvedSite;
                if (!sitesByRole.TryGetValue(plan.Site, out resolvedSite))
                    throw new InvalidOperationException(
                        "NPC '" + plan.Npc + "' requires site role '" + plan.Site +
                        "', but that role has no resolved generated site binding.");

                result.Add(new NpcSiteAssignment(
                    plan.Npc,
                    plan.Site,
                    resolvedSite,
                    plan.RequiresConversation));
            }

            return result;
        }
    }
}
