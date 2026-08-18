using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using VoxelEngine.Characters.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class PlaceholderHumanoidPlaybackTests
    {
        private const string Root = "Assets/ThirdParty/PlaceholderHumanoids";
        private const string MalePath = Root + "/Models/Male_Adult_01.fbx";
        private const string FemalePath = Root + "/Models/Female_Adult_01.fbx";
        private const string AnimationRoot = Root + "/Animations/";
        private const string WalkPath = AnimationRoot + "Walk.fbx";

        [TestCase(MalePath)]
        [TestCase(FemalePath)]
        public void WalkClip_DrivesRetargetedHumanoidPose(string bodyPath)
        {
            var body = AssetDatabase.LoadAssetAtPath<GameObject>(bodyPath);
            Assert.That(body, Is.Not.Null, $"Unity could not load placeholder body {bodyPath}");

            var walk = LoadClip("Walk");
            Assert.That(walk.humanMotion, Is.True, "Walk must remain Humanoid motion for retargeting");
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

                // Prototype movement is character-controller driven. This playback test only
                // proves that the shared Humanoid clip retargets and animates the body pose.
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

        [Test]
        public void RuntimePolicy_PreservesRealClipIntentAcrossMaleFemaleVisualSwaps()
        {
            var male = AssetDatabase.LoadAssetAtPath<GameObject>(MalePath);
            var female = AssetDatabase.LoadAssetAtPath<GameObject>(FemalePath);
            Assert.That(male, Is.Not.Null, $"Unity could not load placeholder body {MalePath}");
            Assert.That(female, Is.Not.Null, $"Unity could not load placeholder body {FemalePath}");

            var idle = LoadClip("Idle");
            var walk = LoadClip("Walk");
            var run = LoadClip("Run");
            var crouchIdle = LoadClip("CrouchIdle");
            var wave = LoadClip("Wave");

            var host = new GameObject("placeholder-runtime-policy");
            CharacterAnimationPlayer player = null;
            try
            {
                var resolver = host.AddComponent<CharacterVisualResolver>();
                player = host.AddComponent<CharacterAnimationPlayer>();
                var policy = host.AddComponent<CharacterAnimationPolicy>();

                player.SetVisualResolver(resolver);
                resolver.SetFallbackVisual(male);
                policy.ConfigureLocomotion(idle, walk, run, crouchIdle);

                Assert.That(policy.SetLocomotion(CharacterLocomotionState.Walk), Is.True);
                Assert.That(player.CurrentClip, Is.SameAs(walk));
                AssertHumanoidTarget(player.Animator, male.name);

                resolver.SetPreferredVisual(female);

                Assert.That(player.CurrentClip, Is.SameAs(walk),
                    "Swapping male to female lost the active Walk intent");
                AssertHumanoidTarget(player.Animator, female.name);

                Assert.That(policy.PlayOneShot(wave), Is.True);
                Assert.That(policy.ActiveOneShot, Is.SameAs(wave));
                Assert.That(player.CurrentClip, Is.SameAs(wave));

                resolver.SetPreferredVisual(null);

                Assert.That(player.CurrentClip, Is.SameAs(wave),
                    "Swapping back to the fallback body lost the active Wave intent");
                AssertHumanoidTarget(player.Animator, male.name);

                Assert.That(policy.CancelOneShot(), Is.True);
                Assert.That(policy.ActiveOneShot, Is.Null);
                Assert.That(player.CurrentClip, Is.SameAs(walk),
                    "Canceling the real Wave clip did not return to the queued Walk state");
            }
            finally
            {
                if (player != null)
                {
                    player.Stop();
                }

                Object.DestroyImmediate(host);
            }
        }

        private static AnimationClip LoadClip(string semanticName)
        {
            string path = AnimationRoot + semanticName + ".fbx";
            var clip = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .FirstOrDefault(candidate => candidate.name == semanticName);
            Assert.That(clip, Is.Not.Null,
                $"The semantic {semanticName} clip was not imported from {path}");
            return clip;
        }

        private static void AssertHumanoidTarget(Animator animator, string expectedSourceName)
        {
            Assert.That(animator, Is.Not.Null,
                $"Animation player has no Animator after resolving {expectedSourceName}");
            Assert.That(animator.avatar, Is.Not.Null,
                $"Resolved {expectedSourceName} Animator has no Avatar");
            Assert.That(animator.avatar.isValid, Is.True,
                $"Resolved {expectedSourceName} Animator Avatar is invalid");
            Assert.That(animator.avatar.isHuman, Is.True,
                $"Resolved {expectedSourceName} Animator Avatar is not Humanoid");
        }
    }
}
