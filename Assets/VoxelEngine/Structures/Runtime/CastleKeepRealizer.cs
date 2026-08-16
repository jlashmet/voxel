using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Thin compatibility-only six-substage keep coordinator. Spatial castles dispatch their
    /// planner-owned keep stages directly from CastleBuildPipeline. Geometry is delegated to
    /// focused keep components rather than authored here.
    /// </summary>
    internal static class CastleKeepRealizer
    {
        internal static bool TryStep(ref VoxelBrush brush, in CastlePlan plan, ref int stage)
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
                    CastleKeepFloorRealizer.BuildCompatibility(
                        ref brush, in plan, min, size, baseY, floors);
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
                    CastleRearOrielRealizer.Build(ref brush, in plan);
                    break;
            }

            stage++;
            return true;
        }
    }
}
