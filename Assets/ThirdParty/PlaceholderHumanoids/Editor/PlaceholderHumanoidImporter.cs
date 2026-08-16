using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

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

        private static readonly HashSet<string> OneShotAnimationNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Wave",
                "Shrug"
            };

        private bool IsPlaceholderAsset =>
            assetPath.StartsWith(Root, StringComparison.OrdinalIgnoreCase);

        private bool IsBodyModel =>
            IsPlaceholderAsset &&
            assetPath.IndexOf(ModelFolder, StringComparison.OrdinalIgnoreCase) >= 0;

        private bool IsAnimationAsset =>
            IsPlaceholderAsset &&
            assetPath.IndexOf(AnimationFolder, StringComparison.OrdinalIgnoreCase) >= 0;

        public override uint GetVersion()
        {
            // Bump when import behavior changes so Unity reimports associated FBXs even
            // when CI/editor sessions retain a warm Library cache.
            return 7;
        }

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
            if (!IsAnimationAsset || !TryGetLoopPolicy(assetPath, out var shouldLoop))
            {
                // Future authenticated Mixamo or other Humanoid clips keep their authored
                // import semantics unless they are intentionally added to the starter policy.
                return;
            }

            var importer = (ModelImporter)assetImporter;
            var clips = importer.defaultClipAnimations;
            var semanticName = Path.GetFileNameWithoutExtension(assetPath);

            foreach (var clip in clips)
            {
                // Hide Rocketbox's source take names behind the same small semantic contract
                // used by the local asset paths and future generated-character animation data.
                clip.name = semanticName;
                clip.loopTime = shouldLoop;
                clip.loopPose = shouldLoop;

                // Prototype movement is controller/transform driven. Keep every temporary clip
                // in-place so animation playback cannot compete with the gameplay motor. The
                // Rocketbox Walk/Run sources come from the XY extraction set, but Unity 6 does
                // not expose usable root-motion curves from these Humanoid imports, so this
                // placeholder package deliberately makes no root-motion promise.
                clip.lockRootPositionXZ = true;
                clip.keepOriginalPositionXZ = false;
            }

            // On first import Unity leaves clipAnimations empty. Persist the default take
            // definitions here so the starter pack has explicit semantic names, looping rules,
            // and a controller-driven/in-place locomotion contract.
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

        private void OnPostprocessModel(GameObject root)
        {
            if (!IsBodyModel)
            {
                return;
            }

            AssignPlaceholderMaterial(root);
        }

        private static void AssignPlaceholderMaterial(GameObject root)
        {
            // We deliberately do not import Rocketbox's large legacy TGA material set.
            // Prefer the currently active SRP, but asset import can run before that pipeline
            // is instantiated (notably in batchmode). In that case use the project's configured
            // default pipeline before considering the Built-in Render Pipeline fallback.
            var pipeline = GraphicsSettings.currentRenderPipeline ?? GraphicsSettings.defaultRenderPipeline;
            var material = pipeline != null
                ? pipeline.defaultMaterial
                : AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");

            if (material == null)
            {
                throw new InvalidOperationException(
                    "Unity did not provide a default material for placeholder humanoid import.");
            }

            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var slotCount = Math.Max(1, renderer.sharedMaterials?.Length ?? 0);
                var materials = new Material[slotCount];
                for (var index = 0; index < materials.Length; index++)
                {
                    materials[index] = material;
                }

                renderer.sharedMaterials = materials;
            }
        }

        private static bool TryGetLoopPolicy(string path, out bool shouldLoop)
        {
            var animationName = Path.GetFileNameWithoutExtension(path);
            if (LoopingAnimationNames.Contains(animationName))
            {
                shouldLoop = true;
                return true;
            }

            if (OneShotAnimationNames.Contains(animationName))
            {
                shouldLoop = false;
                return true;
            }

            shouldLoop = false;
            return false;
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
