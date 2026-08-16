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

            // Humanoid import can collapse source transform nodes before the persistent FBX
            // prefab is written. Validate the resulting renderer state and use either retained
            // transform ancestry or the imported mesh name to recover the Rocketbox LOD label.
            var allSkinnedMeshes = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Assert.That(allSkinnedMeshes.Length, Is.GreaterThan(0),
                $"{path} has no skinned mesh renderer");

            var lodMeshes = allSkinnedMeshes.Where(IsRocketboxLodRenderer).ToArray();
            Assert.That(lodMeshes.Length, Is.GreaterThan(0),
                $"{path} exposes no Rocketbox LOD identity after import. {DescribeRenderers(allSkinnedMeshes, model.transform)}");
            Assert.That(lodMeshes.Any(IsMidpolyRenderer), Is.True,
                $"{path} exposes no midpoly mesh after import. {DescribeRenderers(allSkinnedMeshes, model.transform)}");

            foreach (var renderer in lodMeshes)
            {
                var active = IsActiveWithinAsset(renderer.transform, model.transform);
                Assert.That(active, Is.EqualTo(IsMidpolyRenderer(renderer)),
                    $"Unexpected active state for Rocketbox LOD renderer {DescribeRenderer(renderer, model.transform)} in {path}");
            }

            var assetActiveMeshes = allSkinnedMeshes
                .Where(renderer => IsActiveWithinAsset(renderer.transform, model.transform))
                .ToArray();
            Assert.That(assetActiveMeshes.Length, Is.GreaterThan(0),
                $"{path} has no skinned mesh enabled by the imported asset hierarchy. {DescribeRenderers(allSkinnedMeshes, model.transform)}");
            Assert.That(assetActiveMeshes.Any(IsMidpolyRenderer), Is.True,
                $"{path} has no active midpoly skinned mesh. {DescribeRenderers(allSkinnedMeshes, model.transform)}");
            Assert.That(assetActiveMeshes.Any(renderer => IsRocketboxLodRenderer(renderer) && !IsMidpolyRenderer(renderer)), Is.False,
                $"{path} left a non-midpoly Rocketbox LOD renderer active. {DescribeRenderers(allSkinnedMeshes, model.transform)}");

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

        private static bool IsRocketboxLodRenderer(SkinnedMeshRenderer renderer)
        {
            return HasAncestor(renderer.transform, transform => IsRocketboxLodName(transform.name)) ||
                   IsRocketboxLodName(renderer.sharedMesh?.name);
        }

        private static bool IsMidpolyRenderer(SkinnedMeshRenderer renderer)
        {
            return HasAncestor(renderer.transform, transform => ContainsMidpoly(transform.name)) ||
                   ContainsMidpoly(renderer.sharedMesh?.name);
        }

        private static bool IsRocketboxLodName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            name = name.ToLowerInvariant();
            return name.Contains("midpoly") ||
                   name.Contains("hipoly") ||
                   name.Contains("ultralowpoly") ||
                   name.Contains("lowpoly");
        }

        private static bool ContainsMidpoly(string name)
        {
            return !string.IsNullOrEmpty(name) && name.ToLowerInvariant().Contains("midpoly");
        }

        private static bool IsActiveWithinAsset(Transform transform, Transform root)
        {
            for (var current = transform; current != null; current = current.parent)
            {
                if (!current.gameObject.activeSelf)
                {
                    return false;
                }

                if (current == root)
                {
                    return true;
                }
            }

            return false;
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

        private static string DescribeRenderers(SkinnedMeshRenderer[] renderers, Transform root)
        {
            return "Renderers: " + string.Join("; ", renderers.Select(renderer => DescribeRenderer(renderer, root)));
        }

        private static string DescribeRenderer(SkinnedMeshRenderer renderer, Transform root)
        {
            var meshName = renderer.sharedMesh != null ? renderer.sharedMesh.name : "<null>";
            return $"{GetPath(renderer.transform, root)} mesh={meshName} active={IsActiveWithinAsset(renderer.transform, root)}";
        }

        private static string GetPath(Transform transform, Transform root)
        {
            var names = new System.Collections.Generic.List<string>();
            for (var current = transform; current != null; current = current.parent)
            {
                names.Add(current.name);
                if (current == root)
                {
                    break;
                }
            }

            names.Reverse();
            return string.Join("/", names);
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
