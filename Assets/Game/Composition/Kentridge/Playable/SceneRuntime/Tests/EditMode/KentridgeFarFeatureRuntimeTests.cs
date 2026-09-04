using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeFarFeatureRuntimeTests
    {
        [Test]
        public void SecondCompositionConsumerSelectsSemanticProxyWithoutVoxelWorld()
        {
            var root = new GameObject("kentridge-far-feature-runtime-test");
            var source = new SinglePresentationSource(CreateBuildingBake());
            var runtime = new Game.Kentridge.PlayableSlice.KentridgeFarFeatureRuntime(
                root.transform,
                source,
                1,
                0.1f,
                null);

            try
            {
                runtime.Update(null, float3.zero);

                Assert.That(source.QueryCalls, Is.EqualTo(1));
                Assert.That(runtime.SourceCount, Is.EqualTo(1));
                Assert.That(runtime.VisibleInstanceCount, Is.EqualTo(1),
                    "The Kentridge consumer must select a proxy from presentation metadata alone.");
                Assert.That(runtime.PersistentInstanceObjectCount, Is.EqualTo(0),
                    "Shared far rendering must not create a persistent GameObject per building.");
            }
            finally
            {
                runtime.Dispose();
                Object.DestroyImmediate(root);
            }
        }

        private static FeaturePresentationBake CreateBuildingBake()
        {
            var min = new int3(100, 0, 100);
            var max = new int3(199, 99, 199);
            var primitive = new Primitive
            {
                Shape = PrimitiveShape.Box,
                Mode = PrimitiveMode.Fill,
                Material = 1,
                A = min,
                B = max,
            };
            return new FeaturePresentationBake(
                0x4B454E5452494447UL,
                0x101UL,
                default,
                min,
                0,
                min,
                max,
                new[] { primitive });
        }

        private sealed class SinglePresentationSource : IFeaturePresentationSource
        {
            private readonly FeaturePresentationBake _bake;

            public SinglePresentationSource(FeaturePresentationBake bake) => _bake = bake;
            public int QueryCalls { get; private set; }

            public bool TryGet(ulong sourceId, out FeaturePresentationBake bake)
            {
                bake = sourceId == _bake.SourceId ? _bake : null;
                return bake != null;
            }

            public IReadOnlyList<FeaturePresentationBake> Query(FeaturePresentationBounds bounds)
            {
                QueryCalls++;
                return bounds.Intersects(_bake)
                    ? new[] { _bake }
                    : new FeaturePresentationBake[0];
            }
        }
    }
}
