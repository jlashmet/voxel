using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleCaveBuildReadinessTests
    {
        [Test]
        public void DungeonThresholdRequiresAttachedNaturalCave()
        {
            CastlePlan castle = CastlePlanner.Create(new int3(512, 220, 512), 1u);
            CastleSpatialPlan spatial = CentralSpatial(in castle);
            CastleSpatialPlan withDungeon = CastleSpatialPlanCompletion.AttachDungeon(
                in castle, spatial);

            Assert.IsTrue(withDungeon.Dungeon.HasCaveExit,
                "Seed 1 is expected to exercise the cave-exit readiness path.");
            Assert.IsNull(withDungeon.Cave);
            Assert.IsFalse(
                CastleCaveBuildReadiness.TryValidate(
                    withDungeon, out CastleCaveBuildReadinessIssue issue));
            Assert.AreEqual(CastleCaveBuildReadinessIssue.MissingCavePlan, issue);

            CastleSpatialPlan completed = CastleSpatialPlanCompletion.AttachCave(
                in castle, withDungeon);
            Assert.IsTrue(
                CastleCaveBuildReadiness.TryValidate(
                    completed, out CastleCaveBuildReadinessIssue completedIssue),
                completedIssue.ToString());
            Assert.NotNull(completed.Cave);
        }

        [Test]
        public void RuntimePreflightRequiresNaturalCaveAfterOtherPlanningIsComplete()
        {
            CastlePlan castle = CastlePlanner.Create(new int3(512, 220, 512), 1u);
            CastleSpatialPlan spatial = CentralSpatial(in castle);
            spatial = CastleSpatialPlanCompletion.AttachTowerVariation(in castle, spatial);
            spatial = CastleSpatialPlanCompletion.AttachKeepFloors(in castle, spatial);
            spatial = CastleSpatialPlanCompletion.AttachCourtyardBuildings(in castle, spatial);
            spatial = CastleSpatialPlanCompletion.AttachDungeon(in castle, spatial);

            CastleBuildPreflightResult missing = CastleBuildPreflight.EvaluateRuntimeReady(
                in castle, spatial, long.MaxValue);
            Assert.AreEqual(CastleBuildPreflightIssue.IncompleteSpatialPlan, missing.Issue);
            Assert.AreEqual(CastleSpatialBuildReadinessIssue.MissingCavePlan, missing.ReadinessIssue);

            CastleSpatialPlan completed = CastleSpatialPlanCompletion.AttachCave(in castle, spatial);
            CastleBuildPreflightResult ready = CastleBuildPreflight.EvaluateRuntimeReady(
                in castle, completed, long.MaxValue);
            Assert.IsTrue(ready.IsValid, ready.ReadinessIssue.ToString());
        }

        [Test]
        public void InvalidDungeonReturnsDiagnosticInsteadOfIndexingThreshold()
        {
            CastlePlan castle = CastlePlanner.Create(new int3(512, 220, 512), 1u);
            CastleSpatialPlan completed = CastleSpatialPlanCompletion.CompleteResolved(
                in castle, CentralSpatial(in castle));
            DungeonRoomPlan corrupted = completed.Dungeon.Rooms[0];
            corrupted.Id = 999;
            completed.Dungeon.Rooms[0] = corrupted;

            Assert.IsFalse(
                CastleCaveBuildReadiness.TryValidate(
                    completed, out CastleCaveBuildReadinessIssue issue));
            Assert.AreEqual(CastleCaveBuildReadinessIssue.InvalidDungeonPlan, issue);
        }

        [Test]
        public void DungeonWithoutThresholdRequiresNoNaturalCave()
        {
            CastlePlan castle = CastlePlanner.Create(new int3(512, 220, 512), 18u);
            CastleSpatialPlan spatial = CentralSpatial(in castle);
            CastleSpatialPlan withDungeon = CastleSpatialPlanCompletion.AttachDungeon(
                in castle, spatial);

            Assert.IsFalse(withDungeon.Dungeon.HasCaveExit,
                "Seed 18 is expected to exercise the no-cave readiness path.");
            Assert.IsNull(withDungeon.Cave);
            Assert.IsTrue(
                CastleCaveBuildReadiness.TryValidate(
                    withDungeon, out CastleCaveBuildReadinessIssue issue),
                issue.ToString());
        }

        private static CastleSpatialPlan CentralSpatial(in CastlePlan castle)
        {
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(castle.Seed);
            topology.Perimeter = CastlePerimeterKind.Rectangular;
            topology.Wards = CastleWardPattern.SingleWard;
            topology.KeepPlacement = CastleKeepPlacement.Central;
            topology.DesiredTowerCount = 4;
            topology.HasPosternGate = false;
            return CastleSpatialPlanner.Create(in castle, in topology);
        }
    }
}
