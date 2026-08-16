using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class PlaceholderHumanoidPlaybackTests
    {
        private const string MalePath = "Assets/ThirdParty/PlaceholderHumanoids/Models/Male_Adult_01.fbx";
        private const string FemalePath = "Assets/ThirdParty/PlaceholderHumanoids/Models/Female_Adult_01.fbx";
        private const string WalkPath = "Assets/ThirdParty/PlaceholderHumanoids/Animations/Walk.fbx";

        [TestCase(MalePath)]
        [TestCase(FemalePath)]
        public void WalkClip_DrivesRetargetedHumanoidPose(string bodyPath)
        {
            var body = AssetDatabase.LoadAssetAtPath<GameObject>(bodyPath);
            Assert.That(body, Is.Not.Null, $"Unity could not load placeholder body {bodyPath}");

            var walk = AssetDatabase.LoadAllAssetsAtPath(WalkPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(clip => clip.name == "Walk");
            Assert.That(walk, Is.Not.Null, "The semantic Walk clip was not imported from the placeholder animation FBX");
            Assert.That(walk.isHumanMotion, Is.True, "Walk must remain Humanoid motion for retargeting");
            Assert.That(walk.length, Is.GreaterThan(0.05f), "Walk clip is too short to exercise a retargeted pose");

            var instance = Object.Instantiate(body);
            var graph = PlayableGraph.Create($"Placeholder retarget test - {body.name}");

            try
            {
                var animator = instance.GetComponentInChildren<Animator>(true);
                Assert.That(animator, Is.Not.Null, $"{bodyPath} contains no Animator");
                Assert.That(animator.avatar, Is.Not.Null, $"{bodyPath} Animator has no Avatar");
                Assert.That(animator.avatar.isValid, Is.True, $"{bodyPath} Animator Avatar is invalid");
                Assert.That(animator.avatar.isHuman, Is.True, $"{bodyPath} Animator Avatar is not Humanoid");

                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.applyRootMotion = false;
                animator.Rebind();

                var upperLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                Assert.That(upperLeg, Is.Not.Null, $"{bodyPath} has no mapped LeftUpperLeg Humanoid bone");

                var output = AnimationPlayableOutput.Create(graph, "Walk", animator);
                var playable = AnimationClipPlayable.Create(graph, walk);
                output.SetSourcePlayable(playable);
                graph.Play();

                playable.SetTime(0d);
                graph.Evaluate(0f);
                var startRotation = upperLeg.localRotation;

                playable.SetTime(walk.length * 0.5d);
                graph.Evaluate(0f);
                var middleRotation = upperLeg.localRotation;

                Assert.That(
                    Quaternion.Angle(startRotation, middleRotation),
                    Is.GreaterThan(0.1f),
                    $"Walk did not change the retargeted LeftUpperLeg pose on {bodyPath}");
            }
            finally
            {
                if (graph.IsValid())
                {
                    graph.Destroy();
                }

                Object.DestroyImmediate(instance);
            }
        }
    }
}
