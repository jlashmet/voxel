namespace VoxelEngine.Storage.Api
{
    /// <summary>
    /// Read-only material registration/coating rules used to validate authored voxel content
    /// without exposing Storage's mutable palette implementation.
    /// </summary>
    public interface IMaterialAuthoringCatalogue
    {
        bool IsRegistered(byte materialId);
        bool AllowsCoating(byte materialId, byte coatingId);
    }

    /// <summary>
    /// Read-only surface-style registration used by structure/content validation.
    /// </summary>
    public interface ISurfaceStyleAuthoringCatalogue
    {
        bool IsRegistered(ushort styleId);
    }

    /// <summary>
    /// Read-only coating registration/compatibility used by structure/content validation.
    /// </summary>
    public interface ICoatingAuthoringCatalogue
    {
        bool IsRegistered(byte coatingId);
        bool Allows(byte coatingId, byte materialId);
    }
}
