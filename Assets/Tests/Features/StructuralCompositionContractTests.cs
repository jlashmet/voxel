using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;
using VoxelEngine.Tests.Features.Fixtures;

namespace VoxelEngine.Tests.Features
{
    public sealed class StructuralCompositionContractTests
    {
        private const uint Seed = 0x51A7C0DEu;

        [Test]
        public void AcceptedGraphHashIgnoresOptionalRejectedDecision()
        {
            FeatureCatalogue rejectedCatalogue = StructuralCompositionFixture.Build(Allocator.Temp);
            FeatureCatalogue emptyCatalogue = StructuralCompositionFixture.Build(Allocator.Temp);
            try
            {
                SlotSpec rejectedSlot = rejectedCatalogue.Slots[0];
                rejectedSlot.Flags = StructuralSocketFlags.None;
                rejectedCatalogue.Slots[0] = rejectedSlot;
                FeatureDefinition incompatible = rejectedCatalogue.Definitions[StructuralCompositionFixture.ChildId];
                StructuralPieceSpec incompatiblePiece = incompatible.StructuralPiece;
                incompatiblePiece.Offers = 1UL << 42;
                incompatiblePiece.Accepts = 1UL << 42;
                incompatible.StructuralPiece = incompatiblePiece;
                rejectedCatalogue.Definitions[StructuralCompositionFixture.ChildId] = incompatible;

                SlotSpec emptySlot = emptyCatalogue.Slots[0];
                emptySlot.Flags = StructuralSocketFlags.None;
                emptySlot.CountMin = 0;
                emptySlot.CountMax = 0;
                emptyCatalogue.Slots[0] = emptySlot;

                using var rejectedInstances = new NativeList<StructuralInstance>(Allocator.Temp);
                using var rejectedDecisions = new NativeList<StructuralAttachmentDecision>(Allocator.Temp);
                using var emptyInstances = new NativeList<StructuralInstance>(Allocator.Temp);

                StructuralCompositionReport rejected = StructuralCompositionPlanner.ExpandRoot(
                    in rejectedCatalogue, Seed, StructuralCompositionFixture.RootId,
                    rejectedCatalogue.ExplicitPlacements[0], rejectedInstances, rejectedDecisions);
                StructuralCompositionReport empty = StructuralCompositionPlanner.ExpandRoot(
                    in emptyCatalogue, Seed, StructuralCompositionFixture.RootId,
                    emptyCatalogue.ExplicitPlacements[0], emptyInstances);

                Assert.AreEqual(StructuralCompositionResult.Ok, rejected.Result);
                Assert.AreEqual(StructuralCompositionResult.Ok, empty.Result);
                Assert.AreEqual(0, rejected.ChildCount);
                Assert.AreEqual(0, empty.ChildCount);
                Assert.AreEqual(empty.GraphHash, rejected.GraphHash,
                    "diagnostic rejection history must not change accepted structural graph identity");
                Assert.AreEqual(1, rejectedDecisions.Length);
                Assert.AreEqual(StructuralAttachmentRejectReason.IncompatibleRoleOrTags,
                    rejectedDecisions[0].Rejection);
            }
            finally
            {
                emptyCatalogue.Dispose();
                rejectedCatalogue.Dispose();
            }
        }

        [Test]
        public void MissingTerrainSupportRejectsWithTerrainDiagnostic()
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
                Assert.AreEqual(CatalogueLoadResult.Ok, FeatureCatalogueBuilder.Finalise(ref catalogue));

                using var instances = new NativeList<StructuralInstance>(Allocator.Temp);
                using var decisions = new NativeList<StructuralAttachmentDecision>(Allocator.Temp);
                StructuralCompositionReport report = StructuralCompositionPlanner.ExpandRoot(
                    in catalogue, Seed, StructuralCompositionFixture.RootId,
                    catalogue.ExplicitPlacements[0], instances, decisions);

