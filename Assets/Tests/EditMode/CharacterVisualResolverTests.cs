using NUnit.Framework;
using UnityEngine;
using VoxelEngine.Characters.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CharacterVisualResolverTests
    {
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
    }
}
