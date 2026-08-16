using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MountingForce.Game.Composition.CharacterEquipment;
using UnityEditor;
using UnityEngine;

namespace MountingForce.Game.Composition.CharacterEquipment.Editor
{
    internal sealed class CharacterFactoryAssetImporter : AssetPostprocessor
    {
        private const string DescriptorSuffix = ".characterfactory.json";
        private const string GeneratedRoot = "Assets/Generated/CharacterFactory";

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

        [MenuItem("Mounting Force/Characters/Refresh Generated Part Catalogue")]
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
                List<DescriptorRecord> descriptors = DiscoverDescriptors();
                ConfigureModelImporters(descriptors);
                RebuildCatalogues(descriptors);
            }
            finally
            {
                refreshInProgress = false;
            }
        }

        private static List<DescriptorRecord> DiscoverDescriptors()
        {
            var records = new List<DescriptorRecord>();
            string[] files = Directory.GetFiles(
                Application.dataPath,
                "*" + DescriptorSuffix,
                SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.Ordinal);

            for (int i = 0; i < files.Length; i++)
            {
                string descriptorAssetPath = ToAssetPath(files[i]);
                if (string.IsNullOrEmpty(descriptorAssetPath))
                {
                    continue;
                }

                try
                {
                    CharacterFactoryDescriptor descriptor = JsonUtility.FromJson<CharacterFactoryDescriptor>(
                        File.ReadAllText(files[i]));
                    if (!TryValidateDescriptor(descriptorAssetPath, descriptor, out string error))
                    {
                        Debug.LogError($"Character Factory descriptor ignored: {descriptorAssetPath}: {error}");
                        continue;
                    }

                    records.Add(new DescriptorRecord(descriptorAssetPath, descriptor));
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        $"Character Factory descriptor could not be read: {descriptorAssetPath}: " +
                        exception.Message);
                }
            }

            return records;
        }

        private static bool TryValidateDescriptor(
            string descriptorAssetPath,
            CharacterFactoryDescriptor descriptor,
            out string error)
        {
            error = string.Empty;
            if (descriptor == null)
            {
                error = "JSON payload is empty";
                return false;
            }

            if (descriptor.schemaVersion != 1)
            {
                error = $"unsupported schemaVersion {descriptor.schemaVersion}";
                return false;
            }

            if (string.IsNullOrWhiteSpace(descriptor.id))
            {
                error = "id is required";
                return false;
            }

            if (string.IsNullOrWhiteSpace(descriptor.assetType))
            {
                error = "assetType is required";
                return false;
            }

            if (string.IsNullOrWhiteSpace(descriptor.fbx) ||
                !TryResolveSiblingAssetPath(descriptorAssetPath, descriptor.fbx, out string fbxPath) ||
                !fbxPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            {
                error = "fbx must resolve to an FBX inside the Unity project";
                return false;
            }

            if (string.IsNullOrWhiteSpace(descriptor.catalogueAsset) ||
                !TryNormalizeCataloguePath(descriptor.catalogueAsset, out _))
            {
                error = "catalogueAsset must be an Assets/... .asset path";
                return false;
            }

            return true;
        }

        private static void ConfigureModelImporters(List<DescriptorRecord> descriptors)
        {
            for (int i = 0; i < descriptors.Count; i++)
            {
                DescriptorRecord record = descriptors[i];
                if (!TryResolveSiblingAssetPath(record.AssetPath, record.Descriptor.fbx, out string fbxPath))
                {
                    continue;
                }

                ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
                if (importer == null)
                {
                    continue;
                }

                string assetType = record.Descriptor.assetType.Trim().ToLowerInvariant();
                ModelImporterAnimationType desiredAnimationType;
                bool desiredImportAnimation;
                switch (assetType)
                {
                    case "character":
                        desiredAnimationType = ModelImporterAnimationType.Generic;
                        desiredImportAnimation = true;
                        break;
                    case "clothing":
                        desiredAnimationType = ModelImporterAnimationType.Generic;
                        desiredImportAnimation = false;
                        break;
                    case "weapon":
                    case "accessory":
                        desiredAnimationType = ModelImporterAnimationType.None;
                        desiredImportAnimation = false;
                        break;
                    default:
                        continue;
                }

                bool changed = false;
                if (importer.animationType != desiredAnimationType)
                {
                    importer.animationType = desiredAnimationType;
                    changed = true;
                }

                if (importer.importAnimation != desiredImportAnimation)
                {
                    importer.importAnimation = desiredImportAnimation;
                    changed = true;
                }

                if (importer.importCameras)
                {
                    importer.importCameras = false;
                    changed = true;
                }

                if (importer.importLights)
                {
                    importer.importLights = false;
                    changed = true;
                }

                if (changed)
                {
                    importer.SaveAndReimport();
                }
            }
        }

        private static void RebuildCatalogues(List<DescriptorRecord> descriptors)
        {
            var byCatalogue = new Dictionary<string, List<DescriptorRecord>>(StringComparer.Ordinal);
            for (int i = 0; i < descriptors.Count; i++)
            {
                DescriptorRecord record = descriptors[i];
                CharacterFactoryDescriptor descriptor = record.Descriptor;
                if (descriptor.runtimePart == null)
                {
                    continue;
                }

                string assetType = descriptor.assetType.Trim().ToLowerInvariant();
                if (assetType != "clothing" && assetType != "weapon")
                {
                    continue;
                }

                if (!TryNormalizeCataloguePath(descriptor.catalogueAsset, out string cataloguePath))
                {
                    continue;
                }

                if (!byCatalogue.TryGetValue(cataloguePath, out List<DescriptorRecord> records))
                {
                    records = new List<DescriptorRecord>();
                    byCatalogue.Add(cataloguePath, records);
                }

                records.Add(record);
            }

            ClearGeneratedCataloguesNotRepresented(byCatalogue);

            foreach (KeyValuePair<string, List<DescriptorRecord>> pair in byCatalogue)
            {
                string cataloguePath = pair.Key;
                List<DescriptorRecord> records = pair.Value;
                records.Sort((left, right) => string.CompareOrdinal(left.AssetPath, right.AssetPath));

                var definitions = new List<CharacterPartDefinition>();
                var ids = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < records.Count; i++)
                {
                    DescriptorRecord record = records[i];
                    if (!ids.Add(record.Descriptor.id))
                    {
                        Debug.LogWarning(
                            $"Duplicate Character Factory part id '{record.Descriptor.id}' ignored: " +
                            record.AssetPath);
                        continue;
                    }

                    if (TryBuildDefinition(record, out CharacterPartDefinition definition))
                    {
                        definitions.Add(definition);
                    }
                }

                definitions.Sort((left, right) => string.CompareOrdinal(left.PartId, right.PartId));
                CharacterPartCatalogue catalogue = LoadOrCreateCatalogue(cataloguePath);
                catalogue.Configure(definitions.ToArray());
                EditorUtility.SetDirty(catalogue);
            }

            AssetDatabase.SaveAssets();
        }

        private static bool TryBuildDefinition(
            DescriptorRecord record,
            out CharacterPartDefinition definition)
        {
            definition = null;
            CharacterFactoryDescriptor descriptor = record.Descriptor;
            CharacterFactoryRuntimePart runtimePart = descriptor.runtimePart;
            if (runtimePart == null || string.IsNullOrWhiteSpace(runtimePart.slot))
            {
                Debug.LogError($"Character Factory runtimePart.slot is required: {record.AssetPath}");
                return false;
            }

            if (!TryResolveSiblingAssetPath(record.AssetPath, descriptor.fbx, out string fbxPath))
            {
                Debug.LogError($"Character Factory FBX path is invalid: {record.AssetPath}");
                return false;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (prefab == null)
            {
                Debug.LogError($"Character Factory FBX has no importable GameObject: {fbxPath}");
                return false;
            }

            string assetType = descriptor.assetType.Trim().ToLowerInvariant();
            CharacterPartKind partKind;
            CharacterPartMountMode mountMode;
            string socket;
            if (assetType == "clothing")
            {
                partKind = CharacterPartKind.Clothing;
                mountMode = CharacterPartMountMode.RebindSkeleton;
                socket = string.Empty;
            }
            else if (assetType == "weapon")
            {
                if (string.IsNullOrWhiteSpace(runtimePart.socketBoneName))
                {
                    Debug.LogError($"Character Factory weapon socketBoneName is required: {record.AssetPath}");
                    return false;
                }

                partKind = CharacterPartKind.Weapon;
                mountMode = CharacterPartMountMode.Socket;
                socket = runtimePart.socketBoneName.Trim();
            }
            else
            {
                return false;
            }

            definition = new CharacterPartDefinition(
                descriptor.id.Trim(),
                runtimePart.slot.Trim(),
                partKind,
                mountMode,
                prefab,
                socket,
                ReadVector3(runtimePart.socketLocalPosition, Vector3.zero),
                ReadVector3(runtimePart.socketLocalEulerAngles, Vector3.zero),
                ReadVector3(runtimePart.socketLocalScale, Vector3.one));
            return true;
        }

        private static CharacterPartCatalogue LoadOrCreateCatalogue(string cataloguePath)
        {
            CharacterPartCatalogue catalogue = AssetDatabase.LoadAssetAtPath<CharacterPartCatalogue>(cataloguePath);
            if (catalogue != null)
            {
                return catalogue;
            }

            EnsureFolder(Path.GetDirectoryName(cataloguePath)?.Replace('\\', '/'));
            catalogue = ScriptableObject.CreateInstance<CharacterPartCatalogue>();
            AssetDatabase.CreateAsset(catalogue, cataloguePath);
            return catalogue;
        }

        private static void ClearGeneratedCataloguesNotRepresented(
            Dictionary<string, List<DescriptorRecord>> represented)
        {
            if (!AssetDatabase.IsValidFolder(GeneratedRoot))
            {
                return;
            }

            string[] guids = AssetDatabase.FindAssets(
                "t:CharacterPartCatalogue",
                new[] { GeneratedRoot });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (represented.ContainsKey(path))
                {
                    continue;
                }

                CharacterPartCatalogue catalogue = AssetDatabase.LoadAssetAtPath<CharacterPartCatalogue>(path);
                if (catalogue == null)
                {
                    continue;
                }

                catalogue.Configure();
                EditorUtility.SetDirty(catalogue);
            }
        }

        private static void EnsureFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string[] parts = folderPath.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
            {
                throw new InvalidOperationException("Catalogue folder must be under Assets: " + folderPath);
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
            string prefix = projectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                            Path.DirectorySeparatorChar;
            if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            string relative = normalized.Substring(prefix.Length).Replace('\\', '/');
            return relative.StartsWith("Assets/", StringComparison.Ordinal) ? relative : string.Empty;
        }

        private static Vector3 ReadVector3(float[] values, Vector3 fallback)
        {
            if (values == null || values.Length != 3)
            {
                return fallback;
            }

            return new Vector3(values[0], values[1], values[2]);
        }

        private sealed class DescriptorRecord
        {
            public DescriptorRecord(string assetPath, CharacterFactoryDescriptor descriptor)
            {
                AssetPath = assetPath;
                Descriptor = descriptor;
            }

            public string AssetPath { get; }
            public CharacterFactoryDescriptor Descriptor { get; }
        }

        [Serializable]
        private sealed class CharacterFactoryDescriptor
        {
            public int schemaVersion;
            public string id;
            public string assetType;
            public string fbx;
            public string catalogueAsset;
            public CharacterFactoryRuntimePart runtimePart;
        }

        [Serializable]
        private sealed class CharacterFactoryRuntimePart
        {
            public string slot;
            public string socketBoneName;
            public float[] socketLocalPosition;
            public float[] socketLocalEulerAngles;
            public float[] socketLocalScale;
        }
    }
}
