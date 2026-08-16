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

        private void OnPostprocessModel(GameObject model)
        {
            if (!IsBodyModel)
            {
                return;
            }

            // Rocketbox avatar FBXs contain hipoly/midpoly/lowpoly/ultralowpoly
            // branches simultaneously. Normalize their active state only after Unity
            // has built the complete model hierarchy. Doing this from
            // OnPostprocessMeshHierarchy can leave the selected renderer under an
            // inactive ancestor depending on FBX hierarchy/import order.
            foreach (var transform in model.GetComponentsInChildren<Transform>(true))
            {
                var name = transform.name.ToLowerInvariant();
                if (name.Contains("midpoly"))
                {
                    transform.gameObject.SetActive(true);
                }
                else if (name.Contains("hipoly") ||
                         name.Contains("ultralowpoly") ||
                         name.Contains("lowpoly"))
                {
                    transform.gameObject.SetActive(false);
                }
            }
        }
    }
}
