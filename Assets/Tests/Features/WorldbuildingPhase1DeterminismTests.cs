using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;
using VoxelEngine.Tests.Features.Fixtures;

namespace VoxelEngine.Tests.Features
{
    /// <summary>
    /// WB030 contract coverage. Shared structure authoring may add semantic configuration, but the
    /// authoritative FeatureDefinition -> ShapeProgram -> Primitive -> voxel path must remain a pure
    /// function of configuration and seed. Semantic child seeds must also remain order-independent.
    /// </summary>
    public sealed class WorldbuildingPhase1DeterminismTests
    {
        private const uint WorldSeed = 0x5EED1234u;
        private static readonly int3 CottageRegion = new(4, 0, 6);

        [Test]
        public void SameConfigAndSeedProduceIdenticalPrimitiveOutput()
        {
            FeatureCatalogue catalogue = CottageFixture.Build(Allocator.Temp);
            Assert.AreEqual(CatalogueLoadResult.Ok, FeatureCatalogueBuilder.Finalise(ref catalogue));

            var firstPrimitives = new NativeList<Primitive>(32, Allocator.Temp);
            var secondPrimitives = new NativeList<Primitive>(32, Allocator.Temp);
            var firstAnchors = new NativeList<ResolvedAnchor>(4, Allocator.Temp);
            var secondAnchors = new NativeList<ResolvedAnchor>(4, Allocator.Temp);
            try
            {
                ParameterSet parameters = DefaultParameters(in catalogue);
                int3 origin = catalogue.ExplicitPlacements[0].Position;
                const byte orientation = 2;
                const ulong instanceSeed = 0x0123456789ABCDEFul;

                Assert.AreEqual(EvaluationResult.Ok,
                    ShapeProgram.Evaluate(
                        in catalogue, CottageFixture.CottageId, in parameters,
                        origin, orientation, WorldSeed, instanceSeed,
                        firstPrimitives, firstAnchors));
                Assert.AreEqual(EvaluationResult.Ok,
                    ShapeProgram.Evaluate(
                        in catalogue, CottageFixture.CottageId, in parameters,
                        origin, orientation, WorldSeed, instanceSeed,
                        secondPrimitives, secondAnchors));

                Assert.AreEqual(firstPrimitives.Length, secondPrimitives.Length);
                Assert.AreEqual(firstAnchors.Length, secondAnchors.Length);
                for (var i = 0; i < firstPrimitives.Length; i++)
                    AssertPrimitiveEqual(firstPrimitives[i], secondPrimitives[i], i);

                for (var i = 0; i < firstAnchors.Length; i++)
                {
                    Assert.AreEqual(firstAnchors[i].Name, secondAnchors[i].Name, $"anchor {i} name");
                    Assert.AreEqual(firstAnchors[i].Position, secondAnchors[i].Position, $"anchor {i} position");
                    Assert.AreEqual(firstAnchors[i].Facing, secondAnchors[i].Facing, $"anchor {i} facing");
                }
            }
            finally
            {
                firstPrimitives.Dispose();
                secondPrimitives.Dispose();
                firstAnchors.Dispose();
                secondAnchors.Dispose();
                catalogue.Dispose();
            }
        }

        [Test]
        public void SameConfigAndSeedProduceIdenticalVoxelOutput()
        {
            FeatureCatalogue catalogue = CottageFixture.Build(Allocator.Persistent);
            Assert.AreEqual(CatalogueLoadResult.Ok, FeatureCatalogueBuilder.Finalise(ref catalogue));
            try
            {
                byte[] first = GenerateSnapshot(in catalogue);
                byte[] second = GenerateSnapshot(in catalogue);

                Assert.Greater(first.Length, 0, "determinism comparison must cover a non-empty volume");
                int difference = SubVolumeEquality.FirstDifference(first, second);
                Assert.AreEqual(-1, difference,
                    $"voxel {difference} differs between identical config/seed runs");
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        [Test]
        public void SemanticChildSeedsAreStableAndOrderIndependent()
        {
            const ulong parentSeed = 0xDEADBEEFCAFEBABEul;
            var roof = new FixedString64Bytes("roof");
            var windows = new FixedString64Bytes("windows.north");
            var unrelated = new FixedString64Bytes("optional.porch-detail");

            ulong roofBefore = StructureSeed.Child(parentSeed, in roof);
            _ = StructureSeed.Child(parentSeed, in unrelated);
            ulong roofAfter = StructureSeed.Child(parentSeed, in roof);

            Assert.AreEqual(roofBefore, roofAfter,
                "evaluating an unrelated optional component must not perturb an existing child seed");
            Assert.AreNotEqual(roofBefore, StructureSeed.Child(parentSeed, in windows));
            Assert.AreNotEqual(roofBefore, StructureSeed.Child(parentSeed, in roof, ordinal: 1));
        }

        private static ParameterSet DefaultParameters(in FeatureCatalogue catalogue)
        {
            FeatureDefinition definition = catalogue.Definitions[CottageFixture.CottageId];
            var parameters = new ParameterSet();
            for (var i = 0; i < definition.ParameterCount; i++)
                parameters[i] = catalogue.Parameters[definition.ParameterOffset + i].Default;
            return parameters;
        }

        private static byte[] GenerateSnapshot(in FeatureCatalogue catalogue)
        {
            var table = new RegionTable(8, Allocator.Persistent);
            var pool = new BrickPool(8192, Allocator.Persistent);
            try
            {
                var reads = new RegionReadSource(in table, in pool);
                var mutations = new RegionMutationStore(in table, in pool);
                FeatureGenerationReport report = FeatureGeneration.GenerateRegion(
                    in catalogue, WorldSeed, CottageRegion, reads, mutations);

                Assert.Greater(report.VoxelsWritten, 0,
                    "the deterministic voxel fixture must actually write voxels");

                int3 min = catalogue.ExplicitPlacements[0].Position;
                int3 max = min + catalogue.Definitions[CottageFixture.CottageId].Footprint;
                return SubVolumeEquality.Snapshot(ref table, in pool, min, max);
            }
            finally
            {
                pool.Dispose();
                table.Dispose();
            }
        }

        private static void AssertPrimitiveEqual(Primitive expected, Primitive actual, int index)
        {
            Assert.AreEqual(expected.Shape, actual.Shape, $"primitive {index} shape");
            Assert.AreEqual(expected.Mode, actual.Mode, $"primitive {index} mode");
            Assert.AreEqual(expected.Material, actual.Material, $"primitive {index} material");
            Assert.AreEqual(expected.SurfaceStyle, actual.SurfaceStyle, $"primitive {index} surface style");
            Assert.AreEqual(expected.Coating, actual.Coating, $"primitive {index} coating");
            Assert.AreEqual(expected.SurfaceFlags, actual.SurfaceFlags, $"primitive {index} surface flags");
            Assert.AreEqual(expected.SurfaceDetail, actual.SurfaceDetail, $"primitive {index} surface detail");
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
