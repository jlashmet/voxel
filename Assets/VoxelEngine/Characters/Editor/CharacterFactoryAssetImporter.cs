using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using VoxelEngine.Characters.Api;
using VoxelEngine.Characters.Runtime;

namespace VoxelEngine.Characters.Editor
{
    /// <summary>
    /// Turns portable *.characterfactory.json descriptors emitted by the offline factory into
    /// CharacterPartAsset entries. The descriptor is staged beside its generated FBX, so no browser
    /// or manual inspector setup is required after the build is copied into Assets/.
    /// </summary>
    internal sealed class CharacterFactoryAssetImporter : AssetPostprocessor
    {
        private const string DescriptorSuffix = ".characterfactory.json";
        private static readonly HashSet<string> PendingDescriptors =
            new HashSet<string>(StringComparer.Ordinal);

        private static bool processScheduled;

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            for (int i = 0; i < importedAssets.Length; i++)
            {
                string path = importedAssets[i];
                if (path.EndsWith(DescriptorSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    PendingDescriptors.Add(path);
                }
            }

            if (PendingDescriptors.Count == 0 || processScheduled)
            {
                return;
            }

            processScheduled = true;
            EditorApplication.delayCall += ProcessPendingDescriptors;
        }

        private static void ProcessPendingDescriptors()
        {
            processScheduled = false;
            if (PendingDescriptors.Count == 0)
            {
                return;
            }

            string[] paths = new string[PendingDescriptors.Count];
            PendingDescriptors.CopyTo(paths);
            PendingDescriptors.Clear();

            for (int i = 0; i < paths.Length; i++)
            {
                try
                {
                    ImportDescriptor(paths[i]);
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        $"Character Factory import failed for '{paths[i]}': {exception.Message}\n{exception}"
                    );
                }
            }

            AssetDatabase.SaveAssets();
        }

        private static void ImportDescriptor(string descriptorAssetPath)
        {
            CharacterFactoryImportDescriptor descriptor = ReadDescriptor(descriptorAssetPath);
            if (descriptor.schemaVersion != 1)
            {
                throw new InvalidOperationException(
                    $"Unsupported Character Factory descriptor schema {descriptor.schemaVersion}."
                );
            }

            if (string.IsNullOrWhiteSpace(descriptor.id))
            {
                throw new InvalidOperationException("Descriptor id is required.");
            }

            string descriptorDirectory = NormalizeAssetPath(
                Path.GetDirectoryName(descriptorAssetPath) ?? "Assets"
            );
            string fbxAssetPath = NormalizeAssetPath(
                Path.Combine(descriptorDirectory, descriptor.fbx ?? string.Empty)
            );
            if (!fbxAssetPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Generated FBX must be inside Assets/: {fbxAssetPath}"
                );
            }

            GameObject generatedModel = AssetDatabase.LoadAssetAtPath<GameObject>(fbxAssetPath);
            if (generatedModel == null)
            {
                throw new InvalidOperationException(
                    $"Generated FBX has not imported as a GameObject: {fbxAssetPath}"
                );
            }

            string assetType = (descriptor.assetType ?? string.Empty).Trim().ToLowerInvariant();
            if (assetType == "character")
            {
                // Character bodies are not equipment. Importing the FBX into Assets is sufficient
                // for now; body/prefab construction is a distinct pipeline contract.
                Debug.Log(
                    $"Character Factory imported character body '{descriptor.id}' from {fbxAssetPath}."
                );
                return;
            }

            if (descriptor.runtimePart == null)
            {
                throw new InvalidOperationException(
                    $"Equippable '{descriptor.id}' is missing runtimePart metadata."
                );
            }

            CharacterPartKind kind = ParsePartKind(assetType);
            CharacterEquipmentSlot slot = ParseSlot(descriptor.runtimePart.slot);
            CharacterPartAsset.MountMode mountMode = ParseMountMode(
                descriptor.runtimePart.mountMode,
                kind
            );

            string partAssetPath = NormalizeAssetPath(
                Path.Combine(descriptorDirectory, descriptor.id + ".asset")
            );
            CharacterPartAsset part = AssetDatabase.LoadAssetAtPath<CharacterPartAsset>(partAssetPath);
            if (part == null)
            {
                part = ScriptableObject.CreateInstance<CharacterPartAsset>();
                part.name = descriptor.id;
                AssetDatabase.CreateAsset(part, partAssetPath);
            }

            ConfigurePartAsset(
                part,
                descriptor.id,
                kind,
                slot,
                generatedModel,
                mountMode,
                descriptor.runtimePart
            );

