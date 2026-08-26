using System;
using System.Collections.Generic;

namespace MountingForce.WorldGen.Architecture
{
    /// <summary>
    /// Resolves one aggregated hidden-space request per stable Kentridge site role. Duplicate role
    /// requests are rejected so independent callers cannot accidentally emit overlapping cavities.
    /// </summary>
    public static class KentridgeHiddenSpaceBatchPlanner
    {
        public static IReadOnlyList<KentridgeHiddenSpaceGeometry> Resolve(
            SettlementPlan plan,
            IReadOnlyList<SiteHiddenSpaceRequest> requests)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (requests == null) throw new ArgumentNullException(nameof(requests));

            var plots = new Dictionary<int, BuildingPlot>();
            for (var i = 0; i < plan.Plots.Count; i++)
            {
                BuildingPlot plot = plan.Plots[i];
                if (!plots.TryAdd(plot.RoleId, plot))
                    throw new InvalidOperationException(
                        "Settlement plan contains duplicate structure role id '" + plot.RoleId + "'.");
            }

            var seenRoles = new HashSet<int>();
            var result = new List<KentridgeHiddenSpaceGeometry>();
            for (var i = 0; i < requests.Count; i++)
            {
                SiteHiddenSpaceRequest request = requests[i]
                    ?? throw new InvalidOperationException(
                        "Hidden-space request collection contains null at index " + i + ".");
                if (!seenRoles.Add(request.RoleId))
                    throw new InvalidOperationException(
                        "Hidden-space requests must be aggregated before architecture generation; role '" +
                        request.RoleId + "' appears more than once.");

                BuildingPlot plot;
                if (!plots.TryGetValue(request.RoleId, out plot))
                    throw new InvalidOperationException(
                        "Hidden-space request '" + request.RequestId + "' targets unknown site role '" +
                        request.RoleId + "'.");

                IReadOnlyList<KentridgeHiddenSpaceGeometry> resolved =
                    KentridgeHiddenSpacePlanner.Resolve(plot, plan.Seed, request);
                if (resolved.Count < request.MinimumCount)
                    throw new InvalidOperationException(
                        "Hidden-space request '" + request.RequestId + "' requires at least " +
                        request.MinimumCount + " physical candidate(s) at role '" + request.RoleId +
                        "', but architecture can realize only " + resolved.Count + ".");

                for (var j = 0; j < resolved.Count; j++)
                    result.Add(resolved[j]);
            }

            return result;
        }
    }
}
