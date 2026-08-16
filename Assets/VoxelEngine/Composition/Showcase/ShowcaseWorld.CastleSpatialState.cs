using VoxelEngine.Structures.Api;

namespace VoxelEngine.Showcase
{
    public sealed partial class ShowcaseWorld
    {
        // Keep the runtime-ready planning bundle intact from dependency calculation through build
        // admission. The compact projection is retained after commit for interaction/presentation
        // so those systems follow the exact geometry that Runtime realized.
        private PlannedCastleBuild _pendingPlannedCastle;
        private PlannedCastleBuild _plannedCastle;
        private CastleSpatialProjection _castleSpatialProjection;
    }
}
