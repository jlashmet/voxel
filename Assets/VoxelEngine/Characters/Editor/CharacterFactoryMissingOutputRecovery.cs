using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VoxelEngine.Characters.Editor
{
    /// <summary>
    /// Re-triggers Character Factory descriptors when their derived Unity assets were removed
    /// outside AssetDatabase while the Library cache was retained (for example after git clean
    /// or a branch switch). Generated prefabs/catalogues remain untracked; the portable
    /// descriptor + FBX pair stays the source of truth.
    /// </summary>
    [InitializeOnLoad]
    internal static class CharacterFactoryMissingOutputRecovery
    {
        private const string DescriptorSuffix = ".characterfactory.json";

        static CharacterFactoryMissingOutputRecovery()
        {
            EditorApplication.delayCall += RecoverMissingOutputs;
        }

        private static void RecoverMissingOutputs()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += RecoverMissingOutputs;
                return;
            }

            string assetsRoot = Application.dataPath;
            if (!Directory.Exists(assetsRoot))
            {
                return;
            }

            string[] descriptorFiles = Directory.GetFiles(
                assetsRoot,
                "*" + DescriptorSuffix,
                SearchOption.AllDirectories
            );

            int reimported = 0;
            for (int i = 0; i < descriptorFiles.Length; i++)
            {
                string descriptorAssetPath = ToAssetPath(descriptorFiles[i], assetsRoot);
                try
                {
                    if (!NeedsRecovery(descriptorAssetPath, descriptorFiles[i]))
                    {
                        continue;
                    }

                    AssetDatabase.ImportAsset(
                        descriptorAssetPath,
                        ImportAssetOptions.ForceUpdate
                    );
                    reimported++;
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        $"Character Factory recovery failed for '{descriptorAssetPath}': " +
                        $"{exception.Message}\n{exception}"
                    );
                }
            }

            if (reimported > 0)
            {
                Debug.Log(
                    $"Character Factory recovery reimported {reimported} descriptor(s) " +
                    "with missing generated outputs."
                );
            }
        }

        private static bool NeedsRecovery(string descriptorAssetPath, string descriptorFile)
        {
            RecoveryDescriptor descriptor = JsonUtility.FromJson<RecoveryDescriptor>(
                File.ReadAllText(descriptorFile)
            );
            if (descriptor == null || descriptor.schemaVersion != 1 ||
                string.IsNullOrWhiteSpace(descriptor.id))
            {
                return false;
            }

            string descriptorDirectory = NormalizeAssetPath(
                Path.GetDirectoryName(descriptorAssetPath) ?? "Assets"
            );
            string fbxAssetPath = NormalizeAssetPath(
                Path.Combine(descriptorDirectory, descriptor.fbx ?? string.Empty)
            );

            // Initial project import is already handled by CharacterFactoryAssetImporter.
            // Recovery is only useful once the source model itself is available in AssetDatabase.
            if (AssetDatabase.LoadAssetAtPath<GameObject>(fbxAssetPath) == null)
            {
                return false;
            }

            string assetType = (descriptor.assetType ?? string.Empty).Trim().ToLowerInvariant();
            string generatedAssetPath = NormalizeAssetPath(
                Path.Combine(
                    descriptorDirectory,
                    descriptor.id + (assetType == "character" ? ".prefab" : ".asset")
                )
            );

            if (!AssetFileExists(generatedAssetPath))
            {
                return true;
            }

            string cataloguePath = NormalizeAssetPath(descriptor.catalogueAsset ?? string.Empty);
            return !string.IsNullOrWhiteSpace(cataloguePath) &&
                !AssetFileExists(cataloguePath);
        }

        private static bool AssetFileExists(string assetPath)
        {
            if (!assetPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                return false;
            }

            return File.Exists(Path.GetFullPath(assetPath));
        }

        private static string ToAssetPath(string fullPath, string assetsRoot)
        {
            string relative = fullPath.Substring(assetsRoot.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return "Assets/" + NormalizeAssetPath(relative);
        }

        private static string NormalizeAssetPath(string path)
        {
            return path.Replace('\\', '/');
        }

        [Serializable]
        private sealed class RecoveryDescriptor
        {
            public int schemaVersion;
            public string id;
            public string assetType;
            public string fbx;
            public string catalogueAsset;
        }
    }
}
