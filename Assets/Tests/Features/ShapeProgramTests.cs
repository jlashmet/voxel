using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Features;
using VoxelEngine.Core.Storage;
using VoxelEngine.Tests.Features.Fixtures;

using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.Features
{
    /// <summary>
    /// The US1 exit criteria.
    ///
    /// The load-bearing one is <see cref="RasterisingInPiecesEqualsRasterisingWhole"/>. A feature
    /// spanning four regions is generated four times, once per region, each producing only its own
    /// slice — so "the pieces equal the whole" is not a nice property to have, it is the
    /// definition of correct. A failure shows up in the world as a wall that stops at a region
    /// border.
    /// </summary>
    public sealed class ShapeProgramTests
    {
        private const uint Seed = 4242u;

        [Test]
        public void CottageProgramEmitsPrimitives()
        {
            var catalogue = CottageFixture.Build(Allocator.Temp);
            Assert.AreEqual(CatalogueLoadResult.Ok, FeatureCatalogueBuilder.Finalise(ref catalogue));

            var primitives = new NativeList<Primitive>(32, Allocator.Temp);
            var anchors = new NativeList<ResolvedAnchor>(4, Allocator.Temp);

            var parameters = DefaultParameters(in catalogue);

            var result = ShapeProgram.Evaluate(
                in catalogue, CottageFixture.CottageId, in parameters,
                new int3(1000, 0, 1000), 0, Seed, 99ul, primitives, anchors);

            Assert.AreEqual(EvaluationResult.Ok, result);
            Assert.Greater(primitives.Length, 0, "the cottage program emitted nothing");
            Assert.Greater(anchors.Length, 0, "the cottage program set no anchors");

            primitives.Dispose();
            anchors.Dispose();
            catalogue.Dispose();
        }

        [Test]
        public void EvaluationIsDeterministic()
        {
            var catalogue = CottageFixture.Build(Allocator.Temp);
            FeatureCatalogueBuilder.Finalise(ref catalogue);

            var a = Evaluate(in catalogue, new int3(512, 0, 512), 0, out var anchorsA);
            var b = Evaluate(in catalogue, new int3(512, 0, 512), 0, out var anchorsB);

            Assert.AreEqual(a.Length, b.Length);

            for (var i = 0; i < a.Length; i++)
            {
                Assert.AreEqual(a[i].A, b[i].A, $"primitive {i} min differs");
                Assert.AreEqual(a[i].B, b[i].B, $"primitive {i} max differs");
                Assert.AreEqual(a[i].Material, b[i].Material);
                Assert.AreEqual(a[i].Shape, b[i].Shape);
            }

            Assert.AreEqual(anchorsA[0].Position, anchorsB[0].Position);

            a.Dispose(); b.Dispose(); anchorsA.Dispose(); anchorsB.Dispose();
            catalogue.Dispose();
        }

        [Test]
        public void EveryPrimitiveLiesInsideTheDeclaredFootprint()
        {
            // The footprint bounds the neighbourhood every region in the world scans. Content
            // outside it is content a region will not know to look for, which is a seam.
            var catalogue = CottageFixture.Build(Allocator.Temp);
            FeatureCatalogueBuilder.Finalise(ref catalogue);

            var definition = catalogue.Definitions[CottageFixture.CottageId];
            var origin = new int3(2048, 0, 2048);

            for (byte orientation = 0; orientation < 4; orientation++)
            {
                var primitives = Evaluate(in catalogue, origin, orientation, out var anchors);

                for (var i = 0; i < primitives.Length; i++)
                {
                    primitives[i].Bounds(out var min, out var max);

                    Assert.GreaterOrEqual(min.x, origin.x, $"primitive {i} escapes -x at orientation {orientation}");
                    Assert.GreaterOrEqual(min.y, origin.y, $"primitive {i} escapes -y");
                    Assert.GreaterOrEqual(min.z, origin.z, $"primitive {i} escapes -z");

                    Assert.Less(max.x, origin.x + definition.Footprint.x, $"primitive {i} escapes +x");
                    Assert.Less(max.y, origin.y + definition.Footprint.y, $"primitive {i} escapes +y");
                    Assert.Less(max.z, origin.z + definition.Footprint.z, $"primitive {i} escapes +z");
                }

                primitives.Dispose();
                anchors.Dispose();
            }

            catalogue.Dispose();
        }

        [Test]
        public void RasterisingInPiecesEqualsRasterisingWhole()
        {
            var catalogue = CottageFixture.Build(Allocator.Temp);
            FeatureCatalogueBuilder.Finalise(ref catalogue);

            var origin = new int3(256, 200, 256);
            var definition = catalogue.Definitions[CottageFixture.CottageId];

            int3 min = origin;
            int3 max = origin + definition.Footprint;

            var primitives = Evaluate(in catalogue, origin, 0, out var anchors);

            // Whole.
            var wholeTable = new RegionTable(4, Allocator.Temp);
            var wholePool = new BrickPool(8192, Allocator.Temp);
            Rasterise(primitives.AsArray(), min, max, ref wholeTable, ref wholePool);
            var whole = SubVolumeEquality.Snapshot(ref wholeTable, in wholePool, min, max);

            // Eight disjoint octants that tile the same volume.
            var piecesTable = new RegionTable(4, Allocator.Temp);
            var piecesPool = new BrickPool(8192, Allocator.Temp);

            foreach (var (octantMin, octantMax) in SubVolumeEquality.Octants(min, max))
                Rasterise(primitives.AsArray(), octantMin, octantMax,
                                              ref piecesTable, ref piecesPool);

            var pieces = SubVolumeEquality.Snapshot(ref piecesTable, in piecesPool, min, max);

            int difference = SubVolumeEquality.FirstDifference(whole, pieces);
            Assert.AreEqual(-1, difference,
                $"voxel {difference} differs between whole and piecewise rasterisation — a " +
                "feature spanning regions would show a seam here");

            primitives.Dispose(); anchors.Dispose();
            wholeTable.Dispose(); wholePool.Dispose();
            piecesTable.Dispose(); piecesPool.Dispose();
            catalogue.Dispose();
        }

        [Test]
        public void ClippingNeverWritesOutsideTheSubVolume()
        {
            var catalogue = CottageFixture.Build(Allocator.Temp);
            FeatureCatalogueBuilder.Finalise(ref catalogue);

            var origin = new int3(64, 200, 64);
            var primitives = Evaluate(in catalogue, origin, 0, out var anchors);

            var table = new RegionTable(4, Allocator.Temp);
            var pool = new BrickPool(8192, Allocator.Temp);

            var slice = (min: origin, max: origin + new int3(16, 200, 16));
            Rasterise(primitives.AsArray(), slice.min, slice.max, ref table, ref pool);

            // One voxel outside the slice on each axis must be untouched.
            Assert.AreEqual(VoxelDimensions.MaterialEmpty,
                VoxelAccess.GetVoxel(ref table, in pool, new int3(slice.max.x, origin.y + 1, origin.z)));
            Assert.AreEqual(VoxelDimensions.MaterialEmpty,
                VoxelAccess.GetVoxel(ref table, in pool, new int3(origin.x, origin.y + 1, slice.max.z)));

            primitives.Dispose(); anchors.Dispose();
            table.Dispose(); pool.Dispose();
            catalogue.Dispose();
        }

        [Test]
        public void CarveRemovesWhatFillPlaced()
        {
            var table = new RegionTable(4, Allocator.Temp);
            var pool = new BrickPool(1024, Allocator.Temp);

            var primitives = new NativeArray<Primitive>(2, Allocator.Temp);
            primitives[0] = Core.Features.Emitters.BoxEmitter.Box(
                new int3(10, 10, 10), new int3(8, 8, 8), 1, PrimitiveMode.Fill, 0);
            primitives[1] = Core.Features.Emitters.BoxEmitter.Box(
                new int3(12, 12, 12), new int3(2, 2, 2), 0, PrimitiveMode.Carve, 1);

            Rasterise(primitives, new int3(0, 0, 0), new int3(32, 32, 32),
                                          ref table, ref pool);

            Assert.AreNotEqual(VoxelDimensions.MaterialEmpty,
                VoxelAccess.GetVoxel(ref table, in pool, new int3(10, 10, 10)), "fill did not land");
            Assert.AreEqual(VoxelDimensions.MaterialEmpty,
                VoxelAccess.GetVoxel(ref table, in pool, new int3(12, 12, 12)), "carve did not remove");

            primitives.Dispose();
            table.Dispose(); pool.Dispose();
        }

        [Test]
        public void PrimitiveBudgetIsReportedNotTruncated()
        {
            var table = new RegionTable(4, Allocator.Temp);
            var pool = new BrickPool(256, Allocator.Temp);

            var primitives = new NativeArray<Primitive>(FeatureBudget.MaxPrimitivesPerRegion + 1, Allocator.Temp);

            var result = Rasterise(primitives, int3.zero, new int3(8, 8, 8),
                                                       ref table, ref pool);

            Assert.IsTrue(result.BudgetExceeded, "over-budget batch was accepted");
            Assert.AreEqual(0, result.VoxelsWritten, "an over-budget batch wrote a partial result");

            primitives.Dispose();
            table.Dispose(); pool.Dispose();
        }

        // -- helpers -------------------------------------------------------------

        private static ParameterSet DefaultParameters(in FeatureCatalogue catalogue)
        {
            var definition = catalogue.Definitions[CottageFixture.CottageId];
            var set = new ParameterSet();

            for (var i = 0; i < definition.ParameterCount; i++)
                set[i] = catalogue.Parameters[definition.ParameterOffset + i].Default;

            return set;
        }

        private static NativeList<Primitive> Evaluate(in FeatureCatalogue catalogue, int3 origin,
                                                      byte orientation,
                                                      out NativeList<ResolvedAnchor> anchors)
        {
            var primitives = new NativeList<Primitive>(32, Allocator.Temp);
            anchors = new NativeList<ResolvedAnchor>(4, Allocator.Temp);

            var parameters = DefaultParameters(in catalogue);

            ShapeProgram.Evaluate(in catalogue, CottageFixture.CottageId, in parameters,
                                  origin, orientation, Seed, 7ul, primitives, anchors);

            return primitives;
        }

        private static RasterResult Rasterise(
            NativeArray<Primitive> primitives,
            int3 min,
            int3 max,
            ref RegionTable table,
            ref BrickPool pool,
            bool markHardSurface = false)
        {
            var reads = new RegionReadSource(in table, in pool);
            var mutations = new RegionMutationStore(in table, in pool);
            return PrimitiveRasteriser.Rasterise(
                primitives, min, max, reads, mutations, markHardSurface);
        }

    }
}
