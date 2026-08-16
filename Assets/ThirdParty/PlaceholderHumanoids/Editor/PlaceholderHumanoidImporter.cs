using System;
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
            // for every node. Rocketbox's LOD nodes can therefore be nested below the root.
            // Select the development-friendly midpoly branch before avatar/skinned-mesh
            // generation, while the source FBX node names are still available.
            SelectMidpoly(root.transform);
        }

        private static void SelectMidpoly(Transform transform)
        {
            var name = transform.name.ToLowerInvariant();
            if (IsRocketboxLodName(name))
            {
                transform.gameObject.SetActive(name.Contains("midpoly"));
            }

            foreach (Transform child in transform)
            {
                SelectMidpoly(child);
            }
        }

        private static bool IsRocketboxLodName(string name)
        {
            return name.Contains("midpoly") ||
                   name.Contains("hipoly") ||
                   name.Contains("ultralowpoly") ||
                   name.Contains("lowpoly");
        }
    }
}
