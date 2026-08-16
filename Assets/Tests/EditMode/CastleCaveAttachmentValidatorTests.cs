using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleCaveAttachmentValidatorTests
    {
        [Test]
        public void OptionalAttachmentIsValidUntilNaturalCaveIsCompleted()
        {
            CastlePlan castle = CastlePlanner.Create(new int3(512, 220, 512), 1u);
            CastleSpatialPlan spatial = CompletedSpatial(in castle);

            Assert.IsTrue(
                CastleCaveAttachmentValidator.TryValidate(
                    spatial.Dungeon, null, out CastleCaveAttachmentIssue partialIssue),
                partialIssue.ToString());
            Assert.IsTrue(
                CastleCaveAttachmentValidator.TryValidate(
                    spatial.Dungeon, spatial.Cave, out CastleCaveAttachmentIssue completedIssue),
                completedIssue.ToString());
        }

        [Test]
        public void CaveFromAnotherCastleCannotAttachToDungeonThreshold()
        {
            CastlePlan firstCastle = CastlePlanner.Create(new int3(512, 220, 512), 1u);
            CastlePlan secondCastle = CastlePlanner.Create(new int3(1024, 220, 1024), 1u);
            CastleSpatialPlan first = CompletedSpatial(in firstCastle);
            CastleSpatialPlan second = CompletedSpatial(in secondCastle);

            Assert.IsFalse(
                CastleCaveAttachmentValidator.TryValidate(
                    first.Dungeon, second.Cave, out CastleCaveAttachmentIssue issue));
            Assert.AreEqual(CastleCaveAttachmentIssue.CaveEntranceMismatch, issue);
        }

        [Test]
        public void AttachedCaveRequiresDesignedDungeon()
        {
            CastlePlan castle = CastlePlanner.Create(new int3(512, 220, 512), 1u);
            CastleSpatialPlan spatial = CompletedSpatial(in castle);

            Assert.IsFalse(
                CastleCaveAttachmentValidator.TryValidate(
                    null, spatial.Cave, out CastleCaveAttachmentIssue issue));
            Assert.AreEqual(CastleCaveAttachmentIssue.MissingDungeonPlan, issue);
        }

        private static CastleSpatialPlan CompletedSpatial(in CastlePlan castle)
        {
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(castle.Seed);
            topology.Perimeter = CastlePerimeterKind.Rectangular;
            topology.Wards = CastleWardPattern.SingleWard;
            topology.KeepPlacement = CastleKeepPlacement.Central;
            topology.DesiredTowerCount = 4;
            topology.HasPosternGate = false;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in castle, in topology);
            return CastleSpatialPlanCompletion.CompleteResolved(in castle, spatial);
        }
    }
}
