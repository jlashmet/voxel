using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;
using VoxelEngine.Tests.Features.Fixtures;

namespace VoxelEngine.Tests.Features
{
    /// <summary>Completion coverage for the shared deterministic authoring foundation.</summary>
    public sealed class StructureFoundationCompletionTests
    {
        private const uint TerrainSeed = 4242u;
        private const ulong InstanceSeed = 0x1122334455667788ul;

        [Test]
        public void SharedValidationRejectsEveryPhaseOneFailureClass()
        {
            var invalidOpening = new OpeningConfig
            {
                Kind = StructureOpeningKind.Window,
                Width = 0,
                Height = 8,
                Spacing = 12,
            };
            Assert.AreEqual(
                StructureComponentValidationIssue.InvalidDimension,
                StructureComponentValidation.Opening(in invalidOpening, 64));

            var impossibleSpacing = new OpeningConfig
            {
                Kind = StructureOpeningKind.Window,
                Width = 8,
                Height = 8,
                Spacing = 6,
                StartMargin = 2,
                EndMargin = 2,
            };
            Assert.AreEqual(
                StructureComponentValidationIssue.ImpossibleOpeningSpacing,
                StructureComponentValidation.Opening(in impossibleSpacing, 64));

            var unsupportedRoof = new RoofConfig
            {
                Style = RoofStyle.Gable,
                PitchRise = 1,
                PitchRun = 0,
                Thickness = 2,
            };
            Assert.AreEqual(
                StructureComponentValidationIssue.UnsupportedRoofCombination,
                StructureComponentValidation.Roof(in unsupportedRoof));

            var bounds = new StructureGenerationBounds(int3.zero, new int3(16, 16, 16));
            Assert.AreEqual(
                StructureComponentValidationIssue.BoundsOverflow,
                StructureComponentValidation.VolumeWithinBounds(
                    in bounds, new int3(8, 8, 8), new int3(17, 12, 12)));

            Assert.AreEqual(
                StructureComponentValidationIssue.PrimitiveBudgetOverflow,
                StructureComponentValidation.PrimitiveBudget(
                    FeatureBudget.MaxPrimitivesPerInstance + 1,
                    FeatureBudget.MaxPrimitivesPerInstance));
        }

