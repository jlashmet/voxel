using VoxelEngine.Rendering.Api;
using VoxelEngine.Rendering.Runtime;

namespace VoxelEngine.Composition
{
    /// <summary>
    /// Composition boundary for application-owned material presentation.
    /// Rendering.Runtime remains hidden from game/content assemblies.
    /// </summary>
    public static class MaterialPresentationComposition
    {
        public static void Apply(MaterialPresentationDefinition[] definitions) =>
            VoxelMaterialPresentationInstaller.Apply(definitions);
    }
}
