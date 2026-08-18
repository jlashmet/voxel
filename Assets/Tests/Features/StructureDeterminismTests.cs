using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;
using VoxelEngine.Tests.Features.Fixtures;

namespace VoxelEngine.Tests.Features
{
    public sealed class StructureDeterminismTests
    {
        private const uint Seed = 0x51A7E123u;
        private const ulong InstanceId = 0xC077A6Eul;

        [Test]
        public void SameConfigAndSeedProduceIdenticalPrimitivesAndVoxels()
        {
            FeatureCatalogue catalogue = CottageFixture.Build(Allocator.Temp);
            Assert.AreEqual(CatalogueLoadResult.Ok, FeatureCatalogueBuilder.Finalise(ref catalogue));

            var origin = new int3(256, 160, 384);
            FeatureDefinition definition = catalogue.Definitions[CottageFixture.CottageId];
            int3 max = origin + definition.Footprint;

            NativeList<Primitive> first = Evaluate(in catalogue, origin, out NativeList<ResolvedAnchor> firstAnchors);
            NativeList<Primitive> second = Evaluate(in catalogue, origin, out NativeList<ResolvedAnchor> secondAnchors);

            try
            {
                Assert.AreEqual(first.Length, second.Length);
                Assert.AreEqual(firstAnchors.Length, secondAnchors.Length);
                for (var i = 0; i < first.Length; i++)
                    AssertPrimitiveEqual(first[i], second[i], i);
                for (var i = 0; i < firstAnchors.Length; i++)
                {
                    Assert.AreEqual(firstAnchors[i].Position, secondAnchors[i].Position, $"anchor {i} position differs");
                    Assert.AreEqual(firstAnchors[i].Facing, secondAnchors[i].Facing, $"anchor {i} facing differs");
                }

                byte[] firstVoxels = RasterSnapshot(first.AsArray(), origin, max);
                byte[] secondVoxels = RasterSnapshot(second.AsArray(), origin, max);
                Assert.AreEqual(-1, SubVolumeEquality.FirstDifference(firstVoxels, secondVoxels),
                    "identical structure config/seed produced different authoritative voxel output");
            }
            finally
            {
                first.Dispose();
                second.Dispose();
                firstAnchors.Dispose();
                secondAnchors.Dispose();
                catalogue.Dispose();
            }
        }

        [Test]
        public void SemanticChildSeedsStayStableWhenUnrelatedChildrenAreAdded()
        {
            const ulong parent = 0x1234ABCDEF987654ul;
            var windows = new FixedString64Bytes("windows");
            var chimney = new FixedString64Bytes("chimney");
            var decorations = new FixedString64Bytes("decorations");

            ulong window0Before = StructureSeed.Child(parent, in windows, 0);
            ulong window1Before = StructureSeed.Child(parent, in windows, 1);
            ulong chimneyBefore = StructureSeed.Child(parent, in chimney, 0);

            // Deriving an unrelated semantic branch must not consume mutable RNG state or perturb
            // any pre-existing component choice.
            _ = StructureSeed.Child(parent, in decorations, 0);
            _ = StructureSeed.Child(parent, in decorations, 1);
            _ = StructureSeed.Child(parent, in decorations, 2);

            Assert.AreEqual(window0Before, StructureSeed.Child(parent, in windows, 0));
            Assert.AreEqual(window1Before, StructureSeed.Child(parent, in windows, 1));
            Assert.AreEqual(chimneyBefore, StructureSeed.Child(parent, in chimney, 0));
            Assert.AreNotEqual(window0Before, window1Before, "semantic ordinals must identify distinct children");
        }

        private static NativeList<Primitive> Evaluate(
            in FeatureCatalogue catalogue,
            int3 origin,
            out NativeList<ResolvedAnchor> anchors)
        {
            FeatureDefinition definition = catalogue.Definitions[CottageFixture.CottageId];
            var parameters = new ParameterSet();
            for (var i = 0; i < definition.ParameterCount; i++)
                parameters[i] = catalogue.Parameters[definition.ParameterOffset + i].Default;

            var primitives = new NativeList<Primitive>(32, Allocator.Temp);
            anchors = new NativeList<ResolvedAnchor>(4, Allocator.Temp);
            EvaluationResult result = ShapeProgram.Evaluate(
                in catalogue,
                CottageFixture.CottageId,
                in parameters,
                origin,
                orientation: 0,
                Seed,
                InstanceId,
                primitives,
                anchors);

            Assert.AreEqual(EvaluationResult.Ok, result);
            return primitives;
        }

        private static byte[] RasterSnapshot(NativeArray<Primitive> primitives, int3 min, int3 max)
        {
            var table = new RegionTable(4, Allocator.Temp);
            var pool = new BrickPool(8192, Allocator.Temp);
            try
            {
                var reads = new RegionReadSource(in table, in pool);
                var mutations = new RegionMutationStore(in table, in pool);
                PrimitiveRasteriser.Rasterise(primitives, min, max, reads, mutations);
                return SubVolumeEquality.Snapshot(ref table, in pool, min, max);
            }
            finally
            {
                pool.Dispose();
                table.Dispose();
            }
        }

        private static void AssertPrimitiveEqual(in Primitive expected, in Primitive actual, int index)
        {
            Assert.AreEqual(expected.Shape, actual.Shape, $"primitive {index} shape differs");
            Assert.AreEqual(expected.Mode, actual.Mode, $"primitive {index} mode differs");
            Assert.AreEqual(expected.Material, actual.Material, $"primitive {index} material differs");
            Assert.AreEqual(expected.SurfaceStyle, actual.SurfaceStyle, $"primitive {index} surface style differs");
            Assert.AreEqual(expected.Coating, actual.Coating, $"primitive {index} coating differs");
            Assert.AreEqual(expected.SurfaceFlags, actual.SurfaceFlags, $"primitive {index} flags differ");
            Assert.AreEqual(expected.SurfaceDetail, actual.SurfaceDetail, $"primitive {index} detail differs");
            Assert.AreEqual(expected.Axis, actual.Axis, $"primitive {index} axis differs");
            Assert.AreEqual(expected.Direction, actual.Direction, $"primitive {index} direction differs");
            Assert.AreEqual(expected.Profile, actual.Profile, $"primitive {index} profile differs");
            Assert.AreEqual(expected.Order, actual.Order, $"primitive {index} order differs");
            Assert.AreEqual(expected.A, actual.A, $"primitive {index} A differs");
            Assert.AreEqual(expected.B, actual.B, $"primitive {index} B differs");
            Assert.AreEqual(expected.Radius, actual.Radius, $"primitive {index} radius differs");
            Assert.AreEqual(expected.InnerRadius, actual.InnerRadius, $"primitive {index} inner radius differs");
            Assert.AreEqual(expected.C, actual.C, $"primitive {index} C differs");
            Assert.AreEqual(expected.D, actual.D, $"primitive {index} D differs");
            Assert.AreEqual(expected.StartDirection, actual.StartDirection, $"primitive {index} start direction differs");
            Assert.AreEqual(expected.EndDirection, actual.EndDirection, $"primitive {index} end direction differs");
        }
    }
}
