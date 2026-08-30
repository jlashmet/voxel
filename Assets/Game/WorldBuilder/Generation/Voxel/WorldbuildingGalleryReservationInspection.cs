using System;
using System.Collections.Generic;
using System.Diagnostics;
using MountingForce.WorldGen.Architecture;
using MountingForce.WorldGen.Content.Kentridge;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Read-only presentation data for the Worldbuilding Gallery. All values are copied from canonical
    /// semantic claims; the gallery may render them but cannot mutate reservation authority.
    /// </summary>
    public readonly struct ReservationInspectionPrimitive
    {
        public readonly string Label;
        public readonly ReservationBoundsDm BoundsDm;
        public readonly ReservationCategory Category;
        public readonly ReservationSemantics Semantics;

        public ReservationInspectionPrimitive(
            string label,
            ReservationBoundsDm boundsDm,
            ReservationCategory category,
            ReservationSemantics semantics)
        {
            Label = label ?? string.Empty;
            BoundsDm = boundsDm;
            Category = category;
            Semantics = semantics;
        }
    }

    public sealed class WorldbuildingGalleryReservationReport
    {
        private readonly ReservationInspectionPrimitive[] _primitives;
        public IReadOnlyList<ReservationInspectionPrimitive> Primitives => _primitives;
        public ReservationBoundsDm Window { get; }
        public string RejectedCandidateDescription { get; }
        public ReservationQueryMetrics RejectedCandidateMetrics { get; }
        public long BuildStopwatchTicks { get; }
        public int SourceClaimCount { get; }

        internal WorldbuildingGalleryReservationReport(
            ReservationInspectionPrimitive[] primitives,
            ReservationBoundsDm window,
            string rejectedCandidateDescription,
            ReservationQueryMetrics rejectedCandidateMetrics,
            long buildStopwatchTicks,
            int sourceClaimCount)
        {
            _primitives = primitives ?? Array.Empty<ReservationInspectionPrimitive>();
            Window = window;
            RejectedCandidateDescription = rejectedCandidateDescription ?? string.Empty;
            RejectedCandidateMetrics = rejectedCandidateMetrics;
            BuildStopwatchTicks = buildStopwatchTicks;
            SourceClaimCount = sourceClaimCount;
        }
    }

    public static class WorldbuildingGalleryReservationInspection
    {
        public const uint EvidenceSeed = 0x4B454E54u;
        private const int MaxSurfaceClaims = 24;

        public static WorldbuildingGalleryReservationReport Build(uint seed = EvidenceSeed)
        {
            long started = Stopwatch.GetTimestamp();
            SettlementPlan plan = KentridgeTownPlanner.Build(seed);
            SpatialReservationSnapshot snapshot = KentridgeTownPlanner.BuildReservationSnapshot(seed);
            var primitives = new List<ReservationInspectionPrimitive>(MaxSurfaceClaims + 1);

            bool sawHard = false, sawClearance = false, sawAccess = false, sawRoad = false;
            for (int i = 0; i < snapshot.Reservations.Count && primitives.Count < MaxSurfaceClaims; i++)
            {
                SpatialReservation claim = snapshot.Reservations[i];
                bool hard = (claim.Semantics & ReservationSemantics.HardOccupancy) != 0;
                bool clearance = (claim.Semantics & ReservationSemantics.Clearance) != 0;
                bool access = claim.Category == ReservationCategory.PublicAccess;
                bool road = claim.Category == ReservationCategory.Road;
                if (!(hard && !sawHard) && !(clearance && !sawClearance)
                    && !(access && !sawAccess) && !(road && !sawRoad))
                    continue;

                primitives.Add(new ReservationInspectionPrimitive(
                    claim.OwnerId, claim.Bounds, claim.Category, claim.Semantics));
                sawHard |= hard;
                sawClearance |= clearance;
                sawAccess |= access;
                sawRoad |= road;
            }

            TryAddRealHiddenSpace(plan, primitives);

            SpatialReservation existing = FindFirstHardBuilding(snapshot);
            SpatialReservation rejected = SpatialReservation.Box(
                "gallery:deliberate-rejected-candidate",
                ReservationCategory.Landmark,
                ReservationSemantics.HardOccupancy,
                existing.Bounds,
                precedence: existing.Precedence,
                provenance: "WorldbuildingGalleryReservationInspection deliberate conflict");
            ReservationQueryResult rejection = snapshot.Query(
                rejected,
                ReservationConsumerKind.Landmark,
                ReservationCategory.Building | ReservationCategory.Plaza
                | ReservationCategory.Road | ReservationCategory.PublicAccess);
            if (rejection.Decision != ReservationDecision.Rejected)
                throw new InvalidOperationException(
                    "Gallery reservation inspector expected its deliberate candidate to be rejected.");

            return new WorldbuildingGalleryReservationReport(
                primitives.ToArray(),
                snapshot.Window,
                rejection.Describe(),
                rejection.Metrics,
                Stopwatch.GetTimestamp() - started,
                snapshot.Reservations.Count);
        }

        private static SpatialReservation FindFirstHardBuilding(SpatialReservationSnapshot snapshot)
        {
            for (int i = 0; i < snapshot.Reservations.Count; i++)
            {
                SpatialReservation claim = snapshot.Reservations[i];
                if (claim.Category == ReservationCategory.Building
                    && (claim.Semantics & ReservationSemantics.HardOccupancy) != 0)
                    return claim;
            }
            throw new InvalidOperationException("Gallery reservation inspector found no hard building claim.");
        }

        private static void TryAddRealHiddenSpace(
            SettlementPlan plan,
            List<ReservationInspectionPrimitive> primitives)
        {
            for (int i = 0; i < plan.Plots.Count; i++)
            {
                BuildingPlot plot = plan.Plots[i];
                var request = new SiteHiddenSpaceRequest(
                    "gallery-reservation:" + plot.RoleId,
                    plot.RoleId,
                    minimumCount: 0,
                    targetCount: 1,
                    entrance: HiddenSpaceEntranceKind.BreakableMatchingWall);
                IReadOnlyList<KentridgeHiddenSpaceGeometry> candidates =
                    KentridgeHiddenSpacePlanner.Resolve(plot, plan.Seed, request);
                if (candidates.Count == 0) continue;
                SpatialReservation hidden = WorldBuilderReservationFactory.HiddenSpaceVolume(
                    candidates[0].Realization,
                    new Int3(plot.PositionDm.X, 0, plot.PositionDm.Y));
                primitives.Add(new ReservationInspectionPrimitive(
                    hidden.OwnerId, hidden.Bounds, hidden.Category, hidden.Semantics));
                return;
            }
            throw new InvalidOperationException("Gallery reservation inspector could not realize a hidden-space example.");
        }
    }
}
