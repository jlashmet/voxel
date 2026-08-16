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
    /// ready-to-use Unity assets. Equippable descriptors create/update CharacterPartAsset entries
    /// and the shared catalogue; character descriptors create/update a prefab wired to that same
    /// catalogue. The descriptor is staged beside its generated FBX, so no browser or manual
    /// inspector setup is required after the build is copied into Assets/.
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

            CharacterPartCatalogue catalogue = GetOrCreateCatalogue(descriptor.catalogueAsset);
            string assetType = (descriptor.assetType ?? string.Empty).Trim().ToLowerInvariant();
            if (assetType == "character")
            {
                string prefabPath = CreateOrUpdateCharacterPrefab(
                    descriptorDirectory,
                    descriptor.id,
                    generatedModel,
                    catalogue
                );
                Debug.Log(
                    $"Character Factory imported character '{descriptor.id}' -> {prefabPath}."
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

            UpsertCatalogueEntry(catalogue, part);
            Debug.Log(
                $"Character Factory imported '{descriptor.id}' ({kind}/{slot}) from {fbxAssetPath}."
            );
        }

        private static CharacterPartCatalogue GetOrCreateCatalogue(string catalogueAssetPath)
        {
            string normalized = NormalizeAssetPath(catalogueAssetPath ?? string.Empty);
            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Descriptor catalogueAsset must be inside Assets/: {normalized}"
                );
            }

            CharacterPartCatalogue catalogue =
                AssetDatabase.LoadAssetAtPath<CharacterPartCatalogue>(normalized);
            if (catalogue != null)
            {
                return catalogue;
            }

            string parent = NormalizeAssetPath(Path.GetDirectoryName(normalized) ?? "Assets");
            if (!AssetDatabase.IsValidFolder(parent))
            {
                throw new InvalidOperationException(
                    $"Catalogue parent folder has not been imported yet: {parent}"
                );
            }

            catalogue = ScriptableObject.CreateInstance<CharacterPartCatalogue>();
            catalogue.name = Path.GetFileNameWithoutExtension(normalized);
            AssetDatabase.CreateAsset(catalogue, normalized);
            return catalogue;
        }

        private static string CreateOrUpdateCharacterPrefab(
            string descriptorDirectory,
            string characterId,
            GameObject generatedModel,
            CharacterPartCatalogue catalogue)
        {
            string prefabPath = NormalizeAssetPath(
                Path.Combine(descriptorDirectory, characterId + ".prefab")
            );

            GameObject root = new GameObject(characterId);
            try
            {
                GameObject modelInstance =
                    PrefabUtility.InstantiatePrefab(generatedModel) as GameObject;
                if (modelInstance == null)
                {
                    modelInstance = UnityEngine.Object.Instantiate(generatedModel);
                }

                modelInstance.name = generatedModel.name;
                modelInstance.transform.SetParent(root.transform, false);

                SkinnedMeshRenderer[] renderers =
                    modelInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (renderers.Length == 0)
                {
                    throw new InvalidOperationException(
                        $"Generated character '{characterId}' contains no SkinnedMeshRenderer."
                    );
                }

                Transform skeletonRoot = FindSkeletonRoot(renderers, modelInstance.transform);
                GameObject equipment = new GameObject("Equipment");
                equipment.transform.SetParent(root.transform, false);

                CharacterEquipmentController controller =
                    root.AddComponent<CharacterEquipmentController>();
                SerializedObject serialized = new SerializedObject(controller);
                serialized.FindProperty("skeletonRoot").objectReferenceValue = skeletonRoot;
                serialized.FindProperty("equipmentRoot").objectReferenceValue = equipment.transform;
                serialized.FindProperty("catalogue").objectReferenceValue = catalogue;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                if (saved == null)
                {
                    throw new InvalidOperationException(
                        $"Failed to save generated character prefab: {prefabPath}"
                    );
                }

                return prefabPath;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Transform FindSkeletonRoot(
            SkinnedMeshRenderer[] renderers,
            Transform fallback)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i].rootBone != null)
                {
                    return renderers[i].rootBone;
                }
            }

            Transform[] transforms = fallback.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (string.Equals(transforms[i].name, "Armature", StringComparison.Ordinal))
                {
                    return transforms[i];
                }
            }

            return fallback;
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
