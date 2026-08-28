using System;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Exact walkable access line for one generated Kentridge building. All points use the same
    /// backend units as the realized entrance, so gameplay can move through the physical doorway
    /// without reconstructing placement offsets or hard-coding town coordinates.
    /// </summary>
    public readonly struct KentridgeGameplaySiteAccess
    {
        public RealizedWorldPoint Entrance { get; }
        public RealizedWorldPoint InteriorApproach { get; }
        public RealizedWorldPoint ExteriorApproach { get; }
        public Int2 Inward { get; }

        public KentridgeGameplaySiteAccess(
            RealizedWorldPoint entrance,
            RealizedWorldPoint interiorApproach,
            RealizedWorldPoint exteriorApproach,
            Int2 inward)
        {
            Entrance = entrance;
            InteriorApproach = interiorApproach;
            ExteriorApproach = exteriorApproach;
            Inward = inward;
        }
    }

    /// <summary>
    /// Gameplay-facing access facts derived from the same stable plot role and placement transform
    /// used by Kentridge voxel emission. The approach vector comes from semantic public access rather
    /// than reconstructing one of four cardinal directions from the structure's quarter-turn.
    /// </summary>
    public static class KentridgeGameplaySiteAccessResolver
    {
        public const int ApproachDistanceDecimetres = 18;

        public static bool TryResolve(
            SettlementPlan plan,
            int roleId,
            int unitsPerDecimetre,
            out KentridgeGameplaySiteAccess access)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (unitsPerDecimetre <= 0)
                throw new ArgumentOutOfRangeException(nameof(unitsPerDecimetre));

            BuildingPlot plot = default(BuildingPlot);
            bool found = false;
            for (int i = 0; i < plan.Plots.Count; i++)
            {
                if (plan.Plots[i].RoleId != roleId) continue;
                plot = plan.Plots[i];
                found = true;
                break;
            }

            if (!found)
            {
                access = default(KentridgeGameplaySiteAccess);
                return false;
            }

            var realization = new KentridgeVoxelSiteRealizationFacts(plan, unitsPerDecimetre);
            RealizedWorldPoint entrance;
            if (!realization.TryGetPublicEntrance(roleId, out entrance))
            {
                access = default(KentridgeGameplaySiteAccess);
                return false;
            }

            Int2 inward = new Int2(plot.AccessDirection.X, plot.AccessDirection.Z);
            int distance = ApproachDistanceDecimetres * unitsPerDecimetre;
            RealizedWorldPoint interior = Offset(entrance, inward, distance);
            RealizedWorldPoint exterior = Offset(entrance, inward, -distance);
            access = new KentridgeGameplaySiteAccess(entrance, interior, exterior, inward);
            return true;
        }

        private static RealizedWorldPoint Offset(
            RealizedWorldPoint origin,
            Int2 direction,
            int distance)
        {
            Int3 point = origin.Position;
            return new RealizedWorldPoint(
                new Int3(
                    point.X + direction.X * distance,
                    point.Y,
                    point.Z + direction.Y * distance),
                origin.UnitsPerDecimetre);
        }
    }
}