using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VoxelEngine.Characters.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class PlaceholderCharacterFactoryFreshImportTests
    {
        private const string Root = "Assets/ThirdParty/PlaceholderHumanoids";
        private const string MaleDescriptor = Root + "/Models/placeholder_male.characterfactory.json";
        private const string FemaleDescriptor = Root + "/Models/placeholder_female.characterfactory.json";
        private const string MalePrefab = Root + "/Models/placeholder_male.prefab";
        private const string FemalePrefab = Root + "/Models/placeholder_female.prefab";
        private const string Catalogue = Root + "/PlaceholderCharacterParts.asset";
        private const string AnimationRoot = Root + "/Animations/";

        [SetUp]
        public void SetUp()
        {
            DeleteDerivedOutputs();
        }

        [TearDown]
        public void TearDown()
        {
            DeleteDerivedOutputs();
        }

        [TestCase(MaleDescriptor, MalePrefab)]
        [TestCase(FemaleDescriptor, FemalePrefab)]
        public void Descriptor_RegeneratesAnimationReadyCharacterPrefabFromCleanState(
            string descriptorPath,
            string prefabPath)
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), Is.Null,
                $"Expected clean derived-output state before importing {descriptorPath}");

            AssetDatabase.ImportAsset(
                descriptorPath,
                ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            FlushCharacterFactoryPendingImports();
            FlushPlaceholderAnimationPendingImports();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null,
                $"Character Factory did not regenerate {prefabPath} from {descriptorPath}");
            Assert.That(prefab.transform.Find("Equipment"), Is.Not.Null,
                $"{prefabPath} is missing its Equipment root");

            var equipmentControllers = prefab.GetComponents<MonoBehaviour>()
                .Where(component => component != null &&
                    component.GetType().FullName ==
                    "VoxelEngine.Characters.Runtime.CharacterEquipmentController")
                .ToArray();
            Assert.That(equipmentControllers.Length, Is.EqualTo(1),
                $"{prefabPath} should contain exactly one CharacterEquipmentController");

            var renderer = prefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Assert.That(renderer, Is.Not.Null,
                $"{prefabPath} contains no skinned character mesh");

            var animator = prefab.GetComponentInChildren<Animator>(true);
            Assert.That(animator, Is.Not.Null,
                $"{prefabPath} contains no Animator");
            Assert.That(animator.avatar, Is.Not.Null,
                $"{prefabPath} Animator has no Avatar");
            Assert.That(animator.avatar.isValid, Is.True,
                $"{prefabPath} Animator Avatar is invalid");
            Assert.That(animator.avatar.isHuman, Is.True,
                $"{prefabPath} Animator Avatar is not Humanoid");
            Assert.That(animator.runtimeAnimatorController, Is.Null,
                $"{prefabPath} should remain Playables-driven without a temporary AnimatorController");

            var animationPlayer = prefab.GetComponent<CharacterAnimationPlayer>();
            Assert.That(animationPlayer, Is.Not.Null,
                $"{prefabPath} is missing CharacterAnimationPlayer");
            Assert.That(animationPlayer.Animator, Is.SameAs(animator),
                $"{prefabPath} CharacterAnimationPlayer is not wired to its Humanoid Animator");

            var animationPolicy = prefab.GetComponent<CharacterAnimationPolicy>();
            Assert.That(animationPolicy, Is.Not.Null,
                $"{prefabPath} is missing CharacterAnimationPolicy");
            Assert.That(animationPolicy.Player, Is.SameAs(animationPlayer),
                $"{prefabPath} CharacterAnimationPolicy is not wired to its player");

            AssertClipReference(animationPolicy, "idleClip", "Idle");
            AssertClipReference(animationPolicy, "walkClip", "Walk");
            AssertClipReference(animationPolicy, "runClip", "Run");
            AssertClipReference(animationPolicy, "crouchIdleClip", "CrouchIdle");

            var catalogue = AssetDatabase.LoadAssetAtPath<ScriptableObject>(Catalogue);
            Assert.That(catalogue, Is.Not.Null,
                $"Character Factory did not regenerate the shared catalogue at {Catalogue}");
        }

        private static void AssertClipReference(
            CharacterAnimationPolicy policy,
            string propertyName,
            string clipName)
        {
            var serialized = new SerializedObject(policy);
            var property = serialized.FindProperty(propertyName);
            Assert.That(property, Is.Not.Null,
                $"CharacterAnimationPolicy is missing serialized property {propertyName}");

            var clip = property.objectReferenceValue as AnimationClip;
            Assert.That(clip, Is.Not.Null,
                $"CharacterAnimationPolicy.{propertyName} is not assigned");
            Assert.That(clip.name, Is.EqualTo(clipName));
            Assert.That(
                AssetDatabase.GetAssetPath(clip),
                Is.EqualTo(AnimationRoot + clipName + ".fbx"),
                $"CharacterAnimationPolicy.{propertyName} is not wired to the committed placeholder clip"
            );
        }

        private static void DeleteDerivedOutputs()
        {
            DeleteIfPresent(MalePrefab);
            DeleteIfPresent(FemalePrefab);
            DeleteIfPresent(Catalogue);
        }

        private static void DeleteIfPresent(string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
            {
                Assert.That(AssetDatabase.DeleteAsset(path), Is.True,
                    $"Could not delete derived Character Factory asset {path}");
            }
        }

        private static void FlushCharacterFactoryPendingImports()
        {
            InvokeEditorProcessor(
                "VoxelEngine.Characters.Editor.CharacterFactoryAssetImporter, VoxelEngine.Characters.Editor",
                "ProcessPendingDescriptors",
                "Character Factory"
            );
        }

        private static void FlushPlaceholderAnimationPendingImports()
        {
            InvokeEditorProcessor(
                "VoxelGame.Editor.PlaceholderHumanoidAnimationConfigurator, PlaceholderHumanoids.Editor",
                "ProcessPendingPrefabs",
                "placeholder animation"
            );
        }

        private static void InvokeEditorProcessor(
            string assemblyQualifiedTypeName,
            string methodName,
            string description)
        {
            var importerType = System.Type.GetType(assemblyQualifiedTypeName);
            Assert.That(importerType, Is.Not.Null,
                $"Could not load the {description} editor importer type.");

            var processMethod = importerType.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(processMethod, Is.Not.Null,
                $"Could not locate {importerType.FullName}.{methodName}.");

            processMethod.Invoke(null, null);
        }
    }
}
