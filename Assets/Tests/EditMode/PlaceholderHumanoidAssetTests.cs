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
        private const string AnimationPath = "Assets/ThirdParty/PlaceholderHumanoids/Animations/KayKit_Knight_AnimationLibrary.fbx";

        [TestCase(MalePath, false)]
        [TestCase(FemalePath, false)]
        [TestCase(AnimationPath, true)]
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
        public void PlaceholderBody_LoadsAsRiggedHumanoid(string path)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(model, Is.Not.Null, $"Unity could not load placeholder body {path}");

            var skinnedMeshes = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Assert.That(skinnedMeshes.Length, Is.GreaterThan(0), $"{path} has no skinned mesh renderer");

            var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            Assert.That(avatar, Is.Not.Null, $"{path} did not generate a Humanoid Avatar");
            Assert.That(avatar.isValid, Is.True, $"{path} generated an invalid Avatar");
            Assert.That(avatar.isHuman, Is.True, $"{path} did not generate a Humanoid Avatar");
        }

        [Test]
        public void AnimationLibrary_ContainsRetargetableClips()
        {
            var avatar = AssetDatabase.LoadAllAssetsAtPath(AnimationPath).OfType<Avatar>().FirstOrDefault();
            Assert.That(avatar, Is.Not.Null, "Animation library did not generate a Humanoid Avatar");
            Assert.That(avatar.isValid, Is.True);
            Assert.That(avatar.isHuman, Is.True);

            var clips = AssetDatabase.LoadAllAssetsAtPath(AnimationPath)
                .OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__"))
                .ToArray();

            Assert.That(clips.Length, Is.GreaterThanOrEqualTo(10),
                "Expected the temporary animation library to expose a useful set of clips");
        }
    }
}
