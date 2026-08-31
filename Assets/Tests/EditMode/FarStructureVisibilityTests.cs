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
        public void BatchKey_IsDeterministicForSameSemanticProxyStyleAndTier()
        {
            var go = new GameObject("far-renderer-test");
            try
            {
                var renderer = go.AddComponent<ProceduralFarStructureRenderer>();
                FarStructureInstance first = Instance(1UL, "House", "stone", FarStructureTier.Far);
                FarStructureInstance second = Instance(99UL, "House", "stone", FarStructureTier.Far);

                Assert.That(renderer.BatchKeyFor(first), Is.EqualTo(renderer.BatchKeyFor(second)));
                Assert.That(renderer.BatchKeyFor(first), Is.Not.EqualTo(
                    renderer.BatchKeyFor(Instance(1UL, "Castle", "stone", FarStructureTier.Far))));
                Assert.That(renderer.BatchKeyFor(first), Is.Not.EqualTo(
                    renderer.BatchKeyFor(Instance(1UL, "House", "stone", FarStructureTier.Horizon))));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SetInstances_StoresMatricesWithoutPerStructureGameObjects()
        {
            var go = new GameObject("far-renderer-test");
            try
            {
                var renderer = go.AddComponent<ProceduralFarStructureRenderer>();
                renderer.SetInstances(new[]
                {
                    Instance(1UL, "House", "timber", FarStructureTier.Mid),
                    Instance(2UL, "House", "timber", FarStructureTier.Mid),
                    Instance(3UL, "Castle", "stone", FarStructureTier.Far),
                    Instance(4UL, "House", "timber", FarStructureTier.Culled)
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

        private static FarStructureInstance Instance(
            ulong id,
            string proxy,
            string style,
            FarStructureTier tier)
        {
            return new FarStructureInstance(
                id,
                new float3(10f, 20f, 30f),
                quaternion.identity,
                new float3(12f, 8f, 10f),
                new float3(10f, 24f, 30f),
                new float3(6f, 4f, 5f),
                proxy,
                style,
                tier);
        }
    }
}
