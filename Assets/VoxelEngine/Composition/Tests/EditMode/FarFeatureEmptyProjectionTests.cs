using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Composition.Tests
{
    /// <summary>
    /// Exercises the real bake-to-draw adapter, not shader strings or a model of the adapter.
    /// Operation bounds are not geometry: an empty projection must not request a fallback box.
    /// </summary>
    public sealed class FarFeatureEmptyProjectionTests
    {
        [TestCase(PrimitiveMode.Carve)]
        [TestCase(PrimitiveMode.PaintSolid)]
        [TestCase(PrimitiveMode.PaintSurface)]
        [TestCase(PrimitiveMode.SurfaceDetail)]
        [TestCase(PrimitiveMode.TerrainCorridor)]
        public void OperationWithoutProjectedGeometryDoesNotBecomeFallbackBox(PrimitiveMode mode)
        {
            Primitive operation = Operation(mode, 7);
            IReadOnlyList<FarFeatureInstance> instances = Query(operation);

            Assert.That(instances, Is.Empty,
                "A non-geometric operation cannot be sent to the renderer as null geometry: "
                + "that contract requests a solid fallback box covering the operation bounds.");
        }

        [TestCase(PrimitiveMode.Fill)]
        [TestCase(PrimitiveMode.FillIfEmpty)]
        public void PositiveGeometryStillRetainsItsMaterialAndBounds(PrimitiveMode mode)
        {
            Primitive solid = Operation(mode, 7);
            IReadOnlyList<FarFeatureInstance> instances = Query(solid);

            Assert.That(instances.Count, Is.EqualTo(1));
            Assert.That(instances[0].Geometry, Is.Not.Null);
            Assert.That(instances[0].Geometry.PrimitiveCount, Is.EqualTo(1));
            Assert.That(instances[0].MaterialIndex, Is.EqualTo(7));
            Assert.That(instances[0].BoundsCenter, Is.EqualTo(new float3(0.5f, 10.5f, 0.5f)));
            Assert.That(instances[0].BoundsExtents, Is.EqualTo(new float3(8.5f, 10.5f, 8.5f)));
        }

        [TestCase(PrimitiveMode.PaintSolid)]
        [TestCase(PrimitiveMode.PaintSurface)]
        [TestCase(PrimitiveMode.SurfaceDetail)]
        [TestCase(PrimitiveMode.TerrainCorridor)]
        public void NonProjectedOperationCannotSupplyTheDrawStyle(PrimitiveMode mode)
        {
            Primitive operation = Operation(mode, 9);
            operation.SurfaceStyle = 3;
            operation.Coating = 2;
            Primitive solid = Operation(PrimitiveMode.Fill, 7);
            solid.SurfaceStyle = 1;
            solid.Coating = 0;
            IReadOnlyList<FarFeatureInstance> instances = Query(operation, solid);

            Assert.That(instances.Count, Is.EqualTo(1));
            Assert.That(instances[0].Geometry.PrimitiveCount, Is.EqualTo(1));
            Assert.That(instances[0].MaterialIndex, Is.EqualTo(solid.Material));
            Assert.That(instances[0].StyleKey, Is.EqualTo("m07-s0001-c00"),
                "Geometry, material and style must select from the same projected primitives.");
        }

        private static Primitive Operation(PrimitiveMode mode, byte material) => new()
        {
            Shape = mode == PrimitiveMode.TerrainCorridor
                ? PrimitiveShape.TerrainCorridor : PrimitiveShape.Box,
            Mode = mode,
            Material = material,
            A = new int3(-8, 0, -8),
            B = new int3(8, 20, 8),
        };

        private static IReadOnlyList<FarFeatureInstance> Query(params Primitive[] primitives)
        {
            var bake = new FeaturePresentationBake(
                sourceId: 17ul, revision: 23ul, kind: FeatureKind.Landform,
                position: int3.zero, orientation: 0,
                boundsMin: new int3(-8, 0, -8), boundsMax: new int3(8, 20, 8),
                primitives: primitives);
            var selection = new FarFeatureSelectionPolicy(
                new FarFeatureSelectionPolicy.Thresholds(100f, 80f, 40f, 30f, 10f, 5f),
                new FarFeatureSelectionPolicy.DistanceCaps(1000f, 1000f, 1000f),
                verticalFovDegrees: 60f, viewportHeightPixels: 1080);
            var adapter = new FarFeaturePresentationAdapter(
                new Source(bake), selection, voxelSizeMetres: 1f,
                importance: _ => FarFeatureImportance.Important);
            return adapter.Query(new float3(0f, 10f, -50f), radiusMetres: 100f);
        }

        private sealed class Source : IFeaturePresentationSource
        {
            private readonly FeaturePresentationBake _bake;
            public Source(FeaturePresentationBake bake) => _bake = bake;
            public bool TryGet(ulong sourceId, out FeaturePresentationBake bake)
            {
                bake = sourceId == _bake.SourceId ? _bake : null;
                return bake != null;
            }
            public IReadOnlyList<FeaturePresentationBake> Query(FeaturePresentationBounds bounds) =>
                bounds.Intersects(_bake) ? new[] { _bake } : Array.Empty<FeaturePresentationBake>();
        }
    }
}
