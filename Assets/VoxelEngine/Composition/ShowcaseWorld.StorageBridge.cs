using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Application-level logical storage bridge for Showcase collaborators. This partial keeps
    /// networking and presentation callers on subsystem API vocabulary while concrete storage
    /// ownership remains inside the Composition assembly.
    /// </summary>
    public sealed partial class ShowcaseWorld
    {
        public bool IsRegionDirty(int3 regionCoord) =>
            _table.TryGetRegion(regionCoord, out var region) && region.Dirty;

        public void PublishRegionChange(int3 regionCoord, VoxelChangeKind kind) =>
            _changes.PublishRegion(regionCoord, kind);
    }
}
