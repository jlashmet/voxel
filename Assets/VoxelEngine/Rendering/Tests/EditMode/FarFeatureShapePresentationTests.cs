using System;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Rendering.Runtime.FarWorld;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class FarFeatureShapePresentationTests
    {
        [Test]
        public void ResolveMesh_FrustumPreservesAuthoredTaper()
        {
            var go = new GameObject("FarFeatureShapePresentationTests");
            try
            {
                var renderer = go.AddComponent<ProceduralFarFeatureRenderer>();
                var geometry = new FarFeatureGeometry(new[]
                {
                    new FarFeatureGeometryPrimitive(
                        FarFeatureGeometryShape.Frustum,
                        new float3(-0.5f, 0f, -0.5f),
                        new float3(0.5f, 1f, 0.5f),
                        axis: 1,
                        startRadiusScale: 1f,
                        endRadiusScale: 0.25f),
                });
                var instance = InstanceFor(geometry, materialIndex: 0);

                Mesh mesh = renderer.ResolveMesh(instance);
                Vector3[] vertices = mesh.vertices;

                Assert.That(vertices.Length, Is.GreaterThanOrEqualTo(24));
                Assert.That(RadialExtent(vertices, 0, 12), Is.EqualTo(0.5f).Within(0.001f));
                Assert.That(RadialExtent(vertices, 12, 12), Is.EqualTo(0.125f).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ResolveMaterial_UsesInstalledOpaqueMaterialIndex()
        {
            var go = new GameObject("FarFeatureMaterialPresentationTests");
            try
            {
                var expected = new float4(0.18f, 0.32f, 0.46f, 1f);
                VoxelMaterialPresentationInstaller.Apply(new[]
                {
                    new MaterialPresentationDefinition(7, expected),
                });

                var renderer = go.AddComponent<ProceduralFarFeatureRenderer>();
                var geometry = new FarFeatureGeometry(new[]
                {
                    new FarFeatureGeometryPrimitive(
                        FarFeatureGeometryShape.Box,
                        new float3(-0.5f, 0f, -0.5f),
                        new float3(0.5f, 1f, 0.5f)),
                });
                Material material = renderer.ResolveMaterial(InstanceFor(geometry, materialIndex: 7));
                Color actual = material.HasProperty("_BaseColor")
                    ? material.GetColor("_BaseColor")
                    : material.color;

                Assert.That(actual.r, Is.EqualTo(expected.x).Within(0.001f));
                Assert.That(actual.g, Is.EqualTo(expected.y).Within(0.001f));
                Assert.That(actual.b, Is.EqualTo(expected.z).Within(0.001f));
                Assert.That(actual.a, Is.EqualTo(1f).Within(0.001f));
            }
            finally
            {
                VoxelMaterialPresentationInstaller.Apply(Array.Empty<MaterialPresentationDefinition>());
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static FarFeatureInstance InstanceFor(FarFeatureGeometry geometry, byte materialIndex) =>
            new(
                stableId: 1ul,
                position: float3.zero,
                rotation: quaternion.identity,
                scale: new float3(1f),
                boundsCenter: new float3(0f, 0.5f, 0f),
                boundsExtents: new float3(0.5f),
                geometryKey: $"test-{materialIndex}",
                styleKey: "test-style",
                tier: FarFeatureTier.Mid,
                geometry: geometry,
                materialIndex: materialIndex);

        private static float RadialExtent(Vector3[] vertices, int start, int count)
        {
            float extent = 0f;
            for (int i = start; i < start + count; i++)
                extent = Mathf.Max(extent, Mathf.Abs(vertices[i].x), Mathf.Abs(vertices[i].z));
            return extent;
        }
    }
}