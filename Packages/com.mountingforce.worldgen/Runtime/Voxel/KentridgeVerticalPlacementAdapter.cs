using System;
using MountingForce.WorldGen.Content.Kentridge;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Terrain.Api;

using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Reuses the existing Kentridge feature vocabularies while moving their explicit instances onto
    /// the authored macro elevation profile. The geometry grammars stay untouched: verticality is a
    /// placement concern, not a second copy of every house, prop, fence, and market stall program.
    /// </summary>
    public static class KentridgeVerticalPlacementAdapter
    {
        private const int PlotFillDepthDm = 12;
        private const int PlotSurfaceThicknessDm = 1;
        private const int BuildingFoundationSinkDm = 5;

        public static FeatureCatalogue BuildPlotSurfaces(
            uint seed, VoxelWorldGenSettings settings, Allocator allocator)
        {
            FeatureCatalogue catalogue = KentridgePlotSurfaceCatalogue.Build(
                seed, settings, allocator);
            SettlementPlan plan = KentridgeDefinition.Build(seed);
            int scale = settings.VoxelsPerDecimetre;
            int placementIndex = 0;

            // KentridgePlotSurfaceCatalogue stores explicit placements grouped by archetype.
            for (int archetype = 0; archetype < 8; archetype++)
            {
                for (int i = 0; i < plan.Plots.Count; i++)
                {
                    BuildingPlot plot = plan.Plots[i];
                    if ((int)plot.Archetype != archetype || plot.Archetype == StructureArchetype.Well)
                        continue;

                    ExplicitPlacement placement = catalogue.ExplicitPlacements[placementIndex];
                    int targetSurface = KentridgeVerticalProfile.PlotSurfaceY(plot, seed, scale);
                    placement.Position.y = targetSurface
                                         - (PlotFillDepthDm + PlotSurfaceThicknessDm) * scale;
                    catalogue.ExplicitPlacements[placementIndex] = placement;
                    placementIndex++;
                }
            }

            if (placementIndex != catalogue.ExplicitPlacements.Length)
            {
                catalogue.Dispose();
                throw new InvalidOperationException(
                    "Vertical Kentridge plot adaptation did not visit every placement.");
            }

            return catalogue;
        }

        public static FeatureCatalogue BuildStructures(
            uint seed, VoxelWorldGenSettings settings, Allocator allocator)
        {
            FeatureCatalogue catalogue = KentridgeVoxelCatalogue.Build(seed, settings, allocator);
            SettlementPlan plan = KentridgeDefinition.Build(seed);
            int scale = settings.VoxelsPerDecimetre;
            int placementIndex = 0;

            // KentridgeVoxelCatalogue uses the same archetype grouping as the plot surface pass.
            for (int archetype = 0; archetype < 8; archetype++)
            {
                for (int i = 0; i < plan.Plots.Count; i++)
                {
                    BuildingPlot plot = plan.Plots[i];
                    if ((int)plot.Archetype != archetype) continue;

                    ExplicitPlacement placement = catalogue.ExplicitPlacements[placementIndex];
                    int targetSurface = KentridgeVerticalProfile.PlotSurfaceY(plot, seed, scale);
                    placement.Position.y = targetSurface - BuildingFoundationSinkDm * scale;
                    catalogue.ExplicitPlacements[placementIndex] = placement;
                    placementIndex++;
                }
            }

            if (placementIndex != catalogue.ExplicitPlacements.Length)
            {
                catalogue.Dispose();
                throw new InvalidOperationException(
                    "Vertical Kentridge structure adaptation did not visit every placement.");
            }

            return catalogue;
        }

        public static FeatureCatalogue BuildPlotDressing(
            uint seed, VoxelWorldGenSettings settings, Allocator allocator)
        {
            FeatureCatalogue catalogue = KentridgePlotDressingCatalogue.Build(
                seed, settings, allocator);
            SettlementPlan plan = KentridgeDefinition.Build(seed);
            int scale = settings.VoxelsPerDecimetre;

            for (int i = 0; i < catalogue.ExplicitPlacements.Length; i++)
            {
                ExplicitPlacement placement = catalogue.ExplicitPlacements[i];
                BuildingPlot plot = FindOwningPlot(plan, placement.Position.x, placement.Position.z, scale);
                int naturalLowest = KentridgeVerticalProfile.NaturalLowestUnderPlot(plot, seed, scale);
                int targetSurface = KentridgeVerticalProfile.PlotSurfaceY(plot, seed, scale);
                placement.Position.y += targetSurface - naturalLowest;
                catalogue.ExplicitPlacements[i] = placement;
            }

            return catalogue;
        }

        public static FeatureCatalogue BuildTownDressing(
            uint seed, VoxelWorldGenSettings settings, Allocator allocator)
        {
            FeatureCatalogue catalogue = KentridgeTownDressingCatalogue.Build(
                seed, settings, allocator);
            int scale = settings.VoxelsPerDecimetre;
            Int2 centre = KentridgeDefinition.TownCentreDm;
            int natural = TerrainQuery.HeightAt(centre.X * scale, centre.Y * scale, seed);
            int target = KentridgeVerticalProfile.SurfaceYAtDm(centre.X, centre.Y, seed, scale);
            int delta = target - natural;

            for (int i = 0; i < catalogue.ExplicitPlacements.Length; i++)
            {
                ExplicitPlacement placement = catalogue.ExplicitPlacements[i];
                placement.Position.y += delta;
                catalogue.ExplicitPlacements[i] = placement;
            }

            return catalogue;
        }

        private static BuildingPlot FindOwningPlot(SettlementPlan plan, int worldX, int worldZ,
                                                    int scale)
        {
            int xDm = FloorDiv(worldX, scale);
            int zDm = FloorDiv(worldZ, scale);

            for (int i = 0; i < plan.Plots.Count; i++)
            {
                BuildingPlot plot = plan.Plots[i];
                if (plot.Archetype == StructureArchetype.Well) continue;
                Int3 footprint = KentridgeDefinition.FootprintDm(plot.Archetype);

                if (xDm >= plot.PositionDm.X && xDm < plot.PositionDm.X + footprint.X
                    && zDm >= plot.PositionDm.Y && zDm < plot.PositionDm.Y + footprint.Z)
                    return plot;
            }

            throw new InvalidOperationException(
                "Kentridge plot dressing placement is outside every semantic plot at "
                + xDm + "," + zDm + ".");
        }

        private static int FloorDiv(int value, int divisor)
        {
            int q = value / divisor;
            int r = value % divisor;
            return r < 0 ? q - 1 : q;
        }
    }
}
