using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleDungeonSpatialValidationTests
    {
        [Test]
        public void BaseSpatialPlanMayRemainValidBeforeDungeonCompletion()
        {
            CastlePlan dimensions = CastlePlanner.Create(int3.zero, 131u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(131u);
            topology.Perimeter = CastlePerimeterKind.Rectangular;
            topology.Wards = CastleWardPattern.SingleWard;
            topology.KeepPlacement = CastleKeepPlacement.Central;
            topology.DesiredTowerCount = 4;
            topology.HasPosternGate = false;

            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);

            Assert.IsNull(spatial.Dungeon);
            Assert.IsTrue(
                CastleSpatialPlanValidator.TryValidate(
                    in dimensions, spatial, out CastleSpatialPlanIssue issue),
                issue.ToString());
            Assert.AreEqual(CastleSpatialPlanIssue.None, issue);
        }

        [Test]
        public void MalformedAttachedDungeonIsStructuralButKeepsRuntimeReadinessDiagnostic()
        {
            CastlePlan dimensions = CastlePlanner.Create(int3.zero, 137u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(137u);
            topology.Perimeter = CastlePerimeterKind.Rectangular;
            topology.Wards = CastleWardPattern.SingleWard;
            topology.KeepPlacement = CastleKeepPlacement.Central;
            topology.DesiredTowerCount = 4;
            topology.HasPosternGate = false;

            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);
            CastleSpatialPlan completed = CastleSpatialPlanCompletion.CompleteResolved(
                in dimensions, spatial);
            Assert.NotNull(completed.Dungeon);

            DungeonRoomPlan[] rooms = completed.Dungeon.Rooms;
            DungeonRoomPlan corrupted = rooms[0];
            corrupted.Id = rooms.Length + 10;
            rooms[0] = corrupted;

            Assert.IsFalse(
                CastleSpatialPlanValidator.TryValidate(
                    in dimensions, completed, out CastleSpatialPlanIssue spatialIssue));
            Assert.AreEqual(CastleSpatialPlanIssue.InvalidDungeonPlan, spatialIssue);

            CastleBuildPreflightResult readiness = CastleBuildPreflight.EvaluateRuntimeReady(
                in dimensions, completed, long.MaxValue);
            Assert.AreEqual(CastleBuildPreflightIssue.IncompleteSpatialPlan, readiness.Issue);
            Assert.AreEqual(
                CastleSpatialBuildReadinessIssue.InvalidDungeonPlan,
                readiness.ReadinessIssue);
            Assert.AreEqual(CastleSpatialPlanIssue.None, readiness.SpatialPlanIssue);
            Assert.IsFalse(readiness.IsValid);
        }

        [Test]
        public void DungeonFromDifferentCastleOriginIsRejectedAsAttachmentMismatch()
        {
            CastlePlan dimensions = CastlePlanner.Create(new int3(200, 64, 300), 149u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(149u);
            topology.Perimeter = CastlePerimeterKind.Rectangular;
            topology.Wards = CastleWardPattern.SingleWard;
            topology.KeepPlacement = CastleKeepPlacement.Central;
            topology.DesiredTowerCount = 4;
            topology.HasPosternGate = false;

            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);
            CastleSpatialPlan completed = CastleSpatialPlanCompletion.CompleteResolved(
                in dimensions, spatial);
            Assert.IsTrue(
                DungeonPlanValidator.TryValidate(completed.Dungeon, out DungeonPlanIssue dungeonIssue),
                dungeonIssue.ToString());

            CastlePlan shiftedCastle = dimensions;
            shiftedCastle.Centre += new int3(32, 0, -16);

            Assert.IsFalse(
                CastleSpatialPlanValidator.TryValidate(
                    in shiftedCastle, completed, out CastleSpatialPlanIssue spatialIssue));
            Assert.AreEqual(CastleSpatialPlanIssue.DungeonEntranceMismatch, spatialIssue);

            CastleBuildPreflightResult readiness = CastleBuildPreflight.EvaluateRuntimeReady(
                in shiftedCastle, completed, long.MaxValue);
            Assert.AreEqual(CastleBuildPreflightIssue.IncompleteSpatialPlan, readiness.Issue);
            Assert.AreEqual(
                CastleSpatialBuildReadinessIssue.DungeonEntranceMismatch,
                readiness.ReadinessIssue);
            Assert.AreEqual(CastleSpatialPlanIssue.None, readiness.SpatialPlanIssue);
            Assert.IsFalse(readiness.IsValid);
        }
    }
}
