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
    public sealed class FarFeaturePresentationParityTests
    {
        [Test]
        public void PresentationAdapter_ResolvesInstalledMaterialAndCoatingToValueContract()
        {
            const int materialIndex = 7;
            const int coatingIndex = 2;
            Vector4 originalAlbedo = VoxelPresentationCatalogue.MaterialAlbedo[materialIndex];
            Vector4 originalSurface = VoxelPresentationCatalogue.MaterialSurface[materialIndex];
            Vector4 originalCoatingTint = VoxelPresentationCatalogue.CoatingTint[coatingIndex];
            Vector4 originalCoatingSampling = VoxelPresentationCatalogue.CoatingSampling[coatingIndex];
            Vector4 originalCoatingResponse = VoxelPresentationCatalogue.CoatingResponse[coatingIndex];
            try
            {
                VoxelPresentationCatalogue.MaterialAlbedo[materialIndex] =
                    new Vector4(0.18f, 0.42f, 0.67f, 1f);
                VoxelPresentationCatalogue.MaterialSurface[materialIndex] =
                    new Vector4(0.02f, 0f, 0.82f, 0f);
                VoxelPresentationCatalogue.CoatingTint[coatingIndex] =
                    new Vector4(0.90f, 0.95f, 1.00f, 1f);
                VoxelPresentationCatalogue.CoatingSampling[coatingIndex] =
                    new Vector4(0f, 0f, 0f, 0.50f);
                VoxelPresentationCatalogue.CoatingResponse[coatingIndex] =
                    new Vector4(0f, 1f, 0f, 0.60f);

                var primitive = new Primitive
                {
                    Shape = PrimitiveShape.Box,
                    Mode = PrimitiveMode.Fill,
                    Material = materialIndex,
                    SurfaceStyle = 5,
                    Coating = coatingIndex,
                    A = int3.zero,
                    B = new int3(9),
                };
                var bake = new FeaturePresentationBake(
                    41UL,
                    9UL,
                    default,
                    int3.zero,
                    0,
                    int3.zero,
                    new int3(9),
                    new[] { primitive });
                var source = new SinglePresentationSource(bake);
                var selection = new FarFeatureSelectionPolicy(
                    new FarFeatureSelectionPolicy.Thresholds(24f, 18f, 4f, 3f, 1.5f, 1f),
                    new FarFeatureSelectionPolicy.DistanceCaps(1000f, 1000f, 1000f),
                    60f,
                    1080);
                var adapter = new FarFeaturePresentationAdapter(source, selection, 1f);

                IReadOnlyList<FarFeatureInstance> instances =
                    adapter.Query(new float3(5f, 5f, -20f), 100f);

                Assert.That(instances.Count, Is.EqualTo(1));
                // Coating amount = blend 0.5 * midpoint vertical response 0.5 = 0.25.
                float4 expectedAlbedo = math.lerp(
                    new float4(0.18f, 0.42f, 0.67f, 1f),
                    new float4(0.90f, 0.95f, 1.00f, 1f),
                    0.25f);
                Assert.That(instances[0].Presentation.Albedo.x, Is.EqualTo(expectedAlbedo.x).Within(0.0001f));
                Assert.That(instances[0].Presentation.Albedo.y, Is.EqualTo(expectedAlbedo.y).Within(0.0001f));
                Assert.That(instances[0].Presentation.Albedo.z, Is.EqualTo(expectedAlbedo.z).Within(0.0001f));
                Assert.That(instances[0].Presentation.Albedo.w, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(instances[0].Presentation.Roughness, Is.EqualTo(0.765f).Within(0.0001f));
                Assert.That(instances[0].StyleKey, Does.Contain("s0005"));
            }
            finally
            {
                VoxelPresentationCatalogue.MaterialAlbedo[materialIndex] = originalAlbedo;
                VoxelPresentationCatalogue.MaterialSurface[materialIndex] = originalSurface;
                VoxelPresentationCatalogue.CoatingTint[coatingIndex] = originalCoatingTint;
                VoxelPresentationCatalogue.CoatingSampling[coatingIndex] = originalCoatingSampling;
                VoxelPresentationCatalogue.CoatingResponse[coatingIndex] = originalCoatingResponse;
            }
        }

        [Test]
        public void MultiMaterialBakeKeepsSeparateWallAndRoofDrawSurfaces()
        {
            var primitives = new[]
            {
                new Primitive { Shape = PrimitiveShape.Box, Mode = PrimitiveMode.Fill,
                    Material = 1, A = int3.zero, B = new int3(9, 9, 9) },
                new Primitive { Shape = PrimitiveShape.Prism, Mode = PrimitiveMode.Fill,
                    Material = 8, A = new int3(0, 10, 0), B = new int3(9, 14, 9) }
            };
            var bake = new FeaturePresentationBake(42, 10, default, int3.zero, 0,
                int3.zero, new int3(9, 14, 9), primitives);
            var policy = new FarFeatureSelectionPolicy(
                new FarFeatureSelectionPolicy.Thresholds(24, 18, 4, 3, 1.5f, 1),
                new FarFeatureSelectionPolicy.DistanceCaps(1000, 1000, 1000), 60, 1080);
            var adapter = new FarFeaturePresentationAdapter(new SinglePresentationSource(bake), policy, 1);
            Vector4 wallColour = VoxelPresentationCatalogue.MaterialAlbedo[1];
            Vector4 roofColour = VoxelPresentationCatalogue.MaterialAlbedo[8];
            var root = new GameObject("far-multiple-materials-test");
            try
            {
                VoxelPresentationCatalogue.MaterialAlbedo[1] = new Vector4(0.65f, 0.67f, 0.7f, 1);
                VoxelPresentationCatalogue.MaterialAlbedo[8] = new Vector4(0.5f, 0.12f, 0.04f, 1);
                var instances = adapter.Query(new float3(5, 5, -20), 100);
                var renderer = root.AddComponent<ProceduralFarFeatureRenderer>();
                Mesh mesh = renderer.ResolveMesh(instances[0]);
                Assert.That(mesh.subMeshCount, Is.EqualTo(2),
                    "Wall and roof materials must remain independently drawable after far-feature baking.");
                Assert.That(mesh.GetIndexCount(0), Is.GreaterThan(0));
                Assert.That(mesh.GetIndexCount(1), Is.GreaterThan(0));
                var other = new FarFeatureInstance(99, float3.zero, quaternion.identity, new float3(1),
                    float3.zero, new float3(1), "other-geometry", "other-style", FarFeatureTier.Mid,
                    FarFeatureVisualFlags.None, instances[0].Geometry, instances[0].Presentation);
                for (int slot = 0; slot < 2; slot++)
                {
                    Assert.That(renderer.ResolveMaterial(other, slot), Is.SameAs(renderer.ResolveMaterial(instances[0], slot)),
                        "Identical resolved presentations must share materials across geometry identities.");
                    byte materialId = (byte)(slot == 0 ? 1 : 8);
                    var expected = MaterialPresentationComposition.ResolveFarFeaturePresentation(materialId, 0, 0);
                    Color colour = renderer.ResolveMaterial(instances[0], slot).GetColor("_BaseColor");
                    Assert.That((float4)new Vector4(colour.r, colour.g, colour.b, colour.a),
                        Is.EqualTo(expected.Albedo), $"slot {slot} lost its resolved presentation");
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
                VoxelPresentationCatalogue.MaterialAlbedo[1] = wallColour;
                VoxelPresentationCatalogue.MaterialAlbedo[8] = roofColour;
            }
        }

        [Test]
        public void ProceduralRenderer_UsesResolvedPresentationInsteadOfShaderDefault()
        {
            var root = new GameObject("far-feature-presentation-test");
            try
            {
                var renderer = root.AddComponent<ProceduralFarFeatureRenderer>();
                var instance = new FarFeatureInstance(
                    1UL,
                    float3.zero,
                    quaternion.identity,
                    new float3(1f),
                    float3.zero,
                    new float3(0.5f),
                    "test-geometry",
                    "test-style",
                    FarFeatureTier.Mid,
                    FarFeatureVisualFlags.None,
                    null,
                    new FarFeaturePresentation(new float4(0.18f, 0.42f, 0.67f, 1f), 0.82f));

                Material material = renderer.ResolveMaterial(instance);
                Color actual = material.HasProperty("_BaseColor")
                    ? material.GetColor("_BaseColor")
                    : material.GetColor("_Color");

                Assert.That(actual.r, Is.EqualTo(0.18f).Within(0.0001f));
                Assert.That(actual.g, Is.EqualTo(0.42f).Within(0.0001f));
                Assert.That(actual.b, Is.EqualTo(0.67f).Within(0.0001f));
                if (material.HasProperty("_Smoothness"))
                    Assert.That(material.GetFloat("_Smoothness"), Is.EqualTo(0.18f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SurfaceSubmissionRestoresInstancesWhenReplacementProofIsLost()
        {
            var root = new GameObject("far-replacement-submission-test");
            try
            {
                var renderer = root.AddComponent<ProceduralFarFeatureRenderer>();
                renderer.UseSurfaceReplacementHandoff = true;
                renderer.SetInstances(new[] { new FarFeatureInstance(17UL, float3.zero,
                    quaternion.identity, new float3(1), float3.zero, new float3(0.5f),
                    "replacement-geometry", "replacement-style", FarFeatureTier.Mid) });
                var consumers = new List<ProceduralFarFeatureRenderer>();
                ProceduralFarFeatureRenderer.PrepareSurfaceConsumers(consumers, _ => false);
                Assert.Contains(renderer, consumers);
                Assert.AreEqual(1, renderer.InstanceCount);
                ProceduralFarFeatureRenderer.PrepareSurfaceConsumers(consumers, _ => true);
                Assert.AreEqual(0, renderer.InstanceCount);
                Assert.AreEqual(1, renderer.NearReplacementCount);
                ProceduralFarFeatureRenderer.PrepareSurfaceConsumers(consumers, _ => false);
                Assert.AreEqual(1, renderer.InstanceCount);
                Assert.AreEqual(0, renderer.NearReplacementCount);
                ProceduralFarFeatureRenderer.PrepareSurfaceConsumers(consumers, _ => true);
                renderer.UseSurfaceReplacementHandoff = false;
                Assert.AreEqual(1, renderer.InstanceCount,
                    "Returning to ordinary submission must restore source instances immediately.");
                renderer.UseSurfaceReplacementHandoff = true;
                renderer.enabled = false;
                ProceduralFarFeatureRenderer.PrepareSurfaceConsumers(consumers, _ => false);
                Assert.False(consumers.Contains(renderer));
                renderer.enabled = true;
                renderer.Clear();
                ProceduralFarFeatureRenderer.PrepareSurfaceConsumers(consumers, _ => false);
                Assert.AreEqual(0, renderer.InstanceCount);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private sealed class SinglePresentationSource : IFeaturePresentationSource
        {
            private readonly FeaturePresentationBake _bake;
            private readonly FeaturePresentationBake[] _single;

            public SinglePresentationSource(FeaturePresentationBake bake)
            {
                _bake = bake;
                _single = new[] { bake };
            }

            public bool TryGet(ulong sourceId, out FeaturePresentationBake bake)
            {
                bake = sourceId == _bake.SourceId ? _bake : null;
                return bake != null;
            }

            public IReadOnlyList<FeaturePresentationBake> Query(FeaturePresentationBounds bounds) => _single;
        }
    }
}
