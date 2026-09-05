using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Composition;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Rendering.Runtime.FarWorld;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class FarFeatureShapePresentationTests
    {
        [Test]
        public void PresentationAdapter_PreservesFrustumProfileAndMaterialFromCanonicalBake()
        {
            var primitive = new Primitive
            {
                Shape = PrimitiveShape.Frustum,
                Mode = PrimitiveMode.Fill,
                Material = 7,
                Axis = 1,
                Direction = 1,
                A = new int3(-8, 0, -8),
                B = new int3(8, 20, 8),
                Radius = 8,
                InnerRadius = 2,
            };
            var bake = new FeaturePresentationBake(
                sourceId: 17ul,
                revision: 23ul,
                kind: FeatureKind.Landform,
                position: int3.zero,
                orientation: 0,
                boundsMin: primitive.A,
                boundsMax: primitive.B,
                primitives: new[] { primitive });
            var selection = new FarFeatureSelectionPolicy(
                new FarFeatureSelectionPolicy.Thresholds(100f, 80f, 40f, 30f, 10f, 5f),
                new FarFeatureSelectionPolicy.DistanceCaps(1000f, 1000f, 1000f),
                verticalFovDegrees: 60f,
                viewportHeightPixels: 1080);
            var adapter = new FarFeaturePresentationAdapter(
                new SingleBakeSource(bake),
                selection,
                voxelSizeMetres: 1f,
                importance: _ => FarFeatureImportance.Important);

            IReadOnlyList<FarFeatureInstance> instances = adapter.Query(
                new float3(0f, 10f, -50f),
                radiusMetres: 100f);

            Assert.That(instances.Count, Is.EqualTo(1));
            FarFeatureInstance instance = instances[0];
            Assert.That(instance.MaterialIndex, Is.EqualTo(7));
            Assert.That(instance.Geometry, Is.Not.Null);
            FarFeatureGeometryPrimitive farPrimitive = instance.Geometry.GetPrimitive(0);
            Assert.That(farPrimitive.Shape, Is.EqualTo(FarFeatureGeometryShape.Frustum));
            Assert.That(farPrimitive.StartRadiusScale, Is.EqualTo(1f).Within(0.001f));
            Assert.That(farPrimitive.EndRadiusScale, Is.EqualTo(0.25f).Within(0.001f));
        }

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
                extent = Mathf.Max(extent, Mathf.Max(Mathf.Abs(vertices[i].x), Mathf.Abs(vertices[i].z)));
            return extent;
        }

        private sealed class SingleBakeSource : IFeaturePresentationSource
        {
            private readonly FeaturePresentationBake _bake;

            public SingleBakeSource(FeaturePresentationBake bake) => _bake = bake;

            public bool TryGet(ulong sourceId, out FeaturePresentationBake bake)
            {
                bake = sourceId == _bake.SourceId ? _bake : null;
                return bake != null;
            }

            public IReadOnlyList<FeaturePresentationBake> Query(FeaturePresentationBounds bounds) =>
                bounds.Intersects(_bake)
                    ? new[] { _bake }
                    : Array.Empty<FeaturePresentationBake>();
        }
    }
}