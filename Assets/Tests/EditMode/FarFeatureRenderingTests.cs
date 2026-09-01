using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Rendering.Runtime.FarWorld;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class FarFeatureRenderingTests
    {
        [Test]
        public void GenericContract_RepresentsStructureAndNaturalBakeThroughSameRenderer()
        {
            var structure = new FarFeatureInstance(
                0xCA57EUL,
                new float3(1200f, 80f, -400f),
                quaternion.identity,
                new float3(48f, 72f, 52f),
                new float3(1200f, 116f, -400f),
                new float3(24f, 36f, 26f),
                "baked-structure-massing",
                "stone",
                FarFeatureTier.Horizon,
                FarFeatureVisualFlags.Landmark);
            var natural = new FarFeatureInstance(
                0xB01DUL,
                new float3(-900f, 34f, 650f),
                quaternion.RotateY(math.radians(18f)),
                new float3(28f, 68f, 24f),
                new float3(-900f, 68f, 650f),
                new float3(14f, 34f, 12f),
                "baked-natural-massing",
                "granite",
                FarFeatureTier.Far);

            Assert.That(structure.GeometryKey, Is.EqualTo("baked-structure-massing"));
            Assert.That(natural.GeometryKey, Is.EqualTo("baked-natural-massing"));

            var go = new GameObject("generic-far-feature-renderer-test");
            try
            {
                var renderer = go.AddComponent<ProceduralFarFeatureRenderer>();
                renderer.SetInstances(new[] { structure, natural });

                Assert.That(renderer.InstanceCount, Is.EqualTo(2));
                Assert.That(renderer.PersistentInstanceObjectCount, Is.Zero);
                Assert.That(renderer.BatchKeyFor(structure), Is.Not.EqualTo(renderer.BatchKeyFor(natural)));
                Assert.That(go.transform.childCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
