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
    /// Optional authoring-time placement behavior for a material row. A non-zero placement coating
    /// means that placing this material onto an existing solid should preserve the base material and
    /// apply the returned coating instead. Placement surface style controls newly authored cells.
    /// The engine interprets both properties generically; the game decides their values.
    /// </summary>
    public interface IMaterialPlacementCatalogue
    {
        ushort GetPlacementSurfaceStyle(byte materialId);
        byte GetPlacementCoating(byte materialId);
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
