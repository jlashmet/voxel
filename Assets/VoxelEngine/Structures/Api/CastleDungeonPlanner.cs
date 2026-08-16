using System;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Convenience adapter for callers that still hold a CastleSpatialPlan. CastleDungeonPlanning
    /// owns the single castle-specific constraint mapping into the reusable DungeonPlanner; this
    /// type only resolves the shared spatial projection and delegates to that policy owner.
    /// </summary>
    public static class CastleDungeonPlanner
    {
        public static DungeonPlan Create(
            in CastlePlan dimensions,
            CastleSpatialPlan spatial)
        {
            if (spatial == null) throw new ArgumentNullException(nameof(spatial));
            if (spatial.KeepRequiresTerrainResolution)
            {
                throw new InvalidOperationException(
                    "Castle dungeon planning requires a resolved keep placement.");
            }

            CastleSpatialProjection projection = CastleSpatialProjection.Create(
                in dimensions, spatial);
            return CastleDungeonPlanning.Create(in dimensions, in projection);
        }
    }
}
