namespace VoxelEngine.Characters.Api
{
    /// <summary>
    /// API-level failure reasons. Runtime implementation details remain behind Characters.Runtime.
    /// </summary>
    public enum CharacterEquipmentFailure : byte
    {
        None = 0,
        PartIdRequired = 1,
        CatalogueUnavailable = 2,
        PartNotFound = 3,
        PrefabMissing = 4,
        SkeletonUnavailable = 5,
        NoSkinnedMesh = 6,
        BoneNotFound = 7,
        AmbiguousBoneName = 8,
        SocketNotFound = 9
    }
}
