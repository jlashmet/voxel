using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.Characters.Runtime;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class CharacterVisualResolverPlayModeTests
    {
        [UnityTest]
        public IEnumerator DestroyingResolverHost_CleansOwnedVisualOutsideHostHierarchy()
        {
            var host = new GameObject("character");
            var externalRoot = new GameObject("external-visual-root");
            var fallback = new GameObject("fallback");

            var resolver = host.AddComponent<CharacterVisualResolver>();
            resolver.VisualRoot = externalRoot.transform;
            resolver.SetFallbackVisual(fallback);
            GameObject instance = resolver.CurrentVisual;

            Assert.That(instance, Is.Not.Null);
            Assert.That(externalRoot.transform.childCount, Is.EqualTo(1));

            Object.Destroy(host);
            yield return null;

            Assert.That(instance == null, Is.True,
                "Destroying the resolver host left its externally-parented visual alive");
            Assert.That(externalRoot.transform.childCount, Is.EqualTo(0));

            Object.Destroy(externalRoot);
            Object.Destroy(fallback);
        }
    }
}
