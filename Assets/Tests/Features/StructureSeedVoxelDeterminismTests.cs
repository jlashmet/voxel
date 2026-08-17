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
    /// ShapeProgramTests.EvaluationIsDeterministic; this test proves that the same catalogue and
    /// world seed also produce identical authoritative voxel cells after rasterisation.
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
