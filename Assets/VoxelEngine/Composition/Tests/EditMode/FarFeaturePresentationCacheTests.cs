using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Composition.Tests
{
    public sealed class FarFeaturePresentationCacheTests
    {
        [Test]
        public void CameraQueriesReuseGeometryUntilItsSourceRevisionChanges()
        {
            var source = new Source { Bakes = new[] { Bake(1, 10) } };
            var adapter = Adapter(source);
            var first = adapter.Query(float3.zero, 1000f)[0];
            // A source may return a new immutable bake object for the same revision.
            source.Bakes = new[] { Bake(1, 10) };
            var repeated = adapter.Query(new float3(1, 0, 0), 1000f)[0];
            Assert.That(repeated.Geometry, Is.SameAs(first.Geometry));
            Assert.That(repeated.GeometryKey, Is.SameAs(first.GeometryKey));
            Assert.That(repeated.StyleKey, Is.SameAs(first.StyleKey));

            source.Bakes = new[] { Bake(1, 11) };
            var changed = adapter.Query(float3.zero, 1000f)[0];
            Assert.That(changed.Geometry, Is.Not.SameAs(first.Geometry));
            Assert.That(changed.GeometryKey, Is.Not.EqualTo(first.GeometryKey));
            Assert.That(adapter.CachedGeometryCount, Is.EqualTo(1));
        }

        [Test]
        public void TraversalRetiresGeometryOutsideTheCurrentSourceQuery()
        {
            var source = new Source { Bakes = new[] { Bake(1, 10), Bake(2, 20) } };
            var adapter = Adapter(source);
            adapter.Query(float3.zero, 1000f);
            Assert.That(adapter.CachedGeometryCount, Is.EqualTo(2));
            source.Bakes = new[] { Bake(2, 20) };
            adapter.Query(float3.zero, 1000f);
            Assert.That(adapter.CachedGeometryCount, Is.EqualTo(1));
            source.Bakes = System.Array.Empty<FeaturePresentationBake>();
            adapter.Query(float3.zero, 1000f);
            Assert.That(adapter.CachedGeometryCount, Is.Zero);
        }

        private static FarFeaturePresentationAdapter Adapter(Source source) => new(
            source,
            new FarFeatureSelectionPolicy(
                new FarFeatureSelectionPolicy.Thresholds(24, 18, 4, 3, 1.5f, 1),
                new FarFeatureSelectionPolicy.DistanceCaps(1000, 1000, 1000), 60, 1080),
            0.1f);

        private static FeaturePresentationBake Bake(ulong id, ulong revision) => new(
            id, revision, default, int3.zero, 0, int3.zero, new int3(100),
            new[] { new Primitive { Shape = PrimitiveShape.Box, Mode = PrimitiveMode.Fill,
                A = int3.zero, B = new int3(100), Material = 1 } });

        private sealed class Source : IFeaturePresentationSource
        {
            public FeaturePresentationBake[] Bakes;
            public IReadOnlyList<FeaturePresentationBake> Query(FeaturePresentationBounds bounds) => Bakes;
            public bool TryGet(ulong id, out FeaturePresentationBake bake)
            {
                foreach (var candidate in Bakes)
                    if (candidate.SourceId == id) { bake = candidate; return true; }
                bake = null;
                return false;
            }
        }
    }
}
