using System;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Exact walkable access line for one generated Kentridge building. InteriorInward follows the
    /// quarter-turn door normal required by the current architecture grammar; PublicApproachInward
    /// follows the independently inferred route and may be diagonal.
    /// </summary>
    public readonly struct KentridgeGameplaySiteAccess
    {
        public RealizedWorldPoint Entrance { get; }
        public RealizedWorldPoint InteriorApproach { get; }
        public RealizedWorldPoint ExteriorApproach { get; }
        public Int2 Inward { get; }
        public Int2 PublicApproachInward { get; }

        public KentridgeGameplaySiteAccess(
            RealizedWorldPoint entrance,
            RealizedWorldPoint interiorApproach,
            RealizedWorldPoint exteriorApproach,
            Int2 inward,
            Int2 publicApproachInward)
        {
            Entrance = entrance;
            InteriorApproach = interiorApproach;
            ExteriorApproach = exteriorApproach;
            Inward = inward;
            PublicApproachInward = publicApproachInward;
        }
    }

    /// <summary>
    /// Gameplay-facing access facts derived from stable plot role, physical entrance realization and
    /// semantic public circulation. The interior point remains normal to the actual doorway while the
    /// exterior point can approach diagonally from an organic route.
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

            Int2 doorInward = DoorInward(plot.Frontage);
            Int2 publicInward = new Int2(plot.AccessDirection.X, plot.AccessDirection.Z);
            int distance = ApproachDistanceDecimetres * unitsPerDecimetre;
            RealizedWorldPoint interior = Offset(entrance, doorInward, distance);
            RealizedWorldPoint exterior = Offset(entrance, publicInward, -distance);
            access = new KentridgeGameplaySiteAccess(
                entrance, interior, exterior, doorInward, publicInward);
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

        private static Int2 DoorInward(FrontageDirection frontage)
        {
            switch (frontage)
            {
                case FrontageDirection.West: return new Int2(-1, 0);
                case FrontageDirection.North: return new Int2(0, -1);
                case FrontageDirection.East: return new Int2(1, 0);
                default: return new Int2(0, 1);
            }
        }
    }
}