using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class PlaceholderHumanoidRetargetingTests
    {
        [TestCase("Assets/ThirdParty/PlaceholderHumanoids/Animations/Idle.fbx")]
        [TestCase("Assets/ThirdParty/PlaceholderHumanoids/Animations/Walk.fbx")]
        [TestCase("Assets/ThirdParty/PlaceholderHumanoids/Animations/Run.fbx")]
        [TestCase("Assets/ThirdParty/PlaceholderHumanoids/Animations/CrouchIdle.fbx")]
        [TestCase("Assets/ThirdParty/PlaceholderHumanoids/Animations/Wave.fbx")]
        [TestCase("Assets/ThirdParty/PlaceholderHumanoids/Animations/Shrug.fbx")]
        public void AnimationClip_IsHumanoidMotion(string path)
        {
            var clips = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__"))
                .ToArray();

            Assert.That(clips.Length, Is.GreaterThanOrEqualTo(1), $"{path} exposes no animation clip");
            Assert.That(clips.All(clip => clip.humanMotion), Is.True,
                $"{path} contains motion that Unity cannot retarget through a Humanoid Avatar");
        }

        [TestCase("Assets/ThirdParty/PlaceholderHumanoids/Models/Male_Adult_01.fbx")]
        [TestCase("Assets/ThirdParty/PlaceholderHumanoids/Models/Female_Adult_01.fbx")]
        public void PlaceholderBody_ProvidesRetargetableHumanoidAvatar(string path)
        {
            var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();

            Assert.That(avatar, Is.Not.Null, $"{path} did not generate an Avatar");
            Assert.That(avatar.isValid, Is.True, $"{path} generated an invalid Avatar");
            Assert.That(avatar.isHuman, Is.True, $"{path} is not a Humanoid Avatar");
        }
    }
}
