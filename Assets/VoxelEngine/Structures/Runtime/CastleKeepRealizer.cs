using System;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Thin six-substage keep coordinator. Compatibility builds preserve the historical stage order;
    /// spatial builds bypass substages that already have explicit planner-owned realization data.
    /// Geometry is delegated to focused keep components rather than authored here.
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
            var min = new int3(
                plan.Centre.x - hx,
                baseY,
                plan.Centre.z - hz + CastleLayout.LegacyKeepCentreZOffset);
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
                    CastleKeepCompatibilityCirculationRealizer.Build(
                        ref brush, in plan, min, size, baseY, floors);
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
    }
}
