using System;
using System.Collections.Generic;
using System.IO;
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

        private static readonly HashSet<string> LoopingAnimationNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Idle",
                "Walk",
                "Run",
                "CrouchIdle"
            };

        private bool IsPlaceholderAsset =>
            assetPath.StartsWith(Root, StringComparison.OrdinalIgnoreCase);

        private bool IsBodyModel =>
            IsPlaceholderAsset &&
            assetPath.IndexOf(ModelFolder, StringComparison.OrdinalIgnoreCase) >= 0;

        private bool IsAnimationAsset =>
            IsPlaceholderAsset &&
            assetPath.IndexOf(AnimationFolder, StringComparison.OrdinalIgnoreCase) >= 0;

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
            importer.importAnimation = IsAnimationAsset;
        }

        private void OnPreprocessAnimation()
        {
            if (!IsAnimationAsset)
            {
                return;
            }

            var importer = (ModelImporter)assetImporter;
            var clips = importer.defaultClipAnimations;
            var shouldLoop = LoopingAnimationNames.Contains(
                Path.GetFileNameWithoutExtension(assetPath));

            foreach (var clip in clips)
            {
                clip.loopTime = shouldLoop;
            }

            // On first import Unity leaves clipAnimations empty. Persisting the default
            // take definitions here gives the temporary pack explicit gameplay semantics:
            // locomotion/idles loop, while interaction emotes remain one-shot.
            importer.clipAnimations = clips;
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
}
