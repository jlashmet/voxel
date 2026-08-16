using System;
using UnityEditor;

namespace VoxelGame.Editor
{
    /// <summary>
    /// Keeps temporary third-party character assets behind Unity's Humanoid contract so
    /// gameplay/animation code does not depend on a specific placeholder skeleton.
    /// </summary>
    internal sealed class PlaceholderHumanoidImporter : AssetPostprocessor
    {
        private const string Root = "Assets/ThirdParty/PlaceholderHumanoids/";

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Root, StringComparison.OrdinalIgnoreCase))
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

            // Rocketbox files are body/rig placeholders. The KayKit FBX is kept only
            // as a compact source of humanoid animation clips.
            importer.importAnimation = assetPath.IndexOf("/Animations/", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
