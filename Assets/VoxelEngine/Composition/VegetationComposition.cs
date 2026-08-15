using VoxelEngine.Vegetation.Api;
using VoxelEngine.Vegetation.Runtime;

namespace VoxelEngine.Composition
{
    /// <summary>Production wiring for Vegetation runtime capabilities.</summary>
    public static class VegetationComposition
    {
        private static readonly ITreeDamageService s_treeDamage = new TreeDamageService();

        public static ITreeDamageService TreeDamage => s_treeDamage;
    }
}
