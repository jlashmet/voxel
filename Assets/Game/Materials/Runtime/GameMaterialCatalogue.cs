using System;
using Game.Materials.Api;

namespace Game.Materials.Runtime
{
    /// <summary>
    /// Canonical game-owned semantic catalogue for stable material identity, display names and
    /// game-facing selections. Physical/simulation behavior is a separate projection in
    /// <see cref="GameMaterialSimulationDefinitions"/>; rendering remains separately owned too.
    /// </summary>
    public static class GameMaterialCatalogue
    {
        public const int Count = 22;

        private static readonly byte[] s_BuildableMaterials =
        {
            GameMaterialIds.Stone,
            GameMaterialIds.Wood,
            GameMaterialIds.Sand,
            GameMaterialIds.Glass,
        };

        private static readonly string[] s_Names =
        {
            "empty", "stone", "wood", "sand", "glass", "bedrock",
            "dark stone", "slate", "tile", "cloth", "grass", "water",
            "gold", "dirt", "moss", "lit window", "cascade", "crystal",
            "small masonry", "medium masonry", "large masonry", "white flower",
        };

        public static int BuildableCount => s_BuildableMaterials.Length;

        public static bool IsCanonicalId(byte materialId) => materialId < Count;

        public static string NameOf(byte materialId) =>
            IsCanonicalId(materialId) ? s_Names[materialId] : "unknown";

        public static byte BuildableAt(int index)
        {
            if ((uint)index >= (uint)s_BuildableMaterials.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return s_BuildableMaterials[index];
        }
    }
}
