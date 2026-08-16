using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.Characters.Runtime;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class CharacterAnimationPlayerPlayModeTests
    {
        [UnityTest]
        public IEnumerator Play_WithoutAnimator_ReturnsFalse()
        {
            var host = new GameObject("character");
            var clip = new AnimationClip { name = "test-clip" };
            var player = host.AddComponent<CharacterAnimationPlayer>();

            Assert.That(player.Play(clip), Is.False);
            Assert.That(player.CurrentClip, Is.Null);
            Assert.That(player.IsPlaying, Is.False);

            Object.Destroy(host);
            Object.Destroy(clip);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Play_FindsChildAnimatorAndTracksCurrentClip()
        {
            var host = new GameObject("character");
            var visual = new GameObject("visual");
            visual.transform.SetParent(host.transform, false);
            var animator = visual.AddComponent<Animator>();
            var clip = new AnimationClip { name = "test-clip" };
            var player = host.AddComponent<CharacterAnimationPlayer>();

            Assert.That(player.Play(clip), Is.True);
            Assert.That(player.Animator, Is.SameAs(animator));
            Assert.That(player.CurrentClip, Is.SameAs(clip));
            Assert.That(player.IsPlaying, Is.True);

            yield return null;

            Assert.That(player.IsPlaying, Is.True);

            player.Stop();
            Assert.That(player.IsPlaying, Is.False);
            Assert.That(player.CurrentClip, Is.Null);

            Object.Destroy(host);
            Object.Destroy(clip);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SetAnimator_StopsCurrentGraphAndUsesReplacementTarget()
        {
            var host = new GameObject("character");
            var firstVisual = new GameObject("first-visual");
            var secondVisual = new GameObject("second-visual");
            var firstAnimator = firstVisual.AddComponent<Animator>();
            var secondAnimator = secondVisual.AddComponent<Animator>();
            var firstClip = new AnimationClip { name = "first" };
            var secondClip = new AnimationClip { name = "second" };
            var player = host.AddComponent<CharacterAnimationPlayer>();

            player.SetAnimator(firstAnimator);
            Assert.That(player.Play(firstClip), Is.True);
            Assert.That(player.IsPlaying, Is.True);

            player.SetAnimator(secondAnimator);

            Assert.That(player.Animator, Is.SameAs(secondAnimator));
            Assert.That(player.IsPlaying, Is.False);
            Assert.That(player.CurrentClip, Is.Null);

            Assert.That(player.Play(secondClip), Is.True);
            Assert.That(player.CurrentClip, Is.SameAs(secondClip));

            Object.Destroy(host);
            Object.Destroy(firstVisual);
            Object.Destroy(secondVisual);
            Object.Destroy(firstClip);
            Object.Destroy(secondClip);
            yield return null;
        }

        [UnityTest]
        public IEnumerator VisualResolverSwap_RetargetsAnimatorAndPreservesCurrentClip()
        {
            var host = new GameObject("character");
            var fallback = new GameObject("fallback");
            var preferred = new GameObject("preferred");
            fallback.AddComponent<Animator>();
            preferred.AddComponent<Animator>();
            var clip = new AnimationClip { name = "locomotion" };

            var resolver = host.AddComponent<CharacterVisualResolver>();
            var player = host.AddComponent<CharacterAnimationPlayer>();

            resolver.SetFallbackVisual(fallback);
            Animator fallbackAnimator = resolver.CurrentVisual.GetComponent<Animator>();

            Assert.That(player.VisualResolver, Is.SameAs(resolver));
            Assert.That(player.Animator, Is.SameAs(fallbackAnimator));
            Assert.That(player.Play(clip), Is.True);
            Assert.That(player.IsPlaying, Is.True);

            resolver.SetPreferredVisual(preferred);
            Animator preferredAnimator = resolver.CurrentVisual.GetComponent<Animator>();

            Assert.That(preferredAnimator, Is.Not.SameAs(fallbackAnimator));
            Assert.That(player.Animator, Is.SameAs(preferredAnimator));
            Assert.That(player.CurrentClip, Is.SameAs(clip));
            Assert.That(player.IsPlaying, Is.True);

            Object.Destroy(host);
            Object.Destroy(fallback);
            Object.Destroy(preferred);
            Object.Destroy(clip);
            yield return null;
        }

        [UnityTest]
        public IEnumerator VisualResolverSwap_PreservesPlaybackTime()
        {
            var host = new GameObject("character");
            var fallback = new GameObject("fallback");
            var preferred = new GameObject("preferred");
            fallback.AddComponent<Animator>();
            preferred.AddComponent<Animator>();
            var clip = CreateTimedClip("locomotion", 1f);

            var resolver = host.AddComponent<CharacterVisualResolver>();
            var player = host.AddComponent<CharacterAnimationPlayer>();
            resolver.SetFallbackVisual(fallback);
            Assert.That(player.Play(clip), Is.True);

            for (var frame = 0; frame < 10 && player.CurrentTime <= 0.001d; frame++)
            {
                yield return null;
            }

            double beforeSwap = player.CurrentTime;
            Assert.That(beforeSwap, Is.GreaterThan(0.001d),
                "The timed clip never advanced before the visual swap");

            resolver.SetPreferredVisual(preferred);

            Assert.That(player.CurrentClip, Is.SameAs(clip));
            Assert.That(player.CurrentTime, Is.EqualTo(beforeSwap).Within(0.01d),
                "Visual replacement restarted the active animation instead of preserving playback time");
            Assert.That(player.IsPlaying, Is.True);

            Object.Destroy(host);
            Object.Destroy(fallback);
            Object.Destroy(preferred);
            Object.Destroy(clip);
            yield return null;
        }

        [UnityTest]
        public IEnumerator VisualResolverWithoutVisual_KeepsClipIntentUntilVisualReturns()
        {
            var host = new GameObject("character");
            var fallback = new GameObject("fallback");
            var replacement = new GameObject("replacement");
            fallback.AddComponent<Animator>();
            replacement.AddComponent<Animator>();
            var clip = new AnimationClip { name = "idle" };

            var resolver = host.AddComponent<CharacterVisualResolver>();
            var player = host.AddComponent<CharacterAnimationPlayer>();

            resolver.SetFallbackVisual(fallback);
            Assert.That(player.Play(clip), Is.True);

            resolver.SetFallbackVisual(null);

            Assert.That(player.Animator, Is.Null);
            Assert.That(player.CurrentClip, Is.SameAs(clip));
            Assert.That(player.IsPlaying, Is.False);

            resolver.SetFallbackVisual(replacement);

            Assert.That(player.Animator, Is.SameAs(resolver.CurrentVisual.GetComponent<Animator>()));
            Assert.That(player.CurrentClip, Is.SameAs(clip));
            Assert.That(player.IsPlaying, Is.True);

            Object.Destroy(host);
            Object.Destroy(fallback);
            Object.Destroy(replacement);
            Object.Destroy(clip);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DisabledPlayer_RebindsLatestVisualWhenReenabled()
        {
            var host = new GameObject("character");
            var fallback = new GameObject("fallback");
            var preferred = new GameObject("preferred");
            fallback.AddComponent<Animator>();
            preferred.AddComponent<Animator>();

            var resolver = host.AddComponent<CharacterVisualResolver>();
            var player = host.AddComponent<CharacterAnimationPlayer>();

            resolver.SetFallbackVisual(fallback);
            Animator fallbackAnimator = resolver.CurrentVisual.GetComponent<Animator>();
            Assert.That(player.Animator, Is.SameAs(fallbackAnimator));

            player.enabled = false;
            resolver.SetPreferredVisual(preferred);
            Animator preferredAnimator = resolver.CurrentVisual.GetComponent<Animator>();

            Assert.That(player.Animator, Is.SameAs(fallbackAnimator));

            player.enabled = true;

            Assert.That(player.Animator, Is.SameAs(preferredAnimator));

            Object.Destroy(host);
            Object.Destroy(fallback);
            Object.Destroy(preferred);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AnimationPolicy_SelectsConfiguredLocomotionClips()
        {
            var host = new GameObject("character");
            host.AddComponent<Animator>();
            var idle = new AnimationClip { name = "Idle" };
            var walk = new AnimationClip { name = "Walk" };
            var run = new AnimationClip { name = "Run" };
            var crouch = new AnimationClip { name = "CrouchIdle" };

            var player = host.AddComponent<CharacterAnimationPlayer>();
            var policy = host.AddComponent<CharacterAnimationPolicy>();
            policy.ConfigureLocomotion(idle, walk, run, crouch);

            Assert.That(player.CurrentClip, Is.SameAs(idle));
            Assert.That(policy.SetLocomotion(CharacterLocomotionState.Walk), Is.True);
            Assert.That(player.CurrentClip, Is.SameAs(walk));
            Assert.That(policy.SetLocomotion(CharacterLocomotionState.Run), Is.True);
            Assert.That(player.CurrentClip, Is.SameAs(run));
            Assert.That(policy.SetLocomotion(CharacterLocomotionState.CrouchIdle), Is.True);
            Assert.That(player.CurrentClip, Is.SameAs(crouch));

            Object.Destroy(host);
            Object.Destroy(idle);
            Object.Destroy(walk);
            Object.Destroy(run);
            Object.Destroy(crouch);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AnimationPolicy_OneShotReturnsToLatestLocomotion()
        {
            var host = new GameObject("character");
            host.AddComponent<Animator>();
            var idle = new AnimationClip { name = "Idle" };
            var walk = new AnimationClip { name = "Walk" };
            var run = new AnimationClip { name = "Run" };
            var oneShot = new AnimationClip { name = "Wave" };

            var player = host.AddComponent<CharacterAnimationPlayer>();
            var policy = host.AddComponent<CharacterAnimationPolicy>();
            policy.ConfigureLocomotion(idle, walk, run);
            Assert.That(policy.SetLocomotion(CharacterLocomotionState.Walk), Is.True);
            Assert.That(policy.PlayOneShot(oneShot), Is.True);
            Assert.That(player.CurrentClip, Is.SameAs(oneShot));

            Assert.That(policy.SetLocomotion(CharacterLocomotionState.Run), Is.True);
            Assert.That(player.CurrentClip, Is.SameAs(oneShot),
                "Changing locomotion interrupted the active one-shot");

            yield return null;

            Assert.That(policy.ActiveOneShot, Is.Null);
            Assert.That(policy.LocomotionState, Is.EqualTo(CharacterLocomotionState.Run));
            Assert.That(player.CurrentClip, Is.SameAs(run));

            Object.Destroy(host);
            Object.Destroy(idle);
            Object.Destroy(walk);
            Object.Destroy(run);
            Object.Destroy(oneShot);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AnimationPolicy_OneShotSurvivesVisualSwapThenReturnsToLocomotion()
        {
            var host = new GameObject("character");
            var fallback = new GameObject("fallback");
            var preferred = new GameObject("preferred");
            fallback.AddComponent<Animator>();
            preferred.AddComponent<Animator>();
            var idle = new AnimationClip { name = "Idle" };
            var walk = new AnimationClip { name = "Walk" };
            var run = new AnimationClip { name = "Run" };
            var oneShot = new AnimationClip { name = "Shrug" };

            var resolver = host.AddComponent<CharacterVisualResolver>();
            var player = host.AddComponent<CharacterAnimationPlayer>();
            var policy = host.AddComponent<CharacterAnimationPolicy>();
            resolver.SetFallbackVisual(fallback);
            policy.ConfigureLocomotion(idle, walk, run);
            Assert.That(policy.SetLocomotion(CharacterLocomotionState.Walk), Is.True);
            Assert.That(policy.PlayOneShot(oneShot), Is.True);

            resolver.SetPreferredVisual(preferred);
            Animator replacementAnimator = resolver.CurrentVisual.GetComponent<Animator>();

            Assert.That(player.Animator, Is.SameAs(replacementAnimator));
            Assert.That(policy.ActiveOneShot, Is.SameAs(oneShot));
            Assert.That(player.CurrentClip, Is.SameAs(oneShot));
            Assert.That(player.IsPlaying, Is.True);

            yield return null;

            Assert.That(policy.ActiveOneShot, Is.Null);
            Assert.That(player.CurrentClip, Is.SameAs(walk));

            Object.Destroy(host);
            Object.Destroy(fallback);
            Object.Destroy(preferred);
            Object.Destroy(idle);
            Object.Destroy(walk);
            Object.Destroy(run);
            Object.Destroy(oneShot);
            yield return null;
        }

        private static AnimationClip CreateTimedClip(string name, float length)
        {
            var clip = new AnimationClip { name = name };
            clip.SetCurve(
                string.Empty,
                typeof(Transform),
                "localPosition.x",
                AnimationCurve.Linear(0f, 0f, length, 0f));
            return clip;
        }
    }
}
