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
        private const string MalePrefabPath = "Assets/ThirdParty/PlaceholderHumanoids/Models/placeholder_male.prefab";
        private const string FemalePrefabPath = "Assets/ThirdParty/PlaceholderHumanoids/Models/placeholder_female.prefab";

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
        public void PlaceholderBody_LoadsAsRiggedHumanoidWithPreferredLod(string path)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(model, Is.Not.Null, $"Unity could not load placeholder body {path}");

            // Humanoid import can collapse source transform nodes before the persistent FBX
            // prefab is written. Validate the resulting renderer state and recover Rocketbox
            // LOD identity from either retained ancestry or the imported mesh name.
            var allSkinnedMeshes = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Assert.That(allSkinnedMeshes.Length, Is.GreaterThan(0),
                $"{path} has no skinned mesh renderer");

            var lodMeshes = allSkinnedMeshes
                .Where(renderer => GetRendererLod(renderer) != RocketboxLod.None)
                .ToArray();
            Assert.That(lodMeshes.Length, Is.GreaterThan(0),
                $"{path} exposes no Rocketbox LOD identity after import. {DescribeRenderers(allSkinnedMeshes, model.transform)}");

            var preferredLod = lodMeshes.Select(GetRendererLod)
                .Distinct()
                .OrderBy(GetPreferenceRank)
                .First();

            foreach (var renderer in lodMeshes)
            {
                var active = IsActiveWithinAsset(renderer.transform, model.transform);
                Assert.That(active, Is.EqualTo(GetRendererLod(renderer) == preferredLod),
                    $"Unexpected active state for Rocketbox LOD renderer {DescribeRenderer(renderer, model.transform)} in {path}; preferred={preferredLod}");
            }

            var assetActiveMeshes = allSkinnedMeshes
                .Where(renderer => IsActiveWithinAsset(renderer.transform, model.transform))
                .ToArray();
            Assert.That(assetActiveMeshes.Length, Is.GreaterThan(0),
                $"{path} has no skinned mesh enabled by the imported asset hierarchy. {DescribeRenderers(allSkinnedMeshes, model.transform)}");
            Assert.That(assetActiveMeshes.Any(renderer => GetRendererLod(renderer) == preferredLod), Is.True,
                $"{path} has no active {preferredLod} skinned mesh. {DescribeRenderers(allSkinnedMeshes, model.transform)}");
            Assert.That(assetActiveMeshes.Any(renderer =>
                    GetRendererLod(renderer) != RocketboxLod.None &&
                    GetRendererLod(renderer) != preferredLod), Is.False,
                $"{path} left a non-preferred Rocketbox LOD renderer active. {DescribeRenderers(allSkinnedMeshes, model.transform)}");

            AssertValidHumanoidAvatar(path);
        }

        [TestCase(MalePrefabPath)]
        [TestCase(FemalePrefabPath)]
        public void PlaceholderDescriptor_GeneratesCharacterFactoryPrefab(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null,
                $"CharacterFactoryAssetImporter did not generate {path}");

            Assert.That(prefab.transform.Find("Equipment"), Is.Not.Null,
                $"{path} is missing the Character Factory Equipment root");

            var equipmentControllers = prefab.GetComponents<MonoBehaviour>()
                .Where(component => component != null &&
                    component.GetType().FullName == "VoxelEngine.Characters.Runtime.CharacterEquipmentController")
                .ToArray();
            Assert.That(equipmentControllers.Length, Is.EqualTo(1),
                $"{path} should have exactly one CharacterEquipmentController on its stable root");

            var renderer = prefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Assert.That(renderer, Is.Not.Null, $"{path} contains no skinned character mesh");

            var animator = prefab.GetComponentInChildren<Animator>(true);
            Assert.That(animator, Is.Not.Null, $"{path} contains no Animator");
            Assert.That(animator.avatar, Is.Not.Null, $"{path} Animator has no Avatar");
            Assert.That(animator.avatar.isValid, Is.True, $"{path} Animator Avatar is invalid");
            Assert.That(animator.avatar.isHuman, Is.True, $"{path} Animator Avatar is not Humanoid");
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

        [TestCase("Assets/ThirdParty/PlaceholderHumanoids/Animations/Idle.fbx", true)]
        [TestCase("Assets/ThirdParty/PlaceholderHumanoids/Animations/Walk.fbx", true)]
        [TestCase("Assets/ThirdParty/PlaceholderHumanoids/Animations/Run.fbx", true)]
        [TestCase("Assets/ThirdParty/PlaceholderHumanoids/Animations/CrouchIdle.fbx", true)]
        [TestCase("Assets/ThirdParty/PlaceholderHumanoids/Animations/Wave.fbx", false)]
        [TestCase("Assets/ThirdParty/PlaceholderHumanoids/Animations/Shrug.fbx", false)]
        public void AnimationFile_HasExpectedLoopingContract(string path, bool expectedLoop)
        {
            AssetDatabase.ImportAsset(
                path,
                ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            Assert.That(importer, Is.Not.Null, $"Expected a ModelImporter for {path}");
            Assert.That(importer.clipAnimations.Length, Is.GreaterThanOrEqualTo(1),
                $"{path} has no explicit clip import configuration");
            Assert.That(importer.clipAnimations.All(clip => clip.loopTime == expectedLoop), Is.True,
                $"{path} loopTime does not match expected gameplay semantics ({expectedLoop})");
        }

        private static RocketboxLod GetRendererLod(SkinnedMeshRenderer renderer)
        {
            for (var current = renderer.transform; current != null; current = current.parent)
            {
                var lod = GetLod(current.name);
                if (lod != RocketboxLod.None)
                {
                    return lod;
                }
            }

            return GetLod(renderer.sharedMesh?.name);
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

        private static string DescribeRenderers(SkinnedMeshRenderer[] renderers, Transform root)
        {
            return "Renderers: " + string.Join("; ", renderers.Select(renderer => DescribeRenderer(renderer, root)));
        }

        private static string DescribeRenderer(SkinnedMeshRenderer renderer, Transform root)
        {
            var meshName = renderer.sharedMesh != null ? renderer.sharedMesh.name : "<null>";
            return $"{GetPath(renderer.transform, root)} mesh={meshName} lod={GetRendererLod(renderer)} active={IsActiveWithinAsset(renderer.transform, root)}";
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
