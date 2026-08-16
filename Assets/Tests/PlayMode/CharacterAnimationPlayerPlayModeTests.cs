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
    }
}
