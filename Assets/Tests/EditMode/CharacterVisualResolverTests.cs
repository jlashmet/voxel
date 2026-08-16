using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VoxelEngine.Characters.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CharacterVisualResolverTests
    {
        private const string MalePath = "Assets/ThirdParty/PlaceholderHumanoids/Models/Male_Adult_01.fbx";
        private const string FemalePath = "Assets/ThirdParty/PlaceholderHumanoids/Models/Female_Adult_01.fbx";

        [Test]
        public void ResolveVisual_UsesFallbackWhenPreferredIsUnavailable()
        {
            var host = new GameObject("character");
            var fallback = new GameObject("fallback");

            try
            {
                var resolver = host.AddComponent<CharacterVisualResolver>();
                resolver.FallbackVisualPrefab = fallback;

                GameObject instance = resolver.ResolveVisual();

                Assert.That(instance, Is.Not.Null);
                Assert.That(instance, Is.Not.SameAs(fallback));
                Assert.That(resolver.CurrentSourcePrefab, Is.SameAs(fallback));
                Assert.That(instance.transform.parent, Is.SameAs(host.transform));
                Assert.That(instance.transform.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(instance.transform.localRotation, Is.EqualTo(Quaternion.identity));
                Assert.That(instance.transform.localScale, Is.EqualTo(Vector3.one));
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(fallback);
            }
        }

        [Test]
        public void ResolveVisual_PrefersGeneratedVisualOverFallback()
        {
            var host = new GameObject("character");
            var fallback = new GameObject("fallback");
            var preferred = new GameObject("generated");

            try
            {
                var resolver = host.AddComponent<CharacterVisualResolver>();
                resolver.FallbackVisualPrefab = fallback;
                resolver.PreferredVisualPrefab = preferred;

                GameObject instance = resolver.ResolveVisual();

                Assert.That(instance, Is.Not.Null);
                Assert.That(instance.name, Is.EqualTo(preferred.name));
                Assert.That(resolver.CurrentSourcePrefab, Is.SameAs(preferred));
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(fallback);
                Object.DestroyImmediate(preferred);
            }
        }

        [Test]
        public void SetPreferredVisual_ReplacesOwnedFallbackInstance()
        {
            var host = new GameObject("character");
            var fallback = new GameObject("fallback");
            var preferred = new GameObject("generated");

            try
            {
                var resolver = host.AddComponent<CharacterVisualResolver>();
                resolver.SetFallbackVisual(fallback);
                GameObject first = resolver.CurrentVisual;

                resolver.SetPreferredVisual(preferred);

                Assert.That(first == null, Is.True, "The resolver left its old fallback instance alive");
                Assert.That(resolver.CurrentVisual, Is.Not.Null);
                Assert.That(resolver.CurrentSourcePrefab, Is.SameAs(preferred));
                Assert.That(host.transform.childCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(fallback);
                Object.DestroyImmediate(preferred);
            }
        }

        [Test]
        public void ResolveVisual_WithNoSource_ClearsOwnedVisual()
        {
            var host = new GameObject("character");
            var fallback = new GameObject("fallback");

            try
            {
                var resolver = host.AddComponent<CharacterVisualResolver>();
                resolver.SetFallbackVisual(fallback);
                Assert.That(resolver.CurrentVisual, Is.Not.Null);

                resolver.SetFallbackVisual(null);

                Assert.That(resolver.CurrentVisual, Is.Null);
                Assert.That(resolver.CurrentSourcePrefab, Is.Null);
                Assert.That(host.transform.childCount, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(fallback);
            }
        }

        [Test]
        public void ResolveVisual_UsesConfiguredVisualRootAndNormalizesLocalTransform()
        {
            var host = new GameObject("character");
            var rootObject = new GameObject("visual-root");
            var fallback = new GameObject("fallback");

            try
            {
                rootObject.transform.SetParent(host.transform, false);
                fallback.transform.localPosition = new Vector3(4f, 5f, 6f);
                fallback.transform.localRotation = Quaternion.Euler(10f, 20f, 30f);
                fallback.transform.localScale = new Vector3(2f, 3f, 4f);

                var resolver = host.AddComponent<CharacterVisualResolver>();
                resolver.VisualRoot = rootObject.transform;
                resolver.FallbackVisualPrefab = fallback;

                GameObject instance = resolver.ResolveVisual();

                Assert.That(instance.transform.parent, Is.SameAs(rootObject.transform));
                Assert.That(instance.transform.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(instance.transform.localRotation, Is.EqualTo(Quaternion.identity));
                Assert.That(instance.transform.localScale, Is.EqualTo(Vector3.one));
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(fallback);
            }
        }

        [Test]
        public void ResolveVisual_WhenVisualRootChanges_ReparentsOwnedInstanceWithoutReinstantiating()
        {
            var host = new GameObject("character");
            var firstRoot = new GameObject("first-root");
            var secondRoot = new GameObject("second-root");
            var fallback = new GameObject("fallback");

            try
            {
                firstRoot.transform.SetParent(host.transform, false);
                secondRoot.transform.SetParent(host.transform, false);

                var resolver = host.AddComponent<CharacterVisualResolver>();
                resolver.VisualRoot = firstRoot.transform;
                resolver.FallbackVisualPrefab = fallback;
                GameObject first = resolver.ResolveVisual();

                resolver.VisualRoot = secondRoot.transform;
                GameObject reused = resolver.ResolveVisual();

                Assert.That(reused, Is.SameAs(first));
                Assert.That(reused.transform.parent, Is.SameAs(secondRoot.transform));
                Assert.That(firstRoot.transform.childCount, Is.EqualTo(0));
                Assert.That(secondRoot.transform.childCount, Is.EqualTo(1));
                Assert.That(reused.transform.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(reused.transform.localRotation, Is.EqualTo(Quaternion.identity));
                Assert.That(reused.transform.localScale, Is.EqualTo(Vector3.one));
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(fallback);
            }
        }

        [Test]
        public void ResolveVisual_RocketboxFallbackCanSwapToAnotherHumanoidVisual()
        {
            var male = AssetDatabase.LoadAssetAtPath<GameObject>(MalePath);
            var female = AssetDatabase.LoadAssetAtPath<GameObject>(FemalePath);
            Assert.That(male, Is.Not.Null, $"Unity could not load {MalePath}");
            Assert.That(female, Is.Not.Null, $"Unity could not load {FemalePath}");

            var host = new GameObject("character");
            try
            {
                var resolver = host.AddComponent<CharacterVisualResolver>();
                resolver.SetFallbackVisual(male);
                GameObject maleInstance = resolver.CurrentVisual;

                AssertResolvedHumanoid(maleInstance, male);

                resolver.SetPreferredVisual(female);

                Assert.That(maleInstance == null, Is.True,
                    "The resolver left the old Rocketbox fallback instance alive after the preferred visual arrived");
                Assert.That(resolver.CurrentSourcePrefab, Is.SameAs(female));
                AssertResolvedHumanoid(resolver.CurrentVisual, female);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static void AssertResolvedHumanoid(GameObject instance, GameObject source)
        {
            Assert.That(instance, Is.Not.Null);
            Assert.That(instance, Is.Not.SameAs(source));
            Assert.That(instance.name, Is.EqualTo(source.name));
            Assert.That(instance.GetComponentInChildren<SkinnedMeshRenderer>(true), Is.Not.Null,
                $"Resolved visual {source.name} has no skinned mesh");

            var animator = instance.GetComponentInChildren<Animator>(true);
            Assert.That(animator, Is.Not.Null, $"Resolved visual {source.name} has no Animator");
            Assert.That(animator.avatar, Is.Not.Null, $"Resolved visual {source.name} has no Avatar");
            Assert.That(animator.avatar.isValid, Is.True, $"Resolved visual {source.name} has an invalid Avatar");
            Assert.That(animator.avatar.isHuman, Is.True, $"Resolved visual {source.name} is not Humanoid");
        }
    }
}
