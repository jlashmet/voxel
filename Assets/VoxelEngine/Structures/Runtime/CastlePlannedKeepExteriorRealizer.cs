using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Realizes the keep exterior for a spatially planned castle. Fixed masonry courses and
    /// weathering delegate to the shared facade recipe, while architectural annex choices such as
    /// the rear oriel are consumed from the preplanned keep-annex semantics.
    /// </summary>
    internal static class CastlePlannedKeepExteriorRealizer
    {
        internal static void Build(
            ref VoxelBrush brush,
            in CastlePlan plan,
            in CastleKeepAnnexPlan annexes)
        {
            CastleKeepAnnexPlanValidator.RequireValid(in annexes);

            int3 min = CastleSpatialProjection.KeepMinimum(in plan);
            int baseY = min.y;
            var size = new int3(
                plan.KeepHalfX * 2,
                plan.KeepHeight,
                plan.KeepHalfZ * 2);

            CastleKeepFacadeRealizer.Build(
                ref brush, in plan, min, size, baseY, plan.Floors);
            if (annexes.HasRearOriel)
                CastleRearOrielRealizer.Build(ref brush, in plan);
        }
    }
}
