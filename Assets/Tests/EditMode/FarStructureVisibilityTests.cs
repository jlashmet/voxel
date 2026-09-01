using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Rendering.Runtime.FarWorld;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class FarStructureVisibilityTests
    {
        [Test]
        public void BatchKey_IsDeterministicForSameGeometryStyleAndTier()
        {
            var go = new GameObject("far-renderer-test");
            try
            {
                var renderer = go.AddComponent<ProceduralFarFeatureRenderer>();
                FarFeatureInstance first = Instance(1UL, "House", "stone", FarFeatureTier.Far);
                FarFeatureInstance second = Instance(99UL, "House", "stone", FarFeatureTier.Far);

                Assert.That(renderer.BatchKeyFor(first), Is.EqualTo(renderer.BatchKeyFor(second)));
                Assert.That(renderer.BatchKeyFor(first), Is.Not.EqualTo(
                    renderer.BatchKeyFor(Instance(1UL, "Castle", "stone", FarFeatureTier.Far))));
                Assert.That(renderer.BatchKeyFor(first), Is.Not.EqualTo(
                    renderer.BatchKeyFor(Instance(1UL, "House", "stone", FarFeatureTier.Horizon))));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SetInstances_StoresMatricesWithoutPerFeatureGameObjects()
        {
            var go = new GameObject("far-renderer-test");
            try
            {
                var renderer = go.AddComponent<ProceduralFarFeatureRenderer>();
                renderer.SetInstances(new[]
                {
                    Instance(1UL, "House", "timber", FarFeatureTier.Mid),
                    Instance(2UL, "House", "timber", FarFeatureTier.Mid),
                    Instance(3UL, "Castle", "stone", FarFeatureTier.Far),
                    Instance(4UL, "House", "timber", FarFeatureTier.Culled)
                });

                Assert.That(renderer.InstanceCount, Is.EqualTo(3));
                Assert.That(renderer.PersistentInstanceObjectCount, Is.Zero);
                Assert.That(go.transform.childCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static FarFeatureInstance Instance(
            ulong id,
            string geometry,
            string style,
            FarFeatureTier tier)
        {
            return new FarFeatureInstance(
                id,
                new float3(10f, 20f, 30f),
                quaternion.identity,
                new float3(12f, 8f, 10f),
                new float3(10f, 24f, 30f),
                new float3(6f, 4f, 5f),
                geometry,
                style,
                tier);
        }
    }
}
