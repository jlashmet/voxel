using System.Collections.Generic;
using Game.WorldBuilder.Runtime;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class ShowcaseFarFeatureRuntimeTests
    {
        [Test]
        public void UpdateQueriesDerivedPresentationSourceWithoutVoxelResidencyDependency()
        {
            var root = new GameObject("far-feature-runtime-test");
            var source = new RecordingPresentationSource();
            var states = new StructureVisualStateStore();
            var runtime = new VoxelEngine.Showcase.ShowcaseFarFeatureRuntime(
                root.transform,
                source,
                0,
                states,
                0.1f,
                null);

            try
            {
                runtime.Update(null, new float3(1200f, 50f, -900f));

                Assert.That(source.QueryCalls, Is.EqualTo(1));
                Assert.That(runtime.SourceCount, Is.EqualTo(0));
                Assert.That(runtime.VisibleInstanceCount, Is.EqualTo(0));
            }
            finally
            {
                runtime.Dispose();
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PublishedNearSurfaceRadiusDoesNotRetractOnTransientIncompleteFrame()
        {
            var root = new GameObject("far-feature-runtime-handoff-test");
            var source = new RecordingPresentationSource();
            var states = new StructureVisualStateStore();
            var runtime = new VoxelEngine.Showcase.ShowcaseFarFeatureRuntime(
                root.transform,
                source,
                0,
                states,
                0.1f,
                null);

            try
            {
                runtime.Update(null, float3.zero, 300f);
                Assert.That(runtime.PublishedNearSurfaceRadiusMetres, Is.EqualTo(300f));

                runtime.Update(null, new float3(1f, 0f, 0f), 0f);

                Assert.That(runtime.PublishedNearSurfaceRadiusMetres, Is.EqualTo(300f));
                Assert.That(source.QueryCalls, Is.EqualTo(2));
            }
            finally
            {
                runtime.Dispose();
                Object.DestroyImmediate(root);
            }
        }

        private sealed class RecordingPresentationSource : IFeaturePresentationSource
        {
            private static readonly FeaturePresentationBake[] Empty = new FeaturePresentationBake[0];

            public int QueryCalls { get; private set; }

            public bool TryGet(ulong sourceId, out FeaturePresentationBake bake)
            {
                bake = null;
                return false;
            }

            public IReadOnlyList<FeaturePresentationBake> Query(FeaturePresentationBounds bounds)
            {
                QueryCalls++;
                return Empty;
            }
        }
    }
}
