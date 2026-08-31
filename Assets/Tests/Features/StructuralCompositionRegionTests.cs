using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
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
                Assert.AreEqual(firstReport.PrimitiveCost, secondReport.PrimitiveCost);
                Assert.AreEqual(firstReport.VoxelCost, secondReport.VoxelCost);
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
                    Assert.AreEqual(first[i].AttachmentPosition, second[i].AttachmentPosition);
                    Assert.AreEqual(first[i].Orientation, second[i].Orientation);
                }

                for (int i = 0; i < firstDecisions.Length; i++)
                {
                    Assert.AreEqual(firstDecisions[i].SemanticStructureId, secondDecisions[i].SemanticStructureId);
                    Assert.AreEqual(firstDecisions[i].ParentIndex, secondDecisions[i].ParentIndex);
                    Assert.AreEqual(firstDecisions[i].SocketId, secondDecisions[i].SocketId);
                    Assert.AreEqual(firstDecisions[i].ChildPieceId, secondDecisions[i].ChildPieceId);
                    Assert.AreEqual(firstDecisions[i].Position, secondDecisions[i].Position);
                    Assert.AreEqual(firstDecisions[i].AttachmentPosition, secondDecisions[i].AttachmentPosition);
                    Assert.AreEqual(firstDecisions[i].Orientation, secondDecisions[i].Orientation);
                    Assert.AreEqual(firstDecisions[i].SocketFlags, secondDecisions[i].SocketFlags);
                    Assert.AreEqual(firstDecisions[i].DecorationHandoff, secondDecisions[i].DecorationHandoff);
                    Assert.AreEqual(firstDecisions[i].Rejection, secondDecisions[i].Rejection);
                }
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        [Test]
        public void RequiredSocketWithNoRequestedChildFailsExplicitly()
        {
            FeatureCatalogue catalogue = StructuralCompositionFixture.Build(Allocator.Temp);
            try
            {
                SlotSpec slot = catalogue.Slots[0];
                slot.CountMin = 0;
                slot.CountMax = 0;
                slot.Flags = StructuralSocketFlags.Required;
                catalogue.Slots[0] = slot;
                Assert.AreEqual(CatalogueLoadResult.Ok, FeatureCatalogueBuilder.Finalise(ref catalogue));

                using var instances = new NativeList<StructuralInstance>(Allocator.Temp);
                using var decisions = new NativeList<StructuralAttachmentDecision>(Allocator.Temp);
                StructuralCompositionReport report = StructuralCompositionPlanner.ExpandRoot(
                    in catalogue, Seed, StructuralCompositionFixture.RootId,
                    catalogue.ExplicitPlacements[0], instances, decisions);

                Assert.AreEqual(StructuralCompositionResult.RequiredSocketUnresolved, report.Result);
                Assert.AreEqual(0, report.ChildCount);
                Assert.AreEqual(1, decisions.Length);
                Assert.IsFalse(decisions[0].Accepted);
                Assert.AreEqual(StructuralAttachmentRejectReason.RequiredEmpty, decisions[0].Rejection);
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        [Test]
        public void OptionalSocketMayRemainEmptyWithoutFailingComposition()
        {
            FeatureCatalogue catalogue = StructuralCompositionFixture.Build(Allocator.Temp);
            try
            {
                SlotSpec slot = catalogue.Slots[0];
                slot.CountMin = 0;
                slot.CountMax = 0;
                slot.Flags = StructuralSocketFlags.None;
                catalogue.Slots[0] = slot;
                Assert.AreEqual(CatalogueLoadResult.Ok, FeatureCatalogueBuilder.Finalise(ref catalogue));

                using var instances = new NativeList<StructuralInstance>(Allocator.Temp);
                StructuralCompositionReport report = StructuralCompositionPlanner.ExpandRoot(
                    in catalogue, Seed, StructuralCompositionFixture.RootId,
                    catalogue.ExplicitPlacements[0], instances);

                Assert.AreEqual(StructuralCompositionResult.Ok, report.Result);
                Assert.AreEqual(0, report.ChildCount);
                Assert.AreEqual(1, instances.Length);
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        [Test]
        public void IncompatibleSemanticChildRejectsWithInspectableReason()
        {
            FeatureCatalogue catalogue = StructuralCompositionFixture.Build(Allocator.Temp);
            try
            {
                FeatureDefinition child = catalogue.Definitions[StructuralCompositionFixture.ChildId];
                StructuralPieceSpec piece = child.StructuralPiece;
                piece.Offers = 1UL << 21;
                piece.Accepts = 1UL << 21;
                child.StructuralPiece = piece;
                catalogue.Definitions[StructuralCompositionFixture.ChildId] = child;

                using var instances = new NativeList<StructuralInstance>(Allocator.Temp);
                using var decisions = new NativeList<StructuralAttachmentDecision>(Allocator.Temp);
                StructuralCompositionReport report = StructuralCompositionPlanner.ExpandRoot(
                    in catalogue, Seed, StructuralCompositionFixture.RootId,
                    catalogue.ExplicitPlacements[0], instances, decisions);

                Assert.AreEqual(StructuralCompositionResult.Incompatible, report.Result);
                Assert.AreEqual(1, decisions.Length);
                Assert.AreEqual(StructuralAttachmentRejectReason.IncompatibleRoleOrTags, decisions[0].Rejection);
                Assert.IsFalse(decisions[0].Accepted);
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        [Test]
        public void AcceptedDecisionCarriesSupportLossAndDecorationHandoffMetadata()
        {
            FeatureCatalogue catalogue = StructuralCompositionFixture.Build(Allocator.Temp);
            try
            {
                SlotSpec slot = catalogue.Slots[0];
                slot.Flags = StructuralSocketFlags.Required |
                             StructuralSocketFlags.InvalidateOnSupportLoss |
                             StructuralSocketFlags.DecorationHandoff;
                slot.DecorationHandoff = StructuralDecorationHandoff.Floor |
                                         StructuralDecorationHandoff.Wall;
                slot.SupportProbeMin = new int3(-2, -3, -2);
                slot.SupportProbeMax = new int3(3, 1, 3);
                slot.MinimumSupportContacts = 2;
                catalogue.Slots[0] = slot;
                Assert.AreEqual(CatalogueLoadResult.Ok, FeatureCatalogueBuilder.Finalise(ref catalogue));

                using var instances = new NativeList<StructuralInstance>(Allocator.Temp);
                using var decisions = new NativeList<StructuralAttachmentDecision>(Allocator.Temp);
                StructuralCompositionReport report = StructuralCompositionPlanner.ExpandRoot(
                    in catalogue, Seed, StructuralCompositionFixture.RootId,
                    catalogue.ExplicitPlacements[0], instances, decisions);

                Assert.AreEqual(StructuralCompositionResult.Ok, report.Result);
                Assert.AreEqual(1, decisions.Length);
                Assert.IsTrue(decisions[0].Accepted);
                Assert.IsTrue(decisions[0].InvalidatesOnSupportLoss);
                Assert.AreEqual(slot.DecorationHandoff, decisions[0].DecorationHandoff);
                Assert.AreEqual(slot.MinimumSupportContacts, decisions[0].MinimumSupportContacts);
                Assert.AreEqual(decisions[0].AttachmentPosition + slot.SupportProbeMin,
                    decisions[0].SupportProbeMin);
                Assert.AreEqual(decisions[0].AttachmentPosition + slot.SupportProbeMax,
                    decisions[0].SupportProbeMax);
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        [Test]
        public void OversizedRootVoxelCostFailsBeforeExpansion()
        {
            FeatureCatalogue catalogue = StructuralCompositionFixture.Build(Allocator.Temp);
            try
            {
                FeatureDefinition root = catalogue.Definitions[StructuralCompositionFixture.RootId];
                root.Footprint = new int3(FeatureBudget.MaxFootprintVoxels,
                    FeatureBudget.MaxFootprintVoxels, 11);
                catalogue.Definitions[StructuralCompositionFixture.RootId] = root;

                using var instances = new NativeList<StructuralInstance>(Allocator.Temp);
                StructuralCompositionReport report = StructuralCompositionPlanner.ExpandRoot(
                    in catalogue, Seed, StructuralCompositionFixture.RootId,
                    catalogue.ExplicitPlacements[0], instances);

                Assert.AreEqual(StructuralCompositionResult.VoxelBudgetExceeded, report.Result);
                Assert.Greater(report.VoxelCost, FeatureBudget.MaxCompositionVoxelCost);
                Assert.AreEqual(0, report.ChildCount);
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        [Test]
        public void RegionGenerationOrderDoesNotAlterComposedChildVoxels()
        {
            FeatureCatalogue catalogue = StructuralCompositionFixture.Build(Allocator.Persistent);
            var firstTable = new RegionTable(8, Allocator.Persistent);
            var firstPool = new BrickPool(4096, Allocator.Persistent);
            var secondTable = new RegionTable(8, Allocator.Persistent);
            var secondPool = new BrickPool(4096, Allocator.Persistent);
            try
            {
                Assert.AreEqual(CatalogueLoadResult.Ok, FeatureCatalogueBuilder.Finalise(ref catalogue));

                var firstReads = new RegionReadSource(in firstTable, in firstPool);
                var firstMutations = new RegionMutationStore(in firstTable, in firstPool);
                FeatureGeneration.GenerateRegion(in catalogue, Seed,
                    StructuralCompositionFixture.RootRegion, firstReads, firstMutations);
                FeatureGeneration.GenerateRegion(in catalogue, Seed,
                    StructuralCompositionFixture.ChildRegion, firstReads, firstMutations);

                var secondReads = new RegionReadSource(in secondTable, in secondPool);
                var secondMutations = new RegionMutationStore(in secondTable, in secondPool);
                FeatureGeneration.GenerateRegion(in catalogue, Seed,
                    StructuralCompositionFixture.ChildRegion, secondReads, secondMutations);
                FeatureGeneration.GenerateRegion(in catalogue, Seed,
                    StructuralCompositionFixture.RootRegion, secondReads, secondMutations);

                int3 min = StructuralCompositionFixture.ChildPosition;
                int3 max = min + StructuralCompositionFixture.ChildFootprint;
                byte[] firstSnapshot = SubVolumeEquality.Snapshot(ref firstTable, in firstPool, min, max);
                byte[] secondSnapshot = SubVolumeEquality.Snapshot(ref secondTable, in secondPool, min, max);
                Assert.AreEqual(-1, SubVolumeEquality.FirstDifference(firstSnapshot, secondSnapshot),
                    "authoritative composed voxels must not depend on which logical region generated first");
            }
            finally
            {
                secondPool.Dispose();
                secondTable.Dispose();
                firstPool.Dispose();
                firstTable.Dispose();
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
