using VoxelEngine.Structures.Runtime.MeshImport;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Single composition entry point for the source-specific mountain-dragon bake policy.
    /// It binds the unmaterialed source to the game palette without leaking game material identity
    /// into the reusable mesh voxelizer.
    /// </summary>
    public static class MountainDragonAuthoringPolicy
    {
        public static MeshVoxelizationSettings CreateVoxelizationSettings() =>
            MountainDragonVoxelBakePolicy.CreateSettings(MountainDragonPalettePolicy.DragonMaterial);
    }
}