            string catalogueAssetPath = NormalizeAssetPath(descriptor.catalogueAsset ?? string.Empty);
            if (!catalogueAssetPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Descriptor catalogueAsset must be inside Assets/: {catalogueAssetPath}"
                );
            }

            CharacterPartCatalogue catalogue =
                AssetDatabase.LoadAssetAtPath<CharacterPartCatalogue>(catalogueAssetPath);
            if (catalogue == null)
            {
                catalogue = ScriptableObject.CreateInstance<CharacterPartCatalogue>();
                catalogue.name = Path.GetFileNameWithoutExtension(catalogueAssetPath);
                AssetDatabase.CreateAsset(catalogue, catalogueAssetPath);
            }

            UpsertCatalogueEntry(catalogue, part);
            Debug.Log(
                $"Character Factory imported '{descriptor.id}' ({kind}/{slot}) from {fbxAssetPath}."
            );
        }

        private static CharacterFactoryImportDescriptor ReadDescriptor(string assetPath)
        {
            string fullPath = Path.GetFullPath(assetPath);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("Character Factory descriptor not found", fullPath);
            }

            string json = File.ReadAllText(fullPath);
            CharacterFactoryImportDescriptor descriptor =
                JsonUtility.FromJson<CharacterFactoryImportDescriptor>(json);
            if (descriptor == null)
            {
                throw new InvalidOperationException("Descriptor JSON could not be parsed.");
            }

            return descriptor;
        }

        private static CharacterPartKind ParsePartKind(string assetType)
        {
            switch (assetType)
            {
                case "clothing":
                    return CharacterPartKind.Clothing;
                case "weapon":
                    return CharacterPartKind.Weapon;
                case "accessory":
                    return CharacterPartKind.Accessory;
                default:
                    throw new InvalidOperationException(
                        $"Unsupported equippable assetType '{assetType}'."
                    );
            }
        }

        private static CharacterEquipmentSlot ParseSlot(string value)
        {
            if (!Enum.TryParse(value, true, out CharacterEquipmentSlot slot))
            {
                throw new InvalidOperationException(
                    $"Unknown character equipment slot '{value}'."
                );
            }

            return slot;
        }

        private static CharacterPartAsset.MountMode ParseMountMode(
            string value,
            CharacterPartKind kind)
        {
            if (!string.IsNullOrWhiteSpace(value) &&
                Enum.TryParse(value, true, out CharacterPartAsset.MountMode parsed))
            {
                return parsed;
            }

            return kind == CharacterPartKind.Clothing
                ? CharacterPartAsset.MountMode.SkinnedToCharacterSkeleton
                : CharacterPartAsset.MountMode.BoneSocket;
        }

        private static void ConfigurePartAsset(
            CharacterPartAsset part,
            string partId,
            CharacterPartKind kind,
            CharacterEquipmentSlot slot,
            GameObject prefab,
            CharacterPartAsset.MountMode mountMode,
            RuntimePartDescriptor runtimePart)
        {
            SerializedObject serialized = new SerializedObject(part);
            serialized.FindProperty("partId").stringValue = partId;
            serialized.FindProperty("kind").enumValueIndex = (int)kind;
            serialized.FindProperty("slot").enumValueIndex = (int)slot;
            serialized.FindProperty("prefab").objectReferenceValue = prefab;
            serialized.FindProperty("mountMode").enumValueIndex = (int)mountMode;
            serialized.FindProperty("socketBoneName").stringValue =
                runtimePart.socketBoneName ?? string.Empty;
            serialized.FindProperty("socketLocalPosition").vector3Value =
                ToVector3(runtimePart.socketLocalPosition, Vector3.zero);
            serialized.FindProperty("socketLocalEulerAngles").vector3Value =
                ToVector3(runtimePart.socketLocalEulerAngles, Vector3.zero);
            serialized.FindProperty("socketLocalScale").vector3Value =
                ToVector3(runtimePart.socketLocalScale, Vector3.one);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(part);
        }

        private static void UpsertCatalogueEntry(
            CharacterPartCatalogue catalogue,
            CharacterPartAsset part)
        {
            SerializedObject serialized = new SerializedObject(catalogue);
            SerializedProperty entries = serialized.FindProperty("entries");
            int matchingIndex = -1;

            for (int i = 0; i < entries.arraySize; i++)
            {
                CharacterPartAsset existing =
                    entries.GetArrayElementAtIndex(i).objectReferenceValue as CharacterPartAsset;
                if (existing == part ||
                    (existing != null && string.Equals(
                        existing.PartId,
                        part.PartId,
                        StringComparison.Ordinal)))
                {
                    matchingIndex = i;
                    break;
                }
            }

            if (matchingIndex < 0)
            {
                matchingIndex = entries.arraySize;
                entries.InsertArrayElementAtIndex(matchingIndex);
            }

            entries.GetArrayElementAtIndex(matchingIndex).objectReferenceValue = part;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalogue);
        }

        private static Vector3 ToVector3(float[] values, Vector3 fallback)
        {
            if (values == null || values.Length != 3)
            {
                return fallback;
            }

            return new Vector3(values[0], values[1], values[2]);
        }

        private static string NormalizeAssetPath(string path)
        {
            return path.Replace('\\', '/');
        }

        [Serializable]
        private sealed class CharacterFactoryImportDescriptor
        {
            public int schemaVersion;
            public string id;
            public string assetType;
            public string fbx;
            public string catalogueAsset;
            public RuntimePartDescriptor runtimePart;
        }

        [Serializable]
        private sealed class RuntimePartDescriptor
        {
            public string partKind;
            public string slot;
            public string mountMode;
            public string socketBoneName;
            public float[] socketLocalPosition;
            public float[] socketLocalEulerAngles;
            public float[] socketLocalScale;
        }
    }
}
