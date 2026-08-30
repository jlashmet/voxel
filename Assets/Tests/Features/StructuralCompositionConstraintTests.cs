using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;
using VoxelEngine.Tests.Features.Fixtures;

namespace VoxelEngine.Tests.Features
{
    public sealed class StructuralCompositionConstraintTests
    {
        private const uint Seed = 0x51A7C0DEu;

        [Test]
        public void OptionalRejectedCandidateDoesNotChangeAcceptedGraphHash()
        {
            FeatureCatalogue empty = StructuralCompositionFixture.Build(Allocator.Temp);
            FeatureCatalogue rejected = StructuralCompositionFixture.Build(Allocator.Temp);
            try
            {
                SlotSpec emptySlot = empty.Slots[0];
                emptySlot.CountMin = 0;
                emptySlot.CountMax = 0;
                emptySlot.Flags = StructuralSocketFlags.None;
                empty.Slots[0] = emptySlot;
                Assert.AreEqual(CatalogueLoadResult.Ok, FeatureCatalogueBuilder.Finalise(ref empty));

                SlotSpec rejectedSlot = rejected.Slots[0];
                rejectedSlot.Flags = StructuralSocketFlags.None;
                rejected.Slots[0] = rejectedSlot;
                FeatureDefinition incompatibleChild = rejected.Definitions[StructuralCompositionFixture.ChildId];
                StructuralPieceSpec incompatiblePiece = incompatibleChild.StructuralPiece;
                incompatiblePiece.Offers = 1UL << 21;
                incompatiblePiece.Accepts = 1UL << 21;
                incompatibleChild.StructuralPiece = incompatiblePiece;
                rejected.Definitions[StructuralCompositionFixture.ChildId] = incompatibleChild;

                using var emptyInstances = new NativeList<StructuralInstance>(Allocator.Temp);
                using var rejectedInstances = new NativeList<StructuralInstance>(Allocator.Temp);
                using var decisions = new NativeList<StructuralAttachmentDecision>(Allocator.Temp);
                StructuralCompositionReport emptyReport = StructuralCompositionPlanner.ExpandRoot(
                    in empty, Seed, StructuralCompositionFixture.RootId,
                    empty.ExplicitPlacements[0], emptyInstances);
                StructuralCompositionReport rejectedReport = StructuralCompositionPlanner.ExpandRoot(
                    in rejected, Seed, StructuralCompositionFixture.RootId,
                    rejected.ExplicitPlacements[0], rejectedInstances, decisions);

                Assert.AreEqual(StructuralCompositionResult.Ok, emptyReport.Result);
                Assert.AreEqual(StructuralCompositionResult.Ok, rejectedReport.Result);
                Assert.AreEqual(0, rejectedReport.ChildCount);
                Assert.AreEqual(1, decisions.Length);
                Assert.AreEqual(StructuralAttachmentRejectReason.IncompatibleRoleOrTags,
                    decisions[0].Rejection);
                Assert.AreEqual(emptyReport.GraphHash, rejectedReport.GraphHash,
                    "diagnostic-only rejected alternatives must not perturb accepted graph identity");
            }
            finally
            {
                rejected.Dispose();
                empty.Dispose();
            }
        }

