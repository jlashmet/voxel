using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Rendering.Runtime.FarWorld;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class ProceduralFarFeatureShaderTests
    {
        [Test]
        public void ResolveMaterial_UsesBuildSafeSupportedShader()
        {
            var go = new GameObject("ProceduralFarFeatureShaderTests");
            try
            {
                var renderer = go.AddComponent<ProceduralFarFeatureRenderer>();
                var geometry = new FarFeatureGeometry(new[]
                {
                    new FarFeatureGeometryPrimitive(
                        FarFeatureGeometryShape.Box,
                        new float3(-0.5f, 0f, -0.5f),
                        new float3(0.5f, 1f, 0.5f)),
                });
                var instance = new FarFeatureInstance(
                    stableId: 1ul,
                    position: float3.zero,
                    rotation: quaternion.identity,
                    scale: new float3(1f),
                    boundsCenter: new float3(0f, 0.5f, 0f),
                    boundsExtents: new float3(0.5f),
                    geometryKey: "shader-regression",
                    styleKey: "shader-regression",
                    tier: FarFeatureTier.Mid,
                    geometry: geometry,
                    materialIndex: 0);

                Material material = renderer.ResolveMaterial(instance);

                Assert.That(material.shader, Is.Not.Null);
                Assert.That(material.shader.name, Is.EqualTo(ProceduralFarFeatureRenderer.FarFeatureShaderName));
                Assert.That(material.shader.isSupported, Is.True);
                Assert.That(material.enableInstancing, Is.True);
                Assert.That(material.HasProperty("_BaseColor"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
