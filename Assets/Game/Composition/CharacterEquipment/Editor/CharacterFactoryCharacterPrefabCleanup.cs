using System;
using UnityEditor;

namespace MountingForce.Game.Composition.CharacterEquipment.Editor
{
    /// <summary>
    /// Removes generated runtime character prefabs when their staged Character Factory descriptor
    /// is deleted or moved away. This keeps generated Unity assets idempotent instead of leaving a
    /// stale playable prefab behind after the source character is removed.
    /// </summary>
    internal sealed class CharacterFactoryCharacterPrefabCleanup : AssetPostprocessor
    {
        private const string DescriptorSuffix = ".characterfactory.json";
        private const string GeneratedRoot = "Assets/Generated/CharacterFactory";

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            RemoveStaleRuntimePrefabs(deletedAssets);
            RemoveStaleRuntimePrefabs(movedFromAssetPaths);
        }

        private static void RemoveStaleRuntimePrefabs(string[] paths)
        {
            if (paths == null)
            {
                return;
            }

            for (int i = 0; i < paths.Length; i++)
            {
                string path = paths[i];
                if (string.IsNullOrWhiteSpace(path) ||
                    !path.StartsWith(GeneratedRoot + "/", StringComparison.Ordinal) ||
                    !path.EndsWith(DescriptorSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string prefabPath = path.Substring(0, path.Length - DescriptorSuffix.Length) + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(prefabPath) != null)
                {
                    AssetDatabase.DeleteAsset(prefabPath);
                }
            }
        }
    }
}
