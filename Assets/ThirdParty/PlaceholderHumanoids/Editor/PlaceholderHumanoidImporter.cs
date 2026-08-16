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

        private void OnPostprocessMeshHierarchy(GameObject gameObject)
        {
            if (!IsBodyModel)
            {
                return;
            }

            // Rocketbox avatar FBXs contain hipoly/midpoly/lowpoly/ultralowpoly
            // hierarchies simultaneously. Keep one development-friendly resolution
            // active so the placeholder does not render stacked duplicate meshes.
            var name = gameObject.name.ToLowerInvariant();
            if (name.Contains("poly"))
            {
                gameObject.SetActive(name.Contains("midpoly"));
            }
        }
    }
}
