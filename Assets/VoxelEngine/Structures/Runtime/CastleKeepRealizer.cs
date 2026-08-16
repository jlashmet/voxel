using System;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Incrementally realizes the occupied keep from a precomputed castle plan.
    ///
    /// The migrated substages deliberately preserve the legacy order and geometry. The final
    /// roof/annex substage remains on the migration fallback until its larger helper graph is
    /// extracted as one unit.
    /// </summary>
    internal static class CastleKeepRealizer
    {
        /// <summary>Compatibility path for castles without explicit keep-floor semantics.</summary>
        internal static bool TryStep(ref VoxelBrush brush, in CastlePlan plan, ref int stage) =>
            TryStepCore(ref brush, in plan, null, ref stage);

        /// <summary>
        /// Spatial path: realizes supplied semantic floor purposes without choosing them from the
        /// physical floor index. Existing furnishing recipes remain behavior-compatible.
        /// </summary>
        internal static bool TryStep(
            ref VoxelBrush brush,
            in CastlePlan plan,
            CastleKeepFloorPlan[] roomPlans,
            ref int stage)
        {
            if (roomPlans == null || roomPlans.Length != plan.Floors)
                throw new InvalidOperationException("Castle keep realization requires one planned room per floor.");
            return TryStepCore(ref brush, in plan, roomPlans, ref stage);
        }

        private static bool TryStepCore(
            ref VoxelBrush brush,
            in CastlePlan plan,
            CastleKeepFloorPlan[] roomPlans,
            ref int stage)
        {
            if (stage < 0 || stage >= 6) return false;

            int baseY = plan.Centre.y + plan.PlateauHeight;
            int hx = plan.KeepHalfX;
            int hz = plan.KeepHalfZ;
            var min = new int3(plan.Centre.x - hx, baseY, plan.Centre.z - hz + 60);
            var size = new int3(hx * 2, plan.KeepHeight, hz * 2);
            int floors = plan.Floors;

            switch (stage)
            {
                case 0:
                    CastleKeepShellRealizer.Build(ref brush, min, size, baseY);
                    break;

                case 1:
                    CastleKeepTurretRealizer.Build(ref brush, in plan, min, size, baseY);
                    break;

                case 2:
                    CastleKeepFloorRealizer.Build(
                        ref brush, in plan, min, size, baseY, floors, roomPlans);
                    break;

                case 3:
                    BuildCirculation(ref brush, in plan, min, size, baseY, floors);
                    break;

                case 4:
                    CastleKeepFenestrationRealizer.Build(
                        ref brush, in plan, min, size, baseY, floors);
                    break;

                case 5:
                    CastleKeepFacadeRealizer.Build(
                        ref brush, in plan, min, size, baseY, floors);
                    if (roomPlans == null)
                        CastleRearOrielRealizer.Build(ref brush, in plan);
                    break;
            }

            stage++;
            return true;
        }

        private static void BuildCirculation(ref VoxelBrush brush, in CastlePlan plan,
                                             int3 min, int3 size, int baseY, int floors)
        {
            int entranceX = plan.Centre.x;
            brush.Arch(new int3(entranceX - 15, baseY + 1, min.z - 1),
                       30, 34, 10, 2, Mat.Empty);
            brush.Box(new int3(entranceX - 15, baseY + 2, min.z + 9),
                      new int3(4, 29, 3), Mat.Wood);
            brush.Box(new int3(entranceX + 11, baseY + 2, min.z + 9),
                      new int3(4, 29, 3), Mat.Wood);
            brush.Box(new int3(entranceX - 9, baseY + 1, min.z + 8),
                      new int3(18, 24, size.z / 2 - 28), Mat.Empty);

            int grandX = plan.Centre.x - 68;
            int grandZ = min.z + 28;
            const int grandWidth = 18;
            const int grandRise = 2;
            const int grandRun = 3;
            int grandSteps = plan.FloorHeight / grandRise;
            brush.Box(new int3(grandX, baseY + 1, grandZ),
                      new int3(grandWidth, plan.FloorHeight + 18, grandSteps * grandRun), Mat.Empty);
            brush.Stairs(new int3(grandX, baseY + 1, grandZ), grandWidth,
                         grandSteps, grandRise, grandRun, 2, Mat.Wood);
            brush.Box(new int3(grandX - 3, baseY + 1, grandZ), new int3(3, 20, 3), Mat.Wood);
            brush.Box(new int3(grandX + grandWidth, baseY + 1, grandZ),
                      new int3(3, 20, 3), Mat.Wood);

            int stairX = min.x + 34;
            int stairZ = min.z + 34;
            const int stairRadius = 22;
            brush.SpiralStair(stairX, baseY + 2, stairZ, stairRadius,
                              floors * plan.FloorHeight, Mat.Stone);
        }
    }
}
