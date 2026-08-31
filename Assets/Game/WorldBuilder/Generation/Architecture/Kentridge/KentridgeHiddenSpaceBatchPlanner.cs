using System;
using System.Collections.Generic;

namespace MountingForce.WorldGen.Architecture
{
    /// <summary>
    /// Resolves one aggregated hidden-space request per stable Kentridge site role. Duplicate role
    /// requests are rejected so independent callers cannot accidentally emit overlapping cavities.
    /// Reservation-aware callers may supply an immutable shared snapshot; topology remains owned by
    /// KentridgeHiddenSpacePlanner while this batch boundary rejects only spatially conflicting results.
    /// </summary>
    public static class KentridgeHiddenSpaceBatchPlanner
    {
        public static IReadOnlyList<KentridgeHiddenSpaceGeometry> Resolve(
            SettlementPlan plan,
            IReadOnlyList<SiteHiddenSpaceRequest> requests)
        {
            return ResolveInternal(plan, requests, null);
        }

        public static IReadOnlyList<KentridgeHiddenSpaceGeometry> Resolve(
            SettlementPlan plan,
            IReadOnlyList<SiteHiddenSpaceRequest> requests,
            SpatialReservationSnapshot reservations)
        {
            if (reservations == null) throw new ArgumentNullException(nameof(reservations));
            return ResolveInternal(plan, requests, reservations);
        }

        private static IReadOnlyList<KentridgeHiddenSpaceGeometry> ResolveInternal(
            SettlementPlan plan,
            IReadOnlyList<SiteHiddenSpaceRequest> requests,
            SpatialReservationSnapshot reservations)
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
            var acceptedClaims = new List<SpatialReservation>();
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
                int acceptedForRequest = 0;
                for (var j = 0; j < resolved.Count; j++)
                {
                    KentridgeHiddenSpaceGeometry geometry = resolved[j];
                    if (reservations != null)
                    {
                        SpatialReservation claim = WorldBuilderReservationFactory.HiddenSpaceVolume(
                            geometry.Realization,
                            new Int3(plot.PositionDm.X, 0, plot.PositionDm.Y));
                        ReservationQueryResult external = QueryWithoutHostSite(
                            reservations,
                            plot,
                            claim);
                        if (external.Decision == ReservationDecision.Rejected)
                            continue;

                        if (acceptedClaims.Count > 0)
                        {
                            SpatialReservationSnapshot accepted = SpatialReservationSnapshot.Create(
                                acceptedClaims,
                                reservations.Window,
                                reservations.BucketSizeDm);
                            ReservationQueryResult sibling = accepted.Query(
                                claim,
                                ReservationConsumerKind.Underground);
                            if (sibling.Decision == ReservationDecision.Rejected)
                                continue;
                        }

                        acceptedClaims.Add(claim);
                    }

                    result.Add(geometry);
                    acceptedForRequest++;
                }

                if (acceptedForRequest < request.MinimumCount)
                    throw new InvalidOperationException(
                        "Hidden-space request '" + request.RequestId + "' requires at least " +
                        request.MinimumCount + " physical candidate(s) at role '" + request.RoleId +
                        "', but architecture and shared reservations permit only " + acceptedForRequest + ".");
            }

            return result;
        }

        private static ReservationQueryResult QueryWithoutHostSite(
            SpatialReservationSnapshot reservations,
            BuildingPlot plot,
            in SpatialReservation candidate)
        {
            string hostOwner = FindHostOwner(reservations, plot);
            if (hostOwner == null)
                return reservations.Query(candidate, ReservationConsumerKind.Underground);

            var claims = new List<SpatialReservation>(reservations.Reservations.Count);
            for (var i = 0; i < reservations.Reservations.Count; i++)
            {
                SpatialReservation existing = reservations.Reservations[i];
                if (string.Equals(existing.OwnerId, hostOwner, StringComparison.Ordinal))
                    continue;
                claims.Add(existing);
            }

            return SpatialReservationSnapshot.Create(
                    claims,
                    reservations.Window,
                    reservations.BucketSizeDm)
                .Query(candidate, ReservationConsumerKind.Underground);
        }

        private static string FindHostOwner(
            SpatialReservationSnapshot reservations,
            BuildingPlot plot)
        {
            for (var i = 0; i < reservations.Reservations.Count; i++)
            {
                SpatialReservation existing = reservations.Reservations[i];
                if (existing.Category != ReservationCategory.Building
                    || (existing.Semantics & ReservationSemantics.HardOccupancy) == 0)
                    continue;
                if (existing.Bounds.MinX == plot.PositionDm.X
                    && existing.Bounds.MinZ == plot.PositionDm.Y)
                    return existing.OwnerId;
            }
            return null;
        }
    }
}
