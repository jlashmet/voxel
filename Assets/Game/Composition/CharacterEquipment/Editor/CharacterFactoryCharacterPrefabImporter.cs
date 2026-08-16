using System;
using System.Collections.Generic;
using System.IO;
using MountingForce.Game.Composition.CharacterEquipment;
using UnityEditor;
using UnityEngine;

namespace MountingForce.Game.Composition.CharacterEquipment.Editor
{
    /// <summary>
    /// Materializes staged Character Factory body descriptors as ready-to-use modular character
    /// prefabs. Equipment descriptors remain owned by CharacterFactoryAssetImporter; this bridge
    /// only handles assetType=character and wires the shared part catalogue into the runtime
    /// equipment controller.
    /// </summary>
    internal sealed class CharacterFactoryCharacterPrefabImporter : AssetPostprocessor
    {
        private const string DescriptorSuffix = ".characterfactory.json";
        private const string GeneratedRoot = "Assets/Generated/CharacterFactory";
        private const string CanonicalArmatureName = "Armature";
        private const string EquipmentRootName = "Equipment";

        private static bool refreshScheduled;
        private static bool refreshInProgress;

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (refreshInProgress)
            {
                return;
            }

            if (!ContainsRelevantPath(importedAssets) &&
                !ContainsRelevantPath(deletedAssets) &&
                !ContainsRelevantPath(movedAssets) &&
                !ContainsRelevantPath(movedFromAssetPaths))
            {
                return;
            }

            ScheduleRefresh();
        }

        [MenuItem("Mounting Force/Characters/Refresh Generated Character Prefabs")]
        private static void RefreshFromMenu()
        {
            RefreshAll();
        }

        private static void ScheduleRefresh()
        {
            if (refreshScheduled)
            {
                return;
            }

            refreshScheduled = true;
            EditorApplication.delayCall += () =>
            {
                refreshScheduled = false;
                RefreshAll();
            };
        }

        private static void RefreshAll()
        {
            if (refreshInProgress)
            {
                return;
            }

            refreshInProgress = true;
            try
            {
                List<CharacterDescriptorRecord> descriptors = DiscoverCharacterDescriptors();
                for (int i = 0; i < descriptors.Count; i++)
                {
                    CharacterDescriptorRecord record = descriptors[i];
                    try
                    {
                        MaterializeCharacterPrefab(record);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogError(
                            $"Character Factory character prefab import failed for '{record.AssetPath}': " +
                            exception.Message);
                    }
                }

                AssetDatabase.SaveAssets();
            }
            finally
            {
                refreshInProgress = false;
            }
        }

        private static List<CharacterDescriptorRecord> DiscoverCharacterDescriptors()
        {
            var records = new List<CharacterDescriptorRecord>();
            string[] files = Directory.GetFiles(
                Application.dataPath,
                "*" + DescriptorSuffix,
                SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.Ordinal);

            for (int i = 0; i < files.Length; i++)
            {
                string assetPath = ToAssetPath(files[i]);
                if (string.IsNullOrEmpty(assetPath))
                {
                    continue;
                }

                try
                {
                    CharacterDescriptor descriptor = JsonUtility.FromJson<CharacterDescriptor>(
                        File.ReadAllText(files[i]));
                    if (descriptor == null ||
                        descriptor.schemaVersion != 1 ||
                        !string.Equals(
                            descriptor.assetType?.Trim(),
                            "character",
                            StringComparison.OrdinalIgnoreCase) ||
                        string.IsNullOrWhiteSpace(descriptor.id) ||
                        string.IsNullOrWhiteSpace(descriptor.fbx) ||
                        string.IsNullOrWhiteSpace(descriptor.catalogueAsset))
                    {
                        continue;
                    }

                    records.Add(new CharacterDescriptorRecord(assetPath, descriptor));
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        $"Character Factory character descriptor could not be read: {assetPath}: " +
                        exception.Message);
                }
            }