        [Test]
        public void SameConfigAndSeedProduceIdenticalPrimitivesVoxelsAndSemanticSubSeeds()
        {
            var catalogue = CottageFixture.Build(Allocator.Temp);
            Assert.AreEqual(CatalogueLoadResult.Ok, FeatureCatalogueBuilder.Finalise(ref catalogue));

            var origin = new int3(512, 220, 512);
            var first = Evaluate(in catalogue, origin, out var firstAnchors);
            var second = Evaluate(in catalogue, origin, out var secondAnchors);

            try
            {
                Assert.AreEqual(first.Length, second.Length, "primitive count changed for identical inputs");
                for (var i = 0; i < first.Length; i++)
                {
                    Primitive expected = first[i];
                    Primitive actual = second[i];
                    AssertPrimitiveEqual(in expected, in actual, i);
                }

                Assert.AreEqual(firstAnchors.Length, secondAnchors.Length);
                for (var i = 0; i < firstAnchors.Length; i++)
                {
                    Assert.AreEqual(firstAnchors[i].Name, secondAnchors[i].Name);
                    Assert.AreEqual(firstAnchors[i].Position, secondAnchors[i].Position);
                    Assert.AreEqual(firstAnchors[i].Facing, secondAnchors[i].Facing);
                }

                var definition = catalogue.Definitions[CottageFixture.CottageId];
                int3 min = origin;
                int3 max = origin + definition.Footprint;

                var firstTable = new RegionTable(4, Allocator.Temp);
                var firstPool = new BrickPool(8192, Allocator.Temp);
                var secondTable = new RegionTable(4, Allocator.Temp);
                var secondPool = new BrickPool(8192, Allocator.Temp);

                try
                {
                    Rasterise(first.AsArray(), min, max, ref firstTable, ref firstPool);
                    Rasterise(second.AsArray(), min, max, ref secondTable, ref secondPool);

                    byte[] firstVoxels = SubVolumeEquality.Snapshot(ref firstTable, in firstPool, min, max);
                    byte[] secondVoxels = SubVolumeEquality.Snapshot(ref secondTable, in secondPool, min, max);
                    Assert.AreEqual(-1, SubVolumeEquality.FirstDifference(firstVoxels, secondVoxels),
                        "identical config/seed produced different voxel output");
                }
                finally
                {
                    firstTable.Dispose();
                    firstPool.Dispose();
                    secondTable.Dispose();
                    secondPool.Dispose();
                }

                var roof = new FixedString64Bytes("roof");
                var windows = new FixedString64Bytes("windows");
                var unrelated = new FixedString64Bytes("porch-detail");
                ulong roofBefore = StructureSeed.Child(InstanceSeed, in roof);
                _ = StructureSeed.Child(InstanceSeed, in unrelated);
                ulong roofAfter = StructureSeed.Child(InstanceSeed, in roof);

                Assert.AreEqual(roofBefore, roofAfter,
                    "an unrelated semantic child changed an existing child seed");
                Assert.AreNotEqual(roofBefore, StructureSeed.Child(InstanceSeed, in windows));
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

        private static NativeList<Primitive> Evaluate(
            in FeatureCatalogue catalogue,
            int3 origin,
            out NativeList<ResolvedAnchor> anchors)
        {
            var definition = catalogue.Definitions[CottageFixture.CottageId];
            var parameters = new ParameterSet();
            for (var i = 0; i < definition.ParameterCount; i++)
                parameters[i] = catalogue.Parameters[definition.ParameterOffset + i].Default;

            var primitives = new NativeList<Primitive>(64, Allocator.Temp);
            anchors = new NativeList<ResolvedAnchor>(4, Allocator.Temp);
            var result = ShapeProgram.Evaluate(
                in catalogue,
                CottageFixture.CottageId,
                in parameters,
                origin,
                0,
                TerrainSeed,
                InstanceSeed,
                primitives,
                anchors);
            Assert.AreEqual(EvaluationResult.Ok, result);
            return primitives;
        }

        private static void Rasterise(
            NativeArray<Primitive> primitives,
            int3 min,
            int3 max,
            ref RegionTable table,
            ref BrickPool pool)
        {
            var reads = new RegionReadSource(in table, in pool);
            var mutations = new RegionMutationStore(in table, in pool);
            var result = PrimitiveRasteriser.Rasterise(primitives, min, max, reads, mutations, true);
            Assert.IsFalse(result.BudgetExceeded);
        }

        private static void AssertPrimitiveEqual(in Primitive expected, in Primitive actual, int index)
        {
            Assert.AreEqual(expected.Shape, actual.Shape, $"primitive {index} shape");
            Assert.AreEqual(expected.Mode, actual.Mode, $"primitive {index} mode");
            Assert.AreEqual(expected.Material, actual.Material, $"primitive {index} material");
            Assert.AreEqual(expected.SurfaceStyle, actual.SurfaceStyle, $"primitive {index} surface style");
            Assert.AreEqual(expected.Coating, actual.Coating, $"primitive {index} coating");
            Assert.AreEqual(expected.SurfaceFlags, actual.SurfaceFlags, $"primitive {index} flags");
            Assert.AreEqual(expected.SurfaceDetail, actual.SurfaceDetail, $"primitive {index} detail");
            Assert.AreEqual(expected.Axis, actual.Axis, $"primitive {index} axis");
            Assert.AreEqual(expected.Direction, actual.Direction, $"primitive {index} direction");
            Assert.AreEqual(expected.Profile, actual.Profile, $"primitive {index} profile");
            Assert.AreEqual(expected.Order, actual.Order, $"primitive {index} order");
            Assert.AreEqual(expected.A, actual.A, $"primitive {index} A");
            Assert.AreEqual(expected.B, actual.B, $"primitive {index} B");
            Assert.AreEqual(expected.Radius, actual.Radius, $"primitive {index} radius");
            Assert.AreEqual(expected.InnerRadius, actual.InnerRadius, $"primitive {index} inner radius");
            Assert.AreEqual(expected.C, actual.C, $"primitive {index} C");
            Assert.AreEqual(expected.D, actual.D, $"primitive {index} D");
            Assert.AreEqual(expected.StartDirection, actual.StartDirection, $"primitive {index} start direction");
            Assert.AreEqual(expected.EndDirection, actual.EndDirection, $"primitive {index} end direction");
        }
    }
}
