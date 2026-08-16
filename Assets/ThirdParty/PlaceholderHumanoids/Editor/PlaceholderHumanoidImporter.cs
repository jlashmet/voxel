using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VoxelGame.Editor
{
    /// <summary>
    /// Keeps temporary third-party character assets behind Unity's Humanoid contract so
    /// gameplay/animation code does not depend on a specific placeholder skeleton.
    /// </summary>
    internal sealed class PlaceholderHumanoidImporter : AssetPostprocessor
    {
        private const string Root = "Assets/ThirdParty/PlaceholderHumanoids/";
        private const string ModelFolder = "/Models/";
        private const string AnimationFolder = "/Animations/";

        private bool IsPlaceholderAsset =>
            assetPath.StartsWith(Root, StringComparison.OrdinalIgnoreCase);

        private bool IsBodyModel =>
            IsPlaceholderAsset &&
            assetPath.IndexOf(ModelFolder, StringComparison.OrdinalIgnoreCase) >= 0;

        private void OnPreprocessModel()
        {
            if (!IsPlaceholderAsset)
            {
                return;
            }

            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation =
                assetPath.IndexOf(AnimationFolder, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void OnPostprocessMeshHierarchy(GameObject root)
        {
            if (!IsBodyModel)
            {
                return;
            }

            // Unity invokes this callback once for each imported root hierarchy, not once
            // for every node. Collect every Rocketbox LOD node while source FBX names are
            // still available, then choose one LOD for the whole hierarchy. Prefer midpoly
            // for development when it exists, but some Rocketbox Export FBXs contain only
            // hipoly; in that case never deactivate the only usable body mesh.
            SelectPreferredLod(root.transform);
        }

        private static void SelectPreferredLod(Transform root)
        {
            var lodNodes = new List<Transform>();
            CollectLodNodes(root, lodNodes);
            if (lodNodes.Count == 0)
            {
                return;
            }

            var preferred = lodNodes.Select(node => GetLod(node.name))
                .Where(lod => lod != RocketboxLod.None)
                .OrderBy(GetPreferenceRank)
                .First();

            foreach (var node in lodNodes)
            {
                node.gameObject.SetActive(GetLod(node.name) == preferred);
            }
        }

        private static void CollectLodNodes(Transform transform, List<Transform> lodNodes)
        {
            if (GetLod(transform.name) != RocketboxLod.None)
            {
                lodNodes.Add(transform);
            }

            foreach (Transform child in transform)
            {
                CollectLodNodes(child, lodNodes);
            }
        }

        private static RocketboxLod GetLod(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return RocketboxLod.None;
            }

            name = name.ToLowerInvariant();
            if (name.Contains("midpoly"))
            {
                return RocketboxLod.Midpoly;
            }

            if (name.Contains("hipoly"))
            {
                return RocketboxLod.Hipoly;
            }

            if (name.Contains("ultralowpoly"))
            {
                return RocketboxLod.Ultralowpoly;
            }

            if (name.Contains("lowpoly"))
            {
                return RocketboxLod.Lowpoly;
            }

            return RocketboxLod.None;
        }

        private static int GetPreferenceRank(RocketboxLod lod)
        {
            switch (lod)
            {
                case RocketboxLod.Midpoly:
                    return 0;
                case RocketboxLod.Hipoly:
                    return 1;
                case RocketboxLod.Lowpoly:
                    return 2;
                case RocketboxLod.Ultralowpoly:
                    return 3;
                default:
                    return int.MaxValue;
            }
        }

        private enum RocketboxLod
        {
            None,
            Midpoly,
            Hipoly,
            Lowpoly,
            Ultralowpoly
        }
    }

    /// <summary>
    /// Deterministically regenerates the Character Factory outputs for the temporary
    /// humanoids. This is intentionally explicit because a warm Unity Library cache can
    /// remember that the descriptor was imported even after an untracked generated prefab
    /// has been deleted by a clean checkout.
    /// </summary>
    public static class PlaceholderCharacterAssetMaterializer
    {
        private static readonly string[] DescriptorPaths =
        {
            "Assets/ThirdParty/PlaceholderHumanoids/Models/placeholder_male.characterfactory.json",
            "Assets/ThirdParty/PlaceholderHumanoids/Models/placeholder_female.characterfactory.json"
        };

        private static readonly string[] ExpectedOutputPaths =
        {
            "Assets/ThirdParty/PlaceholderHumanoids/Models/placeholder_male.prefab",
            "Assets/ThirdParty/PlaceholderHumanoids/Models/placeholder_female.prefab",
            "Assets/ThirdParty/PlaceholderHumanoids/PlaceholderCharacterParts.asset"
        };

        public static void Materialize()
        {
            foreach (var descriptorPath in DescriptorPaths)
            {
                AssetDatabase.ImportAsset(
                    descriptorPath,
                    ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            }

            // CharacterFactoryAssetImporter intentionally processes descriptors on
            // EditorApplication.delayCall so it can safely create/update assets outside the
            // AssetDatabase import callback. Queue verification after that callback instead
            // of racing it from this executeMethod.
            EditorApplication.delayCall += VerifyMaterializedAssetsAndExit;
        }

        private static void VerifyMaterializedAssetsAndExit()
        {
            try
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                foreach (var outputPath in ExpectedOutputPaths)
                {
                    if (AssetDatabase.LoadMainAssetAtPath(outputPath) == null)
                    {
                        throw new InvalidOperationException(
                            $"Character Factory did not materialize expected placeholder asset: {outputPath}");
                    }
                }

                Debug.Log("Character Factory placeholder assets materialized successfully.");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(0);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                    return;
                }

                throw;
            }
        }
    }
}
