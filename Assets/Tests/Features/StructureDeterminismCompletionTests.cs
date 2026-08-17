using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;
using VoxelEngine.Tests.Features.Fixtures;

namespace VoxelEngine.Tests.Features
{
    public sealed class StructureDeterminismCompletionTests
    {
        private const uint Seed = 0x51A6EEDu;
        private const ulong InstanceSeed = 0x0BADF00DCAFEBEEFul;

        [Test]
        public void SameConfigAndSeedProduceIdenticalPrimitiveAndVoxelOutput()
        {
            FeatureCatalogue catalogue = CottageFixture.Build(Allocator.Temp);
            var primitivesA = new NativeList<Primitive>(32, Allocator.Temp);
            var primitivesB = new NativeList<Primitive>(32, Allocator.Temp);
            var anchorsA = new NativeList<ResolvedAnchor>(4, Allocator.Temp);
            var anchorsB = new NativeList<ResolvedAnchor>(4, Allocator.Temp);
            var tableA = new RegionTable(4, Allocator.Temp);
            var tableB = new RegionTable(4, Allocator.Temp);
            var poolA = new BrickPool(8192, Allocator.Temp);
            var poolB = new BrickPool(8192, Allocator.Temp);

            try
            {
                Assert.AreEqual(CatalogueLoadResult.Ok, FeatureCatalogueBuilder.Finalise(ref catalogue));

                ParameterSet parameters = DefaultParameters(in catalogue);
                int3 origin = new(512, 200, 512);

                EvaluationResult resultA = ShapeProgram.Evaluate(
                    in catalogue,
                    CottageFixture.CottageId,
                    in parameters,
                    origin,
                    orientation: 0,
                    Seed,
                    InstanceSeed,
                    primitivesA,
                    anchorsA);
                EvaluationResult resultB = ShapeProgram.Evaluate(
                    in catalogue,
                    CottageFixture.CottageId,
                    in parameters,
                    origin,
                    orientation: 0,
                    Seed,
                    InstanceSeed,
                    primitivesB,
                    anchorsB);

                Assert.AreEqual(EvaluationResult.Ok, resultA);
                Assert.AreEqual(EvaluationResult.Ok, resultB);
                Assert.AreEqual(primitivesA.Length, primitivesB.Length);
                Assert.AreEqual(anchorsA.Length, anchorsB.Length);

                for (var i = 0; i < primitivesA.Length; i++)
                    AssertPrimitiveEqual(in primitivesA.ElementAt(i), in primitivesB.ElementAt(i), i);

                for (var i = 0; i < anchorsA.Length; i++)
                {
                    Assert.AreEqual(anchorsA[i].Name, anchorsB[i].Name, $"anchor {i} name differs");
                    Assert.AreEqual(anchorsA[i].Position, anchorsB[i].Position, $"anchor {i} position differs");
                    Assert.AreEqual(anchorsA[i].Facing, anchorsB[i].Facing, $"anchor {i} facing differs");
                }

                FeatureDefinition definition = catalogue.Definitions[CottageFixture.CottageId];
                int3 maxExclusive = origin + definition.Footprint;

                RasterResult rasterA = Rasterise(
                    primitivesA.AsArray(), origin, maxExclusive, ref tableA, ref poolA);
                RasterResult rasterB = Rasterise(
                    primitivesB.AsArray(), origin, maxExclusive, ref tableB, ref poolB);

                Assert.IsFalse(rasterA.BudgetExceeded);
                Assert.IsFalse(rasterB.BudgetExceeded);
                Assert.AreEqual(rasterA.VoxelsWritten, rasterB.VoxelsWritten);

                byte[] voxelsA = SubVolumeEquality.Snapshot(
                    ref tableA, in poolA, origin, maxExclusive);
                byte[] voxelsB = SubVolumeEquality.Snapshot(
                    ref tableB, in poolB, origin, maxExclusive);

                int difference = SubVolumeEquality.FirstDifference(voxelsA, voxelsB);
                Assert.AreEqual(-1, difference,
                    $"voxel {difference} differs for identical config and seed");
            }
            finally
            {
                poolB.Dispose();
                poolA.Dispose();
                tableB.Dispose();
                tableA.Dispose();
                anchorsB.Dispose();
                anchorsA.Dispose();
                primitivesB.Dispose();
                primitivesA.Dispose();
                catalogue.Dispose();
            }
        }

        [Test]
        public void SemanticChildSeedsRemainStableWhenUnrelatedKeysAreEvaluated()
        {
            var palette = new StructureMaterialPalette();
            var bounds = new StructureGenerationBounds(int3.zero, new int3(64));
            var terrain = new StructureTerrainAccess(77u);
            var context = new StructureGenerationContext(
                instanceId: 42ul,
                worldSeed: Seed,
                definitionId: 9,
                instanceSeed: InstanceSeed,
                origin: int3.zero,
                orientation: 0,
                in bounds,
                in terrain,
                in palette,
                default);

            var windows = new FixedString64Bytes("windows.north");
            var chimney = new FixedString64Bytes("chimney");
            var porch = new FixedString64Bytes("porch");

            ulong windowsBefore = context.ChildSeed(in windows);
            _ = context.ChildSeed(in chimney);
            _ = context.ChildSeed(in porch);
            ulong windowsAfter = context.ChildSeed(in windows);

            Assert.AreEqual(windowsBefore, windowsAfter);
            Assert.AreNotEqual(windowsBefore, context.ChildSeed(in windows, 1));
            Assert.AreNotEqual(windowsBefore, context.ChildSeed(in chimney));
        }

        private static ParameterSet DefaultParameters(in FeatureCatalogue catalogue)
        {
            FeatureDefinition definition = catalogue.Definitions[CottageFixture.CottageId];
            var parameters = new ParameterSet();

            for (var i = 0; i < definition.ParameterCount; i++)
                parameters[i] = catalogue.Parameters[definition.ParameterOffset + i].Default;

            return parameters;
        }

        private static void AssertPrimitiveEqual(in Primitive expected, in Primitive actual, int index)
        {
            Assert.AreEqual(expected.Shape, actual.Shape, $"primitive {index} shape differs");
            Assert.AreEqual(expected.Mode, actual.Mode, $"primitive {index} mode differs");
            Assert.AreEqual(expected.Material, actual.Material, $"primitive {index} material differs");
            Assert.AreEqual(expected.SurfaceStyle, actual.SurfaceStyle, $"primitive {index} surface style differs");
            Assert.AreEqual(expected.Coating, actual.Coating, $"primitive {index} coating differs");
            Assert.AreEqual(expected.SurfaceFlags, actual.SurfaceFlags, $"primitive {index} surface flags differ");
            Assert.AreEqual(expected.SurfaceDetail, actual.SurfaceDetail, $"primitive {index} surface detail differs");
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

        private static RasterResult Rasterise(
            NativeArray<Primitive> primitives,
            int3 min,
            int3 max,
            ref RegionTable table,
            ref BrickPool pool)
        {
            var reads = new RegionReadSource(in table, in pool);
            var mutations = new RegionMutationStore(in table, in pool);
            return PrimitiveRasteriser.Rasterise(primitives, min, max, reads, mutations);
        }
    }
}
