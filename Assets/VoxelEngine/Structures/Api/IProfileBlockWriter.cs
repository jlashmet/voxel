using VoxelEngine.Storage.Api;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Narrow authoring sink for retained sub-voxel profile detail emitted by structure features.
    /// The mutable store remains a Structures.Runtime implementation; consumers that only render
    /// retained profiles use Storage.Api.IProfileBlockReadSource instead.
    /// </summary>
    public interface IProfileBlockWriter
    {
        void Add(in ProfileBlock block);
    }
}