        [Test]
        public void ReservedClearanceIsCenteredOnChosenRangedAttachment()
        {
            FeatureCatalogue catalogue = StructuralCompositionFixture.Build(Allocator.Temp);
            try
            {
                FeatureDefinition child = catalogue.Definitions[StructuralCompositionFixture.ChildId];
                child.Footprint = new int3(1, 1, 1);
                catalogue.Definitions[StructuralCompositionFixture.ChildId] = child;

                SlotSpec slot = catalogue.Slots[0];
                slot.LocalPosition = new int3(StructuralCompositionFixture.RootFootprint.x, 0, 1000);
                slot.LocalMin = new int3(StructuralCompositionFixture.RootFootprint.x, 0, 0);
                slot.LocalMax = new int3(StructuralCompositionFixture.RootFootprint.x, 0, 32);
                slot.CountMin = 2;
                slot.CountMax = 2;
                slot.Capacity = 2;
                slot.Spacing = 16;
                slot.ClearanceMin = new int3(-33, 0, -33);
                slot.ClearanceMax = new int3(33, 2, 33);
                catalogue.Slots[0] = slot;

                using var instances = new NativeList<StructuralInstance>(Allocator.Temp);
                using var decisions = new NativeList<StructuralAttachmentDecision>(Allocator.Temp);
                StructuralCompositionReport report = StructuralCompositionPlanner.ExpandRoot(
                    in catalogue, Seed, StructuralCompositionFixture.RootId,
                    catalogue.ExplicitPlacements[0], instances, decisions);

                Assert.AreEqual(StructuralCompositionResult.ClearanceBlocked, report.Result);
                Assert.AreEqual(1, report.ChildCount,
                    "first ranged child should place; the second must hit the chosen attachment's reserved volume");
                Assert.AreEqual(2, decisions.Length);
                Assert.IsTrue(decisions[0].Accepted);
                Assert.IsFalse(decisions[1].Accepted);
                Assert.AreEqual(StructuralAttachmentRejectReason.ClearanceBlocked, decisions[1].Rejection);
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        [Test]
        public void RepeatedAttachmentSpacingIsPairwiseAndBounded()
        {
            FeatureCatalogue catalogue = StructuralCompositionFixture.Build(Allocator.Temp);
            try
            {
                FeatureDefinition child = catalogue.Definitions[StructuralCompositionFixture.ChildId];
                child.Footprint = new int3(1, 1, 1);
                catalogue.Definitions[StructuralCompositionFixture.ChildId] = child;

                SlotSpec slot = catalogue.Slots[0];
                slot.LocalMin = slot.LocalPosition;
                slot.LocalMax = slot.LocalPosition;
                slot.CountMin = 2;
                slot.CountMax = 2;
                slot.Capacity = 2;
                slot.Spacing = 2;
                catalogue.Slots[0] = slot;

                using var instances = new NativeList<StructuralInstance>(Allocator.Temp);
                using var decisions = new NativeList<StructuralAttachmentDecision>(Allocator.Temp);
                StructuralCompositionReport report = StructuralCompositionPlanner.ExpandRoot(
                    in catalogue, Seed, StructuralCompositionFixture.RootId,
                    catalogue.ExplicitPlacements[0], instances, decisions);

                Assert.AreEqual(StructuralCompositionResult.ClearanceBlocked, report.Result);
                Assert.AreEqual(1, report.ChildCount);
                Assert.AreEqual(2, decisions.Length);
                Assert.AreEqual(StructuralAttachmentRejectReason.SpacingBlocked, decisions[1].Rejection);
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        [Test]
        public void MissingTerrainSupportRejectsWithSpecificReason()
        {
            FeatureCatalogue catalogue = StructuralCompositionFixture.Build(Allocator.Temp);
            try
            {
                SlotSpec slot = catalogue.Slots[0];
                slot.Flags = StructuralSocketFlags.Required | StructuralSocketFlags.RequireTerrainSupport;
                slot.SupportProbeMin = new int3(0, 100000, 0);
                slot.SupportProbeMax = new int3(0, 100000, 0);
                slot.MinimumSupportContacts = 1;
                catalogue.Slots[0] = slot;

                using var instances = new NativeList<StructuralInstance>(Allocator.Temp);
                using var decisions = new NativeList<StructuralAttachmentDecision>(Allocator.Temp);
                StructuralCompositionReport report = StructuralCompositionPlanner.ExpandRoot(
                    in catalogue, Seed, StructuralCompositionFixture.RootId,
                    catalogue.ExplicitPlacements[0], instances, decisions);

                Assert.AreEqual(StructuralCompositionResult.MissingSupport, report.Result);
                Assert.AreEqual(1, decisions.Length);
                Assert.AreEqual(StructuralAttachmentRejectReason.MissingTerrainSupport,
                    decisions[0].Rejection);
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        [Test]
        public void MissingStructuralSupportRejectsWithSpecificReason()
        {
            FeatureCatalogue catalogue = StructuralCompositionFixture.Build(Allocator.Temp);
            try
            {
                SlotSpec slot = catalogue.Slots[0];
                slot.Flags = StructuralSocketFlags.Required | StructuralSocketFlags.RequireStructuralSupport;
                slot.SupportProbeMin = new int3(100, 0, 100);
                slot.SupportProbeMax = new int3(101, 1, 101);
                slot.MinimumSupportContacts = 1;
                catalogue.Slots[0] = slot;

                using var instances = new NativeList<StructuralInstance>(Allocator.Temp);
                using var decisions = new NativeList<StructuralAttachmentDecision>(Allocator.Temp);
                StructuralCompositionReport report = StructuralCompositionPlanner.ExpandRoot(
                    in catalogue, Seed, StructuralCompositionFixture.RootId,
                    catalogue.ExplicitPlacements[0], instances, decisions);

                Assert.AreEqual(StructuralCompositionResult.MissingSupport, report.Result);
                Assert.AreEqual(1, decisions.Length);
                Assert.AreEqual(StructuralAttachmentRejectReason.MissingStructuralSupport,
                    decisions[0].Rejection);
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        [Test]
        public void CountAboveCapacityFailsBeforePlacement()
        {
            FeatureCatalogue catalogue = StructuralCompositionFixture.Build(Allocator.Temp);
            try
            {
                SlotSpec slot = catalogue.Slots[0];
                slot.CountMin = 2;
                slot.CountMax = 2;
                slot.Capacity = 1;
                catalogue.Slots[0] = slot;

                using var instances = new NativeList<StructuralInstance>(Allocator.Temp);
                using var decisions = new NativeList<StructuralAttachmentDecision>(Allocator.Temp);
                StructuralCompositionReport report = StructuralCompositionPlanner.ExpandRoot(
                    in catalogue, Seed, StructuralCompositionFixture.RootId,
                    catalogue.ExplicitPlacements[0], instances, decisions);

                Assert.AreEqual(StructuralCompositionResult.CapacityExceeded, report.Result);
                Assert.AreEqual(0, report.ChildCount);
                Assert.AreEqual(StructuralAttachmentRejectReason.CapacityExceeded,
                    decisions[0].Rejection);
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        [Test]
        public void RuntimeDepthGuardStopsMalformedCycle()
        {
            FeatureCatalogue catalogue = StructuralCompositionFixture.Build(Allocator.Temp);
            try
            {
                SlotSpec slot = catalogue.Slots[0];
                slot.DefinitionId = StructuralCompositionFixture.RootId;
                catalogue.Slots[0] = slot;

                using var instances = new NativeList<StructuralInstance>(Allocator.Temp);
                StructuralCompositionReport report = StructuralCompositionPlanner.ExpandRoot(
                    in catalogue, Seed, StructuralCompositionFixture.RootId,
                    catalogue.ExplicitPlacements[0], instances);

                Assert.AreEqual(StructuralCompositionResult.DepthExceeded, report.Result);
                Assert.AreEqual(FeatureBudget.MaxCompositionDepth, report.ChildCount);
                Assert.AreEqual(FeatureBudget.MaxCompositionDepth + 1, instances.Length);
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        [Test]
        public void ChildBudgetStopsMalformedHighFanout()
        {
            FeatureCatalogue catalogue = StructuralCompositionFixture.Build(Allocator.Temp);
            try
            {
                FeatureDefinition child = catalogue.Definitions[StructuralCompositionFixture.ChildId];
                child.Footprint = int3.zero;
                catalogue.Definitions[StructuralCompositionFixture.ChildId] = child;

                SlotSpec slot = catalogue.Slots[0];
                slot.CountMin = FeatureBudget.MaxCompositionChildren + 1;
                slot.CountMax = FeatureBudget.MaxCompositionChildren + 1;
                slot.Capacity = (ushort)(FeatureBudget.MaxCompositionChildren + 1);
                slot.LocalMin = slot.LocalPosition;
                slot.LocalMax = slot.LocalPosition;
                slot.Spacing = 0;
                catalogue.Slots[0] = slot;

                using var instances = new NativeList<StructuralInstance>(Allocator.Temp);
                using var decisions = new NativeList<StructuralAttachmentDecision>(Allocator.Temp);
                StructuralCompositionReport report = StructuralCompositionPlanner.ExpandRoot(
                    in catalogue, Seed, StructuralCompositionFixture.RootId,
                    catalogue.ExplicitPlacements[0], instances, decisions);

                Assert.AreEqual(StructuralCompositionResult.ChildBudgetExceeded, report.Result);
                Assert.AreEqual(FeatureBudget.MaxCompositionChildren, report.ChildCount);
                Assert.AreEqual(StructuralAttachmentRejectReason.ChildBudgetExceeded,
                    decisions[decisions.Length - 1].Rejection);
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        [Test]
        public void AggregatePrimitiveBudgetRejectsChildBeforeAcceptance()
        {
            FeatureCatalogue catalogue = StructuralCompositionFixture.Build(Allocator.Temp);
            try
            {
                FeatureDefinition root = catalogue.Definitions[StructuralCompositionFixture.RootId];
                root.MaxPrimitives = FeatureBudget.MaxCompositionPrimitiveCost;
                catalogue.Definitions[StructuralCompositionFixture.RootId] = root;

                using var instances = new NativeList<StructuralInstance>(Allocator.Temp);
                using var decisions = new NativeList<StructuralAttachmentDecision>(Allocator.Temp);
                StructuralCompositionReport report = StructuralCompositionPlanner.ExpandRoot(
                    in catalogue, Seed, StructuralCompositionFixture.RootId,
                    catalogue.ExplicitPlacements[0], instances, decisions);

                Assert.AreEqual(StructuralCompositionResult.PrimitiveBudgetExceeded, report.Result);
                Assert.AreEqual(0, report.ChildCount);
                Assert.AreEqual(StructuralAttachmentRejectReason.PrimitiveBudgetExceeded,
                    decisions[0].Rejection);
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        [Test]
        public void AggregateVoxelExposureBudgetRejectsChildBeforeAcceptance()
        {
            FeatureCatalogue catalogue = StructuralCompositionFixture.Build(Allocator.Temp);
            try
            {
                FeatureDefinition child = catalogue.Definitions[StructuralCompositionFixture.ChildId];
                child.Footprint = new int3(FeatureBudget.MaxFootprintVoxels,
                    FeatureBudget.MaxFootprintVoxels, 11);
                catalogue.Definitions[StructuralCompositionFixture.ChildId] = child;

                using var instances = new NativeList<StructuralInstance>(Allocator.Temp);
                using var decisions = new NativeList<StructuralAttachmentDecision>(Allocator.Temp);
                StructuralCompositionReport report = StructuralCompositionPlanner.ExpandRoot(
                    in catalogue, Seed, StructuralCompositionFixture.RootId,
                    catalogue.ExplicitPlacements[0], instances, decisions);

                Assert.AreEqual(StructuralCompositionResult.VoxelBudgetExceeded, report.Result);
                Assert.AreEqual(0, report.ChildCount);
                Assert.AreEqual(StructuralAttachmentRejectReason.VoxelBudgetExceeded,
                    decisions[0].Rejection);
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        [Test]
        public void SpatialExtentBudgetRejectsDistantChild()
        {
            FeatureCatalogue catalogue = StructuralCompositionFixture.Build(Allocator.Temp);
            try
            {
                SlotSpec slot = catalogue.Slots[0];
                int distantX = FeatureBudget.MaxCompositionExtentVoxels + 1;
                slot.LocalPosition = new int3(distantX, 0, 0);
                slot.LocalMin = slot.LocalPosition;
                slot.LocalMax = slot.LocalPosition;
                catalogue.Slots[0] = slot;

                using var instances = new NativeList<StructuralInstance>(Allocator.Temp);
                using var decisions = new NativeList<StructuralAttachmentDecision>(Allocator.Temp);
                StructuralCompositionReport report = StructuralCompositionPlanner.ExpandRoot(
                    in catalogue, Seed, StructuralCompositionFixture.RootId,
                    catalogue.ExplicitPlacements[0], instances, decisions);

                Assert.AreEqual(StructuralCompositionResult.SpatialExtentExceeded, report.Result);
                Assert.AreEqual(0, report.ChildCount);
                Assert.AreEqual(StructuralAttachmentRejectReason.SpatialExtentExceeded,
                    decisions[0].Rejection);
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        [Test]
        public void AlternateSeedsProduceBoundedAttachmentVariation()
        {
            FeatureCatalogue catalogue = StructuralCompositionFixture.Build(Allocator.Temp);
            try
            {
                FeatureDefinition child = catalogue.Definitions[StructuralCompositionFixture.ChildId];
                child.Footprint = new int3(1, 1, 1);
                catalogue.Definitions[StructuralCompositionFixture.ChildId] = child;

                SlotSpec slot = catalogue.Slots[0];
                slot.LocalMin = new int3(StructuralCompositionFixture.RootFootprint.x, 0, 0);
                slot.LocalMax = new int3(StructuralCompositionFixture.RootFootprint.x, 0, 256);
                slot.CountMin = 2;
                slot.CountMax = 2;
                slot.Capacity = 2;
                slot.Spacing = 32;
                catalogue.Slots[0] = slot;
                Assert.AreEqual(CatalogueLoadResult.Ok, FeatureCatalogueBuilder.Finalise(ref catalogue));

                using var baseline = new NativeList<StructuralInstance>(Allocator.Temp);
                StructuralCompositionReport baselineReport = StructuralCompositionPlanner.ExpandRoot(
                    in catalogue, Seed, StructuralCompositionFixture.RootId,
                    catalogue.ExplicitPlacements[0], baseline);
                Assert.AreEqual(StructuralCompositionResult.Ok, baselineReport.Result);
                Assert.AreEqual(2, baselineReport.ChildCount);

                bool foundVariation = false;
                for (uint alternateSeed = Seed + 1; alternateSeed <= Seed + 32; alternateSeed++)
                {
                    using var alternate = new NativeList<StructuralInstance>(Allocator.Temp);
                    StructuralCompositionReport alternateReport = StructuralCompositionPlanner.ExpandRoot(
                        in catalogue, alternateSeed, StructuralCompositionFixture.RootId,
                        catalogue.ExplicitPlacements[0], alternate);
                    Assert.AreEqual(StructuralCompositionResult.Ok, alternateReport.Result);
                    Assert.AreEqual(2, alternateReport.ChildCount);
                    AssertAttachmentWithinRange(alternate[1].AttachmentPosition,
                        catalogue.ExplicitPlacements[0].Position, in slot);
                    AssertAttachmentWithinRange(alternate[2].AttachmentPosition,
                        catalogue.ExplicitPlacements[0].Position, in slot);
                    if (!alternate[1].AttachmentPosition.Equals(baseline[1].AttachmentPosition) ||
                        !alternate[2].AttachmentPosition.Equals(baseline[2].AttachmentPosition))
                    {
                        foundVariation = true;
                        break;
                    }
                }

                Assert.IsTrue(foundVariation,
                    "authored ranged sockets should expose deterministic but seed-varying bounded layouts");
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        private static void AssertAttachmentWithinRange(int3 worldAttachment, int3 rootPosition,
            in SlotSpec slot)
        {
            int3 local = worldAttachment - rootPosition;
            Assert.That(local.x, Is.InRange(slot.LocalMin.x, slot.LocalMax.x));
            Assert.That(local.y, Is.InRange(slot.LocalMin.y, slot.LocalMax.y));
            Assert.That(local.z, Is.InRange(slot.LocalMin.z, slot.LocalMax.z));
        }
    }
}