                Assert.AreEqual(StructuralCompositionResult.MissingSupport, report.Result);
                Assert.AreEqual(1, decisions.Length);
                Assert.AreEqual(StructuralAttachmentRejectReason.MissingTerrainSupport, decisions[0].Rejection);
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        [Test]
        public void MissingStructuralSupportRejectsWithStructuralDiagnostic()
        {
            FeatureCatalogue catalogue = StructuralCompositionFixture.Build(Allocator.Temp);
            try
            {
                SlotSpec slot = catalogue.Slots[0];
                slot.Flags = StructuralSocketFlags.Required | StructuralSocketFlags.RequireStructuralSupport;
                slot.SupportProbeMin = new int3(128, 0, 0);
                slot.SupportProbeMax = new int3(129, 1, 1);
                slot.MinimumSupportContacts = 1;
                catalogue.Slots[0] = slot;
                Assert.AreEqual(CatalogueLoadResult.Ok, FeatureCatalogueBuilder.Finalise(ref catalogue));

                using var instances = new NativeList<StructuralInstance>(Allocator.Temp);
                using var decisions = new NativeList<StructuralAttachmentDecision>(Allocator.Temp);
                StructuralCompositionReport report = StructuralCompositionPlanner.ExpandRoot(
                    in catalogue, Seed, StructuralCompositionFixture.RootId,
                    catalogue.ExplicitPlacements[0], instances, decisions);

                Assert.AreEqual(StructuralCompositionResult.MissingSupport, report.Result);
                Assert.AreEqual(StructuralAttachmentRejectReason.MissingStructuralSupport, decisions[0].Rejection);
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        [Test]
        public void CapacityOverrunFailsBeforePlacement()
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
                Assert.AreEqual(StructuralAttachmentRejectReason.CapacityExceeded, decisions[0].Rejection);
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        [Test]
        public void RuntimeDepthGuardFailsClosedOnMalformedCycle()
        {
            FeatureCatalogue catalogue = StructuralCompositionFixture.Build(Allocator.Temp);
            try
            {
                FeatureDefinition child = catalogue.Definitions[StructuralCompositionFixture.ChildId];
                FeatureDefinition root = catalogue.Definitions[StructuralCompositionFixture.RootId];
                child.SlotOffset = root.SlotOffset;
                child.SlotCount = root.SlotCount;
                child.ProgramOffset = root.ProgramOffset;
                child.ProgramLength = root.ProgramLength;
                catalogue.Definitions[StructuralCompositionFixture.ChildId] = child;

                using var instances = new NativeList<StructuralInstance>(Allocator.Temp);
                StructuralCompositionReport report = StructuralCompositionPlanner.ExpandRoot(
                    in catalogue, Seed, StructuralCompositionFixture.RootId,
                    catalogue.ExplicitPlacements[0], instances);

                Assert.AreEqual(StructuralCompositionResult.DepthExceeded, report.Result);
                Assert.AreEqual(FeatureBudget.MaxCompositionDepth, report.ChildCount);
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        [Test]
        public void ChildBudgetOverrunFailsClosed()
        {
            FeatureCatalogue catalogue = StructuralCompositionFixture.Build(Allocator.Temp);
            try
            {
                FeatureDefinition child = catalogue.Definitions[StructuralCompositionFixture.ChildId];
                child.Footprint = new int3(1, 1, 1);
                catalogue.Definitions[StructuralCompositionFixture.ChildId] = child;

                SlotSpec slot = catalogue.Slots[0];
                slot.CountMin = FeatureBudget.MaxCompositionChildren + 1;
                slot.CountMax = FeatureBudget.MaxCompositionChildren + 1;
                slot.Capacity = (ushort)(FeatureBudget.MaxCompositionChildren + 1);
                slot.LocalMin = new int3(StructuralCompositionFixture.RootFootprint.x, 0, 0);
                slot.LocalMax = new int3(StructuralCompositionFixture.RootFootprint.x, 10000, 10000);
                catalogue.Slots[0] = slot;

                using var instances = new NativeList<StructuralInstance>(Allocator.Temp);
                StructuralCompositionReport report = StructuralCompositionPlanner.ExpandRoot(
                    in catalogue, Seed, StructuralCompositionFixture.RootId,
                    catalogue.ExplicitPlacements[0], instances);

                Assert.AreEqual(StructuralCompositionResult.ChildBudgetExceeded, report.Result);
                Assert.AreEqual(FeatureBudget.MaxCompositionChildren, report.ChildCount);
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        [Test]
        public void PrimitiveBudgetOverrunRejectsChildBeforeAcceptance()
        {
            FeatureCatalogue catalogue = StructuralCompositionFixture.Build(Allocator.Temp);
            try
            {
                FeatureDefinition child = catalogue.Definitions[StructuralCompositionFixture.ChildId];
                child.MaxPrimitives = FeatureBudget.MaxCompositionPrimitiveCost;
                catalogue.Definitions[StructuralCompositionFixture.ChildId] = child;

                using var instances = new NativeList<StructuralInstance>(Allocator.Temp);
                using var decisions = new NativeList<StructuralAttachmentDecision>(Allocator.Temp);
                StructuralCompositionReport report = StructuralCompositionPlanner.ExpandRoot(
                    in catalogue, Seed, StructuralCompositionFixture.RootId,
                    catalogue.ExplicitPlacements[0], instances, decisions);

                Assert.AreEqual(StructuralCompositionResult.PrimitiveBudgetExceeded, report.Result);
                Assert.AreEqual(0, report.ChildCount);
                Assert.AreEqual(StructuralAttachmentRejectReason.PrimitiveBudgetExceeded, decisions[0].Rejection);
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        [Test]
        public void ChildVoxelExposureBudgetOverrunRejectsBeforeAcceptance()
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
                Assert.AreEqual(StructuralAttachmentRejectReason.VoxelBudgetExceeded, decisions[0].Rejection);
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        [Test]
        public void SpatialExtentOverrunRejectsBeforeAcceptance()
        {
            FeatureCatalogue catalogue = StructuralCompositionFixture.Build(Allocator.Temp);
            try
            {
                SlotSpec slot = catalogue.Slots[0];
                int farX = FeatureBudget.MaxCompositionExtentVoxels + StructuralCompositionFixture.RootFootprint.x + 1;
                slot.LocalPosition = new int3(farX, 0, 0);
                slot.LocalMin = slot.LocalPosition;
                slot.LocalMax = slot.LocalPosition;
                catalogue.Slots[0] = slot;

                using var instances = new NativeList<StructuralInstance>(Allocator.Temp);
                using var decisions = new NativeList<StructuralAttachmentDecision>(Allocator.Temp);
                StructuralCompositionReport report = StructuralCompositionPlanner.ExpandRoot(
                    in catalogue, Seed, StructuralCompositionFixture.RootId,
                    catalogue.ExplicitPlacements[0], instances, decisions);

                Assert.AreEqual(StructuralCompositionResult.SpatialExtentExceeded, report.Result);
                Assert.AreEqual(StructuralAttachmentRejectReason.SpatialExtentExceeded, decisions[0].Rejection);
                Assert.AreEqual(0, report.ChildCount);
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        [Test]
        public void RepeatedAttachmentSpacingCanSeparateAlongYOrZWhenXIsFixed()
        {
            FeatureCatalogue catalogue = StructuralCompositionFixture.Build(Allocator.Temp);
            try
            {
                SlotSpec slot = catalogue.Slots[0];
                slot.CountMin = 2;
                slot.CountMax = 2;
                slot.Capacity = 2;
                slot.LocalMin = new int3(StructuralCompositionFixture.RootFootprint.x, 0, 0);
                slot.LocalMax = new int3(StructuralCompositionFixture.RootFootprint.x, 32, 32);
                slot.Spacing = 16;
                catalogue.Slots[0] = slot;
                Assert.AreEqual(CatalogueLoadResult.Ok, FeatureCatalogueBuilder.Finalise(ref catalogue));

                using var instances = new NativeList<StructuralInstance>(Allocator.Temp);
                StructuralCompositionReport report = StructuralCompositionPlanner.ExpandRoot(
                    in catalogue, Seed, StructuralCompositionFixture.RootId,
                    catalogue.ExplicitPlacements[0], instances);

                Assert.AreEqual(StructuralCompositionResult.Ok, report.Result);
                Assert.AreEqual(2, report.ChildCount);
                int3 a = instances[1].AttachmentPosition;
                int3 b = instances[2].AttachmentPosition;
                Assert.AreEqual(a.x, b.x, "the fixture fixes X so spacing must use Y/Z");
                long dy = (long)a.y - b.y;
                long dz = (long)a.z - b.z;
                Assert.GreaterOrEqual(dy * dy + dz * dz, 16L * 16L);
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        [Test]
        public void ParentSocketReservedClearanceBlocksAdditionalSibling()
        {
            FeatureCatalogue catalogue = StructuralCompositionFixture.Build(Allocator.Temp);
            try
            {
                SlotSpec slot = catalogue.Slots[0];
                slot.CountMin = 2;
                slot.CountMax = 2;
                slot.Capacity = 2;
                slot.LocalMin = new int3(StructuralCompositionFixture.RootFootprint.x, 0, 0);
                slot.LocalMax = new int3(StructuralCompositionFixture.RootFootprint.x, 32, 32);
                slot.Spacing = 16;
                slot.ClearanceMin = int3.zero;
                slot.ClearanceMax = new int3(16, 48, 48);
                catalogue.Slots[0] = slot;
                Assert.AreEqual(CatalogueLoadResult.Ok, FeatureCatalogueBuilder.Finalise(ref catalogue));

                using var instances = new NativeList<StructuralInstance>(Allocator.Temp);
                using var decisions = new NativeList<StructuralAttachmentDecision>(Allocator.Temp);
                StructuralCompositionReport report = StructuralCompositionPlanner.ExpandRoot(
                    in catalogue, Seed, StructuralCompositionFixture.RootId,
                    catalogue.ExplicitPlacements[0], instances, decisions);

                Assert.AreEqual(StructuralCompositionResult.ClearanceBlocked, report.Result);
                Assert.AreEqual(1, report.ChildCount);
                Assert.AreEqual(2, decisions.Length);
                Assert.IsTrue(decisions[0].Accepted);
                Assert.AreEqual(StructuralAttachmentRejectReason.ClearanceBlocked, decisions[1].Rejection);
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        [Test]
        public void AlternateSeedsProduceMeaningfulBoundedVariation()
        {
            FeatureCatalogue catalogue = StructuralCompositionFixture.Build(Allocator.Temp);
            try
            {
                SlotSpec slot = catalogue.Slots[0];
                slot.CountMin = 1;
                slot.CountMax = 2;
                slot.Capacity = 2;
                slot.LocalMin = new int3(StructuralCompositionFixture.RootFootprint.x, 0, 0);
                slot.LocalMax = new int3(StructuralCompositionFixture.RootFootprint.x, 32, 32);
                slot.Spacing = 16;
                catalogue.Slots[0] = slot;
                Assert.AreEqual(CatalogueLoadResult.Ok, FeatureCatalogueBuilder.Finalise(ref catalogue));

                var hashes = new HashSet<ulong>();
                using var instances = new NativeList<StructuralInstance>(Allocator.Temp);
                for (uint seed = 1; seed <= 24; seed++)
                {
                    StructuralCompositionReport report = StructuralCompositionPlanner.ExpandRoot(
                        in catalogue, seed, StructuralCompositionFixture.RootId,
                        catalogue.ExplicitPlacements[0], instances);
                    Assert.AreEqual(StructuralCompositionResult.Ok, report.Result, $"seed {seed}");
                    Assert.That(report.ChildCount, Is.InRange(1, 2), $"seed {seed}");
                    Assert.LessOrEqual(report.PrimitiveCost, FeatureBudget.MaxCompositionPrimitiveCost);
                    Assert.LessOrEqual(report.VoxelCost, FeatureBudget.MaxCompositionVoxelCost);
                    hashes.Add(report.GraphHash);
                }

                Assert.Greater(hashes.Count, 1,
                    "alternate seeds should vary bounded attachment choice rather than collapse to one graph");
            }
            finally
            {
                catalogue.Dispose();
            }
        }
    }
}
