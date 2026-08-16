using VoxelEngine.Terrain.Api;

namespace VoxelEngine.Composition
{
    /// <summary>
    /// Application-to-Terrain handoff for legacy terrain callers. New code should pass a
    /// TerrainMaterialSet explicitly; this bridge exists only while old entry points are migrated.
    /// </summary>
    public static class TerrainMaterialComposition
    {
        public static void Configure(in TerrainMaterialSet materials) =>
            TerrainMaterialCompatibility.Configure(in materials);

        public static bool IsConfigured => TerrainMaterialCompatibility.IsConfigured;

        public static void Reset() => TerrainMaterialCompatibility.Reset();
    }
}
