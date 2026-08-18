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
    /// End-to-end determinism guard for WB030. Primitive determinism is covered by
    /// ShapeProgramTests.EvaluationIsDeterministic; these tests prove that the same catalogue and
    /// world seed also produce identical authoritative voxel cells after rasterisation and that
    /// semantic child streams remain stable without consuming shared mutable RNG state.
    /// </summary>
    public sealed class StructureSeedVoxelDeterminismTests
    {
        private const uint WorldSeed = 0x5EED1234u;
        private static readonly int3 CottageRegion = new(4, 0, 6);

        [Test]
        public void SameCatalogueAndSeedProduceIdenticalVoxelOutput()
        {
            FeatureCatalogue catalogue = CottageFixture.Build(Allocator.Persistent, placements: 1);
            Assert.AreEqual(CatalogueLoadResult.Ok, FeatureCatalogueBuilder.Finalise(ref catalogue));

            try
            {
                int3 min = catalogue.ExplicitPlacements[0].Position;
                int3 max = min + catalogue.Definitions[CottageFixture.CottageId].Footprint;

                byte[] first = GenerateSnapshot(in catalogue, min, max);
                byte[] second = GenerateSnapshot(in catalogue, min, max);

                int difference = SubVolumeEquality.FirstDifference(first, second);
                Assert.AreEqual(-1, difference,
                    $"voxel {difference} differs between identical catalogue/seed builds");
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        [Test]
        public void SemanticChildSeedsAreStableAndIndependent()
        {
            const ulong parentSeed = 0x7D31A5B9C2E4F607ul;
            FixedString64Bytes wallKey = new("wall.front");
            FixedString64Bytes roofKey = new("roof.main");

            ulong firstWall = StructureSeed.Child(parentSeed, in wallKey);
            ulong roof = StructureSeed.Child(parentSeed, in roofKey);
            ulong secondWall = StructureSeed.Child(parentSeed, in wallKey);

            Assert.AreEqual(firstWall, secondWall,
                "deriving an unrelated semantic child must not perturb an existing child stream");
            Assert.AreNotEqual(firstWall, roof,
                "different semantic keys should resolve to independent child streams");

            ulong firstDormer = StructureSeed.Child(parentSeed, in roofKey, ordinal: 3);
            ulong secondDormer = StructureSeed.Child(parentSeed, in roofKey, ordinal: 3);
            ulong nextDormer = StructureSeed.Child(parentSeed, in roofKey, ordinal: 4);

            Assert.AreEqual(firstDormer, secondDormer,
                "the same semantic key and ordinal must always derive the same child seed");
            Assert.AreNotEqual(firstDormer, nextDormer,
                "different ordinals under one semantic key should resolve to independent streams");
        }

        private static byte[] GenerateSnapshot(
            in FeatureCatalogue catalogue,
            int3 min,
            int3 max)
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
                    "determinism comparison is vacuous if the fixture writes no voxels");

                return SubVolumeEquality.Snapshot(ref table, in pool, min, max);
            }
            finally
            {
                pool.Dispose();
                table.Dispose();
            }
        }
    }
}
