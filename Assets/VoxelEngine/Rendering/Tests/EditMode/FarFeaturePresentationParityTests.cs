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