            return records;
        }

        private static void MaterializeCharacterPrefab(CharacterDescriptorRecord record)
        {
            CharacterDescriptor descriptor = record.Descriptor;
            if (!TryResolveSiblingAssetPath(record.AssetPath, descriptor.fbx, out string fbxPath) ||
                !fbxPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Character descriptor FBX path is invalid.");
            }

            GameObject generatedModel = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (generatedModel == null)
            {
                // A descriptor and its FBX can be reported in the same import batch before the model
                // importer has produced the GameObject. The FBX import callback will schedule another
                // refresh, so leave this descriptor for that pass rather than creating a broken prefab.
                return;
            }

            if (!TryNormalizeCataloguePath(descriptor.catalogueAsset, out string cataloguePath))
            {
                throw new InvalidOperationException(
                    "Character descriptor catalogueAsset must be a safe Assets/... .asset path.");
            }

            CharacterPartCatalogue catalogue = LoadOrCreateCatalogue(cataloguePath);
            string descriptorDirectory = Path.GetDirectoryName(record.AssetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(descriptorDirectory))
            {
                throw new InvalidOperationException("Character descriptor has no Unity asset directory.");
            }

            string prefabPath = descriptorDirectory + "/" + descriptor.id.Trim() + ".prefab";
            CreateOrUpdateCharacterPrefab(
                descriptor.id.Trim(),
                generatedModel,
                catalogue,
                prefabPath);
        }

        private static void CreateOrUpdateCharacterPrefab(
            string characterId,
            GameObject generatedModel,
            CharacterPartCatalogue catalogue,
            string prefabAssetPath)
        {
            if (string.IsNullOrWhiteSpace(characterId))
            {
                throw new ArgumentException("Character id is required.", nameof(characterId));
            }

            if (generatedModel == null)
            {
                throw new ArgumentNullException(nameof(generatedModel));
            }

            if (catalogue == null)
            {
                throw new ArgumentNullException(nameof(catalogue));
            }

            if (string.IsNullOrWhiteSpace(prefabAssetPath) ||
                !prefabAssetPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                !prefabAssetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "Character prefab path must be an Assets/... .prefab path.",
                    nameof(prefabAssetPath));
            }

            GameObject prefabRoot = new GameObject(characterId);
            try
            {
                GameObject modelInstance = PrefabUtility.InstantiatePrefab(generatedModel) as GameObject;
                if (modelInstance == null)
                {
                    throw new InvalidOperationException(
                        $"Could not instantiate generated character model '{generatedModel.name}'.");
                }

                modelInstance.transform.SetParent(prefabRoot.transform, false);
                modelInstance.name = "Model";

                Transform armature = FindUniqueDescendant(modelInstance.transform, CanonicalArmatureName);
                if (armature == null)
                {
                    throw new InvalidOperationException(
                        $"Generated character '{characterId}' must contain exactly one " +
                        $"'{CanonicalArmatureName}' skeleton root.");
                }

                GameObject equipmentRoot = new GameObject(EquipmentRootName);
                equipmentRoot.transform.SetParent(prefabRoot.transform, false);

                ModularCharacterAssembler assembler =
                    equipmentRoot.AddComponent<ModularCharacterAssembler>();
                assembler.SkeletonRoot = armature;

                CharacterEquipmentController controller =
                    equipmentRoot.AddComponent<CharacterEquipmentController>();
                controller.Configure(catalogue, assembler);

                string folder = Path.GetDirectoryName(prefabAssetPath)?.Replace('\\', '/');
                EnsureFolder(folder);
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabAssetPath);
                if (saved == null)
                {
                    throw new InvalidOperationException(
                        $"Unity did not save generated character prefab '{prefabAssetPath}'.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(prefabRoot);
            }
        }

        private static Transform FindUniqueDescendant(Transform root, string transformName)
        {
            if (root == null || string.IsNullOrWhiteSpace(transformName))
            {
                return null;
            }

            Transform match = null;
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (!string.Equals(candidate.name, transformName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (match != null)
                {
                    return null;
                }

                match = candidate;
            }

            return match;
        }

        private static CharacterPartCatalogue LoadOrCreateCatalogue(string cataloguePath)
        {
            CharacterPartCatalogue catalogue =
                AssetDatabase.LoadAssetAtPath<CharacterPartCatalogue>(cataloguePath);
            if (catalogue != null)
            {
                return catalogue;
            }

            EnsureFolder(Path.GetDirectoryName(cataloguePath)?.Replace('\\', '/'));
            catalogue = ScriptableObject.CreateInstance<CharacterPartCatalogue>();
            catalogue.name = Path.GetFileNameWithoutExtension(cataloguePath);
            AssetDatabase.CreateAsset(catalogue, cataloguePath);
            return catalogue;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string[] parts = folderPath.Split('/');
            if (parts.Length == 0 || !string.Equals(parts[0], "Assets", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Generated character prefab folder must be under Assets/: " + folderPath);
            }

            string current = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static bool ContainsRelevantPath(string[] paths)
        {
            if (paths == null)
            {
                return false;
            }

            for (int i = 0; i < paths.Length; i++)
            {
                string path = paths[i];
                if (path.EndsWith(DescriptorSuffix, StringComparison.OrdinalIgnoreCase) ||
                    (path.StartsWith(GeneratedRoot + "/", StringComparison.Ordinal) &&
                     path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryResolveSiblingAssetPath(
            string descriptorAssetPath,
            string relativeOrAssetPath,
            out string assetPath)
        {
            assetPath = string.Empty;
            if (string.IsNullOrWhiteSpace(relativeOrAssetPath))
            {
                return false;
            }

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string source = relativeOrAssetPath.Replace('\\', '/');
            string absolute;
            if (source.StartsWith("Assets/", StringComparison.Ordinal))
            {
                absolute = Path.GetFullPath(Path.Combine(projectRoot, source));
            }
            else
            {
                string descriptorAbsolute = Path.Combine(projectRoot, descriptorAssetPath);
                string descriptorDirectory = Path.GetDirectoryName(descriptorAbsolute);
                absolute = Path.GetFullPath(Path.Combine(descriptorDirectory ?? projectRoot, source));
            }

            assetPath = ToAssetPath(absolute);
            return !string.IsNullOrEmpty(assetPath);
        }

        private static bool TryNormalizeCataloguePath(string rawPath, out string cataloguePath)
        {
            cataloguePath = (rawPath ?? string.Empty).Replace('\\', '/').Trim();
            return cataloguePath.StartsWith("Assets/", StringComparison.Ordinal) &&
                   cataloguePath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) &&
                   !cataloguePath.Contains("../", StringComparison.Ordinal);
        }

        private static string ToAssetPath(string absolutePath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string normalized = Path.GetFullPath(absolutePath);
            string prefix = projectRoot.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            string relative = normalized.Substring(prefix.Length).Replace('\\', '/');
            return relative.StartsWith("Assets/", StringComparison.Ordinal) ? relative : string.Empty;
        }

        private sealed class CharacterDescriptorRecord
        {
            public CharacterDescriptorRecord(string assetPath, CharacterDescriptor descriptor)
            {
                AssetPath = assetPath;
                Descriptor = descriptor;
            }

            public string AssetPath { get; }
            public CharacterDescriptor Descriptor { get; }
        }

        [Serializable]
        private sealed class CharacterDescriptor
        {
            public int schemaVersion;
            public string id;
            public string assetType;
            public string fbx;
            public string catalogueAsset;
        }
    }
}
