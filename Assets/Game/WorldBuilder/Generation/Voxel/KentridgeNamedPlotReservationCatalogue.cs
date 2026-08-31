using System;
using MountingForce.WorldGen.Content.Kentridge;

using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Applies the settlement's canonical named-site reservations before secondary urban catalogues
    /// are combined with gameplay structures. Secondary stages own infill, courts, galleries and
    /// access; they do not get to overwrite stable named lots and rely on precedence to hide collision.
    /// </summary>
    internal static class KentridgeNamedPlotReservationCatalogue
    {
        public static FeatureCatalogue Apply(
            FeatureCatalogue catalogue,
            SettlementPlan settlement,
            VoxelWorldGenSettings settings)
        {
            int scale = settings.VoxelsPerDecimetre;
            if (scale <= 0)
                throw new ArgumentOutOfRangeException(nameof(settings));

            SpatialReservationSnapshot reservations =
                KentridgeTownPlanner.BuildReservationSnapshot(settlement.Seed);

            try
            {
                for (int ruleIndex = 0; ruleIndex < catalogue.Rules.Length; ruleIndex++)
                {
                    PlacementRule rule = catalogue.Rules[ruleIndex];
                    FeatureDefinition definition = catalogue.Definitions[rule.DefinitionId];
                    int first = rule.ExplicitOffset;
                    int end = first + rule.ExplicitCount;
                    int write = first;

                    for (int read = first; read < end; read++)
                    {
                        ExplicitPlacement placement = catalogue.ExplicitPlacements[read];
                        if (BlockedByNamedReservation(
                            in definition,
                            in placement,
                            reservations,
                            scale,
                            ruleIndex,
                            read))
                            continue;

                        catalogue.ExplicitPlacements[write] = placement;
                        write++;
                    }

                    for (int clear = write; clear < end; clear++)
                        catalogue.ExplicitPlacements[clear] = default;

                    rule.ExplicitCount = write - first;
                    catalogue.Rules[ruleIndex] = rule;
                }

                CatalogueLoadResult result = FeatureCatalogueBuilder.Finalise(ref catalogue);
                if (result != CatalogueLoadResult.Ok)
                    throw new InvalidOperationException(
                        "Kentridge named-plot reservation produced an invalid catalogue: " + result);
                return catalogue;
            }
            catch
            {
                catalogue.Dispose();
                throw;
            }
        }

        private static bool BlockedByNamedReservation(
            in FeatureDefinition definition,
            in ExplicitPlacement placement,
            SpatialReservationSnapshot reservations,
            int scale,
            int ruleIndex,
            int placementIndex)
        {
            bool quarterTurn = (placement.Orientation & 1) != 0;
            int width = quarterTurn ? definition.Footprint.z : definition.Footprint.x;
            int depth = quarterTurn ? definition.Footprint.x : definition.Footprint.z;
            int minX = FloorDiv(placement.Position.x, scale);
            int minZ = FloorDiv(placement.Position.z, scale);
            int maxX = CeilDiv(checked(placement.Position.x + width), scale);
            int maxZ = CeilDiv(checked(placement.Position.z + depth), scale);

            SpatialReservation candidate = SpatialReservation.Box(
                "kentridge-secondary:" + ruleIndex + ":" + placementIndex,
                ReservationCategory.StructuralChild,
                ReservationSemantics.HardOccupancy,
                new ReservationBoundsDm(minX, -1000000, minZ, maxX, 1000000, maxZ),
                precedence: 0,
                compatibleConsumers: ReservationConsumerKind.None,
                provenance: "KentridgeNamedPlotReservationCatalogue");
            ReservationQueryResult result = reservations.Query(
                candidate,
                ReservationConsumerKind.StructuralChild,
                ReservationCategory.Building | ReservationCategory.Plaza);
            return result.Decision == ReservationDecision.Rejected;
        }

        private static int FloorDiv(int value, int divisor)
        {
            int quotient = value / divisor;
            int remainder = value % divisor;
            return remainder < 0 ? quotient - 1 : quotient;
        }

        private static int CeilDiv(int value, int divisor) => -FloorDiv(-value, divisor);
    }
}
