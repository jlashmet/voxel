using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class PlaceholderHumanoidAssetTests
    {
        private const string MalePath = "Assets/ThirdParty/PlaceholderHumanoids/Models/Male_Adult_01.fbx";
        private const string FemalePath = "Assets/ThirdParty/PlaceholderHumanoids/Models/Female_Adult_01.fbx";

        [TestCase(MalePath, false)]
        [TestCase(FemalePath, false)]
        [TestCase("Assets/ThirdParty/PlaceholderHumanoids/Animations/Idle.fbx", true)]
        [TestCase("Assets/ThirdParty/PlaceholderHumanoids/Animations/Walk.fbx", true)]
        [TestCase("Assets/ThirdParty/PlaceholderHumanoids/Animations/Run.fbx", true)]
        [TestCase("Assets/ThirdParty/PlaceholderHumanoids/Animations/CrouchIdle.fbx", true)]
        [TestCase("Assets/ThirdParty/PlaceholderHumanoids/Animations/Wave.fbx", true)]
        [TestCase("Assets/ThirdParty/PlaceholderHumanoids/Animations/Shrug.fbx", true)]
        public void PlaceholderFbx_UsesHumanoidImportContract(string path, bool importsAnimation)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;

            Assert.That(importer, Is.Not.Null, $"Expected a ModelImporter for {path}");
            Assert.That(importer.animationType, Is.EqualTo(ModelImporterAnimationType.Human));
            Assert.That(importer.avatarSetup, Is.EqualTo(ModelImporterAvatarSetup.CreateFromThisModel));
            Assert.That(importer.importAnimation, Is.EqualTo(importsAnimation));
            Assert.That(importer.importCameras, Is.False);
            Assert.That(importer.importLights, Is.False);
            Assert.That(importer.materialImportMode, Is.EqualTo(ModelImporterMaterialImportMode.None));
        }

        [TestCase(MalePath)]
        [TestCase(FemalePath)]
        public void PlaceholderBody_LoadsAsRiggedHumanoidAtMidpoly(string path)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(model, Is.Not.Null, $"Unity could not load placeholder body {path}");

            var allTransforms = model.GetComponentsInChildren<Transform>(true);
            var lodNodes = allTransforms.Where(IsRocketboxLodNode).ToArray();
            Assert.That(lodNodes.Length, Is.GreaterThan(0), $"{path} did not expose Rocketbox LOD nodes");
            Assert.That(lodNodes.Any(IsMidpolyNode), Is.True, $"{path} has no midpoly hierarchy");

            foreach (var node in lodNodes)
            {
                Assert.That(node.gameObject.activeSelf, Is.EqualTo(IsMidpolyNode(node)),
                    $"Unexpected active state for Rocketbox LOD node {node.name} in {path}");
            }

            var activeSkinnedMeshes = model.GetComponentsInChildren<SkinnedMeshRenderer>(false);
            Assert.That(activeSkinnedMeshes.Length, Is.GreaterThan(0),
                $"{path} has no active skinned mesh renderer");
            Assert.That(activeSkinnedMeshes.Any(renderer => HasAncestor(renderer.transform, IsMidpolyNode)), Is.True,
                $"{path} has no active skinned mesh under its midpoly hierarchy");
            Assert.That(activeSkinnedMeshes.Any(renderer => HasAncestor(renderer.transform, IsNonMidpolyLodNode)), Is.False,
                $"{path} left a non-midpoly Rocketbox LOD renderer active");

            AssertValidHumanoidAvatar(path);
        }

        [TestCase("Assets/ThirdParty/PlaceholderHumanoids/Animations/Idle.fbx")]
        [TestCase("Assets/ThirdParty/PlaceholderHumanoids/Animations/Walk.fbx")]
        [TestCase("Assets/ThirdParty/PlaceholderHumanoids/Animations/Run.fbx")]
        [TestCase("Assets/ThirdParty/PlaceholderHumanoids/Animations/CrouchIdle.fbx")]
        [TestCase("Assets/ThirdParty/PlaceholderHumanoids/Animations/Wave.fbx")]
        [TestCase("Assets/ThirdParty/PlaceholderHumanoids/Animations/Shrug.fbx")]
        public void AnimationFile_ContainsRetargetableClip(string path)
        {
            AssertValidHumanoidAvatar(path);

            var clips = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__"))
                .ToArray();

            Assert.That(clips.Length, Is.GreaterThanOrEqualTo(1), $"{path} exposes no animation clip");
        }

        private static bool IsRocketboxLodNode(Transform transform)
        {
            var name = transform.name.ToLowerInvariant();
            return name.Contains("midpoly") ||
                   name.Contains("hipoly") ||
                   name.Contains("ultralowpoly") ||
                   name.Contains("lowpoly");
        }

        private static bool IsMidpolyNode(Transform transform)
        {
            return transform.name.ToLowerInvariant().Contains("midpoly");
        }

        private static bool IsNonMidpolyLodNode(Transform transform)
        {
            return IsRocketboxLodNode(transform) && !IsMidpolyNode(transform);
        }

        private static bool HasAncestor(Transform transform, System.Func<Transform, bool> predicate)
        {
            for (var current = transform; current != null; current = current.parent)
            {
                if (predicate(current))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AssertValidHumanoidAvatar(string path)
        {
            var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            Assert.That(avatar, Is.Not.Null, $"{path} did not generate a Humanoid Avatar");
            Assert.That(avatar.isValid, Is.True, $"{path} generated an invalid Avatar");
            Assert.That(avatar.isHuman, Is.True, $"{path} did not generate a Humanoid Avatar");
        }
    }
}
