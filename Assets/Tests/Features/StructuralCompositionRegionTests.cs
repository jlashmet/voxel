using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;
using VoxelEngine.Tests.Features.Fixtures;

namespace VoxelEngine.Tests.Features
{
    public sealed class StructuralCompositionRegionTests
    {
        private const uint Seed = 0x51A7C0DEu;

        [Test]
        public void SameSeedAndInputProduceIdenticalAttachmentGraph()
        {
            FeatureCatalogue catalogue = StructuralCompositionFixture.Build(Allocator.Temp);
            try
            {
                Assert.AreEqual(CatalogueLoadResult.Ok, FeatureCatalogueBuilder.Finalise(ref catalogue));

                using var first = new NativeList<StructuralInstance>(Allocator.Temp);
                using var second = new NativeList<StructuralInstance>(Allocator.Temp);
                using var firstDecisions = new NativeList<StructuralAttachmentDecision>(Allocator.Temp);
                using var secondDecisions = new NativeList<StructuralAttachmentDecision>(Allocator.Temp);

                StructuralCompositionReport firstReport = StructuralCompositionPlanner.ExpandRoot(
                    in catalogue, Seed, StructuralCompositionFixture.RootId,
                    catalogue.ExplicitPlacements[0], first, firstDecisions);
                StructuralCompositionReport secondReport = StructuralCompositionPlanner.ExpandRoot(
                    in catalogue, Seed, StructuralCompositionFixture.RootId,
                    catalogue.ExplicitPlacements[0], second, secondDecisions);

                Assert.AreEqual(StructuralCompositionResult.Ok, firstReport.Result);
                Assert.AreEqual(firstReport.GraphHash, secondReport.GraphHash);
                Assert.AreEqual(firstReport.ChildCount, secondReport.ChildCount);
                Assert.AreEqual(first.Length, second.Length);
                Assert.AreEqual(firstDecisions.Length, secondDecisions.Length);

                for (int i = 0; i < first.Length; i++)
                {
                    Assert.AreEqual(first[i].SemanticStructureId, second[i].SemanticStructureId);
                    Assert.AreEqual(first[i].InstanceId, second[i].InstanceId);
                    Assert.AreEqual(first[i].DefinitionId, second[i].DefinitionId);
                    Assert.AreEqual(first[i].PieceId, second[i].PieceId);
                    Assert.AreEqual(first[i].ParentIndex, second[i].ParentIndex);
                    Assert.AreEqual(first[i].ParentSocketId, second[i].ParentSocketId);
                    Assert.AreEqual(first[i].Position, second[i].Position);
                    Assert.AreEqual(first[i].Orientation, second[i].Orientation);
                }
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        [Test]
        public void ChildOutsideRootRegionRasterisesAsAuthoritativeRegionVoxels()
        {
            FeatureCatalogue catalogue = StructuralCompositionFixture.Build(Allocator.Persistent);
            var table = new RegionTable(8, Allocator.Persistent);
            var pool = new BrickPool(4096, Allocator.Persistent);
            try
            {
                Assert.AreEqual(CatalogueLoadResult.Ok, FeatureCatalogueBuilder.Finalise(ref catalogue));
                var reads = new RegionReadSource(in table, in pool);
                var mutations = new RegionMutationStore(in table, in pool);

                FeatureGenerationReport report = FeatureGeneration.GenerateRegion(
                    in catalogue, Seed, StructuralCompositionFixture.ChildRegion, reads, mutations);

                Assert.AreEqual(StructuralCompositionResult.Ok, report.LastCompositionResult);
                Assert.AreEqual(1, report.StructuralRootsPlanned);
                Assert.AreEqual(1, report.StructuralChildrenPlanned);
                Assert.AreEqual(1, report.InstancesRasterised,
                    "the explicit root ends at the previous region boundary; only its composed child can rasterise here");
                Assert.Greater(report.VoxelsWritten, 0,
                    "accepted descendants must become authoritative storage voxels in their own logical region");
                Assert.IsFalse(report.BudgetExceeded);
            }
            finally
            {
                pool.Dispose();
                table.Dispose();
                catalogue.Dispose();
            }
        }
    }
}