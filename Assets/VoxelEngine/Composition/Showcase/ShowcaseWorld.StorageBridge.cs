using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Application-level logical storage bridge for Showcase collaborators. This partial keeps
    /// networking/presentation adapters on Storage.Api vocabulary while the main ShowcaseWorld
    /// borrows the single Composition-owned storage lifetime used by the world hot paths.
    /// </summary>
    public sealed partial class ShowcaseWorld
    {
        public bool IsRegionDirty(int3 regionCoord) =>
            _table.TryGetRegion(regionCoord, out var region) && region.Dirty;

        public void PublishRegionChange(int3 regionCoord, VoxelChangeKind kind) =>
            _changes.PublishRegion(regionCoord, kind);
    }
}
