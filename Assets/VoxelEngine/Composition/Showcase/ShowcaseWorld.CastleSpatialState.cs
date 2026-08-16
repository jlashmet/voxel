using VoxelEngine.Structures.Api;

namespace VoxelEngine.Showcase
{
    public sealed partial class ShowcaseWorld
    {
        // Spatial planning remains separate from the historical CastlePlan dimensions. The
        // pending plan is handed to Runtime once every dependency region is resident; the compact
        // projection is retained afterwards for interaction/presentation coordinates.
        private CastleSpatialPlan _pendingCastleSpatialPlan;
        private CastleSpatialProjection _castleSpatialProjection;
    }
}
