using VoxelEngine.Structures.Api;

namespace VoxelEngine.Composition
{
    /// <summary>
    /// Application-to-Structures material-role handoff. Semantic material identity stays outside
    /// VoxelEngine; legacy structure generators resolve their temporary Mat facade through this
    /// configured purpose-based role set.
    /// </summary>
    public static class StructureMaterialComposition
    {
        public static void Configure(in StructureMaterialSet roles) =>
            Mat.ConfigureCompatibility(in roles);

        public static bool IsConfigured => Mat.IsConfigured;

        public static void Reset() => Mat.ResetCompatibility();
    }
}
