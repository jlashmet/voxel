using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace VoxelGame.Editor
{
    /// <summary>
    /// One-shot CI entrypoint used to materialize the temporary Character Factory prefabs
    /// so their Unity GUIDs can be committed and safely referenced by scenes/prefabs.
    /// </summary>
    public static class PlaceholderCharacterMaterializer
    {
        private static readonly string[] ModelPaths =
        {
            "Assets/ThirdParty/PlaceholderHumanoids/Models/Male_Adult_01.fbx",
            "Assets/ThirdParty/PlaceholderHumanoids/Models/Female_Adult_01.fbx"
        };

        private static readonly string[] DescriptorPaths =
        {
            "Assets/ThirdParty/PlaceholderHumanoids/Models/placeholder_male.characterfactory.json",
            "Assets/ThirdParty/PlaceholderHumanoids/Models/placeholder_female.characterfactory.json"
        };

        private static readonly string[] RequiredOutputs =
        {
            "Assets/ThirdParty/PlaceholderHumanoids/Models/placeholder_male.prefab",
            "Assets/ThirdParty/PlaceholderHumanoids/Models/placeholder_female.prefab",
            "Assets/ThirdParty/PlaceholderHumanoids/PlaceholderCharacterParts.asset"
        };

        public static void Run()
        {
            foreach (string modelPath in ModelPaths)
            {
                AssetDatabase.ImportAsset(
                    modelPath,
                    ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

                if (AssetDatabase.LoadAssetAtPath<GameObject>(modelPath) == null)
                {
                    throw new InvalidOperationException($"Placeholder model did not import: {modelPath}");
                }
            }

            foreach (string descriptorPath in DescriptorPaths)
            {
                AssetDatabase.ImportAsset(
                    descriptorPath,
                    ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            }

            FlushCharacterFactoryPendingImports();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            foreach (string outputPath in RequiredOutputs)
            {
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(outputPath) == null)
                {
                    throw new InvalidOperationException($"Character Factory output is missing: {outputPath}");
                }

                if (!File.Exists(Path.GetFullPath(outputPath)))
                {
                    throw new InvalidOperationException($"Character Factory output was not written to disk: {outputPath}");
                }

                string metaPath = outputPath + ".meta";
                if (!File.Exists(Path.GetFullPath(metaPath)))
                {
                    throw new InvalidOperationException($"Unity metadata was not written to disk: {metaPath}");
                }
            }

            Debug.Log("Temporary placeholder Character Factory assets materialized successfully.");
        }

        private static void FlushCharacterFactoryPendingImports()
        {
            const string ImporterTypeName =
                "VoxelEngine.Characters.Editor.CharacterFactoryAssetImporter, VoxelEngine.Characters.Editor";

            Type importerType = Type.GetType(ImporterTypeName);
            if (importerType == null)
            {
                throw new InvalidOperationException($"Could not load {ImporterTypeName}.");
            }

            MethodInfo processMethod = importerType.GetMethod(
                "ProcessPendingDescriptors",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (processMethod == null)
            {
                throw new InvalidOperationException(
                    "Could not locate CharacterFactoryAssetImporter.ProcessPendingDescriptors.");
            }

            processMethod.Invoke(null, null);
        }
    }
}
