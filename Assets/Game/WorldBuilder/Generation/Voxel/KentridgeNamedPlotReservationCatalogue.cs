using System;
using MountingForce.WorldGen.Content.Kentridge;

using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Applies the settlement's declared named-plot spacing before secondary urban catalogues are
    /// combined with gameplay structures. Secondary stages own infill, courts, galleries and access;
    /// they do not get to overwrite stable named lots and rely on precedence to hide the collision.
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

            int spacingDm = KentridgeTownPlanner.CompositionPolicy.Density.MinSpacingDm;
            int spacing = checked(spacingDm * scale);

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
                        if (IntersectsNamedReservation(
                            in definition, in placement, settlement, scale, spacing))
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

        private static bool IntersectsNamedReservation(
            in FeatureDefinition definition,
            in ExplicitPlacement placement,
            SettlementPlan settlement,
            int scale,
            int spacing)
        {
            bool quarterTurn = (placement.Orientation & 1) != 0;
            int width = quarterTurn ? definition.Footprint.z : definition.Footprint.x;
            int depth = quarterTurn ? definition.Footprint.x : definition.Footprint.z;
            int minX = placement.Position.x;
            int minZ = placement.Position.z;
            int maxX = checked(minX + width);
            int maxZ = checked(minZ + depth);

            for (int plotIndex = 0; plotIndex < settlement.Plots.Count; plotIndex++)
            {
                BuildingPlot plot = settlement.Plots[plotIndex];
                Int3 footprintDm = SettlementFootprints.For(settlement, plot.Archetype);
                int reservedMinX = checked(plot.PositionDm.X * scale - spacing);
                int reservedMinZ = checked(plot.PositionDm.Y * scale - spacing);
                int reservedMaxX = checked(
                    (plot.PositionDm.X + footprintDm.X) * scale + spacing);
                int reservedMaxZ = checked(
                    (plot.PositionDm.Y + footprintDm.Z) * scale + spacing);

                if (maxX > reservedMinX && minX < reservedMaxX
                    && maxZ > reservedMinZ && minZ < reservedMaxZ)
                    return true;
            }

            return false;
        }
    }
}
