using System;
using System.Collections.Generic;

namespace MountingForce.WorldGen.Content.Kentridge
{
    /// <summary>
    /// Pure semantic tree placement for Kentridge.
    ///
    /// Trees are deliberate pieces of the settlement composition rather than random scatter:
    /// residential yards get one strong silhouette each, civic and noble districts get older
    /// specimen trees, the market receives a light promenade rhythm, and a sparse perimeter belt
    /// transitions the authored town back into wilderness. Every candidate is filtered against
    /// roads, the market square, and building envelopes before it leaves this planner.
    /// </summary>
    public static class KentridgeVegetationLayoutPlanner
    {
        private const int BuildingClearanceDm = 12;
        private const int RoadClearanceDm = 10;
        private const int PlazaClearanceDm = 8;

        public static List<VegetationCandidate> Build(SettlementPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));

            var result = new List<VegetationCandidate>(48);
            int ordinal = 0;

            AddResidentialTrees(plan, result, ref ordinal);
            AddMarketTrees(plan, result, ref ordinal);
            AddUpperTownTrees(plan, result, ref ordinal);
            AddCivicTrees(plan, result, ref ordinal);
            AddNobleTrees(plan, result, ref ordinal);
            AddPerimeterBelt(plan, result, ref ordinal);

            return result;
        }

        private static void AddResidentialTrees(SettlementPlan plan,
                                                List<VegetationCandidate> result,
                                                ref int ordinal)
        {
            for (int i = 0; i < plan.Plots.Count; i++)
            {
                BuildingPlot plot = plan.Plots[i];
                if (plot.District != DistrictKind.Residential) continue;

                Int3 footprint = KentridgeDefinition.FootprintDm(plot.Archetype);
                int hash = StableHash(plan.Seed, plot.RoleId);
                int side = (hash & 1) == 0 ? -1 : 1;
                ResidentialTreePoint(plot, footprint, side, out int x, out int z);

                SemanticTreeSpecies species;
                if (plot.RoleId == (int)KentridgeRole.AbandonedHouse)
                    species = SemanticTreeSpecies.Dead;
                else
                    species = ResidentialSpecies(hash);

                int height = 86 + PositiveMod(hash >> 3, 30);
                if (TryAdd(plan, result, x, z, height, species, ref ordinal))
                    continue;

                // Dense mixed-use blocks can occupy the first hashed side of a house. Preserve the
                // stable hash preference, but deterministically mirror to the other side rather than
                // silently dropping that residence's tree identity.
                ResidentialTreePoint(plot, footprint, -side, out x, out z);
                if (!TryAdd(plan, result, x, z, height, species, ref ordinal))
                    throw new InvalidOperationException(
                        "Kentridge residence has no clear semantic tree side: role " + plot.RoleId);
            }
        }

        private static void ResidentialTreePoint(BuildingPlot plot, Int3 footprint, int side,
                                                 out int x, out int z)
        {
            if (plot.Frontage == FrontageDirection.South
                || plot.Frontage == FrontageDirection.North)
            {
                x = side < 0
                    ? plot.PositionDm.X - 22
                    : plot.PositionDm.X + footprint.X + 22;
                z = plot.PositionDm.Y + footprint.Z * 3 / 4;
            }
            else
            {
                x = plot.PositionDm.X + footprint.X * 3 / 4;
                z = side < 0
                    ? plot.PositionDm.Y - 22
                    : plot.PositionDm.Y + footprint.Z + 22;
            }
        }

        private static void AddMarketTrees(SettlementPlan plan,
                                           List<VegetationCandidate> result,
                                           ref int ordinal)
        {
            // Kept outside the market-square rectangle itself: stalls and the well remain the focus,
            // while the trees frame approaches and break up the long shop frontage.
            Add(plan, result, 730, 455, 96, SemanticTreeSpecies.Maple, ref ordinal);
            Add(plan, result, 900, 455, 90, SemanticTreeSpecies.Birch, ref ordinal);
            Add(plan, result, 1030, 455, 102, SemanticTreeSpecies.Maple, ref ordinal);
            Add(plan, result, 1310, 570, 92, SemanticTreeSpecies.Birch, ref ordinal);
        }

        private static void AddUpperTownTrees(SettlementPlan plan,
                                              List<VegetationCandidate> result,
                                              ref int ordinal)
        {
            // The inn's upper shoulder is intentionally narrow; two trees on its western edge make
            // the step up from the market visible without hiding the facade or main-spine climb.
            Add(plan, result, 914, 266, 112, SemanticTreeSpecies.Oak, ref ordinal);
            Add(plan, result, 914, 408, 104, SemanticTreeSpecies.Maple, ref ordinal);
        }

        private static void AddCivicTrees(SettlementPlan plan,
                                          List<VegetationCandidate> result,
                                          ref int ordinal)
        {
            Add(plan, result, 932, 82, 126, SemanticTreeSpecies.Oak, ref ordinal);
            Add(plan, result, 932, 218, 118, SemanticTreeSpecies.Oak, ref ordinal);
            Add(plan, result, 1380, 82, 110, SemanticTreeSpecies.Maple, ref ordinal);
            Add(plan, result, 1380, 220, 104, SemanticTreeSpecies.Birch, ref ordinal);
        }

        private static void AddNobleTrees(SettlementPlan plan,
                                          List<VegetationCandidate> result,
                                          ref int ordinal)
        {
            // Radcliffe's ridge is compact, so use a formal east-side line rather than a dense grove.
            Add(plan, result, 1818, 118, 116, SemanticTreeSpecies.Maple, ref ordinal);
            Add(plan, result, 1818, 248, 126, SemanticTreeSpecies.Oak, ref ordinal);
            Add(plan, result, 1818, 382, 112, SemanticTreeSpecies.Maple, ref ordinal);
        }

        private static void AddPerimeterBelt(SettlementPlan plan,
                                             List<VegetationCandidate> result,
                                             ref int ordinal)
        {
            // Sparse, asymmetric edge planting. These are intentionally outside the urban shelves
            // and use sampled natural terrain so the authored town dissolves into wilderness instead
            // of ending at a rectangular generation boundary.
            (int x, int z, SemanticTreeSpecies species, int h)[] points =
            {
                (570, 120, SemanticTreeSpecies.Pine, 142),
                (565, 330, SemanticTreeSpecies.Oak, 118),
                (575, 560, SemanticTreeSpecies.Pine, 148),
                (560, 790, SemanticTreeSpecies.Birch, 108),
                (580, 1035, SemanticTreeSpecies.Pine, 152),

                (1850, 110, SemanticTreeSpecies.Pine, 150),
                (1850, 330, SemanticTreeSpecies.Oak, 126),
                (1840, 570, SemanticTreeSpecies.Pine, 146),
                (1850, 820, SemanticTreeSpecies.Maple, 112),
                (1840, 1040, SemanticTreeSpecies.Pine, 154),

                (735, 1150, SemanticTreeSpecies.Oak, 118),
                (1035, 1160, SemanticTreeSpecies.Pine, 148),
                (1390, 1155, SemanticTreeSpecies.Birch, 112),
                (1705, 1145, SemanticTreeSpecies.Pine, 152),

                (745, -120, SemanticTreeSpecies.Pine, 146),
                (1040, -130, SemanticTreeSpecies.Oak, 124),
                (1360, -125, SemanticTreeSpecies.Pine, 150),
                (1680, -110, SemanticTreeSpecies.Birch, 116),
            };

            for (int i = 0; i < points.Length; i++)
                Add(plan, result, points[i].x, points[i].z,
                    points[i].h, points[i].species, ref ordinal);
        }

        private static void Add(SettlementPlan plan, List<VegetationCandidate> result,
                                int x, int z, int heightDm, SemanticTreeSpecies species,
                                ref int ordinal)
        {
            if (!TryAdd(plan, result, x, z, heightDm, species, ref ordinal))
                throw new InvalidOperationException(
                    $"Authored Kentridge vegetation point {x},{z} collides with settlement geometry.");
        }

        private static bool TryAdd(SettlementPlan plan, List<VegetationCandidate> result,
                                   int x, int z, int heightDm, SemanticTreeSpecies species,
                                   ref int ordinal)
        {
            if (BlockedByBuilding(plan, x, z)
                || BlockedByStreet(plan, x, z)
                || BlockedByPlaza(plan, x, z))
                return false;

            // Surface limits are backend hints only. The Kentridge voxel adapter uses the authored
            // macro profile for urban trees and the resident terrain column for perimeter trees.
            result.Add(VegetationCandidate.Surface(
                x, z, heightDm, species, maxY: 512, minY: 1, ordinal: ordinal++));
            return true;
        }

        private static bool BlockedByBuilding(SettlementPlan plan, int x, int z)
        {
            for (int i = 0; i < plan.Plots.Count; i++)
            {
                BuildingPlot plot = plan.Plots[i];
                Int3 fp = KentridgeDefinition.FootprintDm(plot.Archetype);
                if (InsideExpandedRect(
                        x, z,
                        plot.PositionDm.X, plot.PositionDm.Y,
                        fp.X, fp.Z,
                        BuildingClearanceDm))
                    return true;
            }
            return false;
        }

        private static bool BlockedByStreet(SettlementPlan plan, int x, int z)
        {
            for (int s = 0; s < plan.Streets.Count; s++)
            {
                PlannedStreet street = plan.Streets[s];
                int radius = street.WidthDm / 2 + RoadClearanceDm;
                for (int i = 0; i + 1 < street.Points.Count; i++)
                {
                    Int2 a = street.Points[i];
                    Int2 b = street.Points[i + 1];
                    int minX = Math.Min(a.X, b.X) - radius;
                    int maxX = Math.Max(a.X, b.X) + radius;
                    int minZ = Math.Min(a.Y, b.Y) - radius;
                    int maxZ = Math.Max(a.Y, b.Y) + radius;
                    if (x >= minX && x <= maxX && z >= minZ && z <= maxZ)
                        return true;
                }
            }
            return false;
        }

        private static bool BlockedByPlaza(SettlementPlan plan, int x, int z)
        {
            PlannedPlaza plaza = plan.Plaza;
            int minX = plaza.CentreDm.X - plaza.SizeDm.X / 2;
            int minZ = plaza.CentreDm.Y - plaza.SizeDm.Y / 2;
            return InsideExpandedRect(
                x, z, minX, minZ, plaza.SizeDm.X, plaza.SizeDm.Y, PlazaClearanceDm);
        }

        private static bool InsideExpandedRect(int x, int z,
                                               int minX, int minZ,
                                               int width, int depth,
                                               int clearance)
        {
            return x >= minX - clearance
                && x <= minX + width + clearance
                && z >= minZ - clearance
                && z <= minZ + depth + clearance;
        }

        private static SemanticTreeSpecies ResidentialSpecies(int hash)
        {
            switch (PositiveMod(hash, 4))
            {
                case 0: return SemanticTreeSpecies.Oak;
                case 1: return SemanticTreeSpecies.Birch;
                case 2: return SemanticTreeSpecies.Maple;
                default: return SemanticTreeSpecies.Oak;
            }
        }

        private static int StableHash(uint seed, int value)
        {
            uint x = seed ^ ((uint)value * 0x9E3779B9u);
            x ^= x >> 16;
            x *= 0x7FEB352Du;
            x ^= x >> 15;
            x *= 0x846CA68Bu;
            x ^= x >> 16;
            return unchecked((int)x);
        }

        private static int PositiveMod(int value, int divisor)
        {
            int result = value % divisor;
            return result < 0 ? result + divisor : result;
        }
    }
}
