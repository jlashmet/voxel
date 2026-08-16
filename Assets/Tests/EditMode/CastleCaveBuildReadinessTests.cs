using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleCaveBuildReadinessTests
    {
        [Test]
        public void DungeonThresholdRequiresAttachedNaturalCaveAndDecoration()
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

            CastleSpatialPlan withCave = CastleSpatialPlanCompletion.AttachCave(
                in castle, withDungeon);
            Assert.NotNull(withCave.Cave);
            Assert.IsNull(withCave.CaveDecoration);
            Assert.IsFalse(
                CastleCaveBuildReadiness.TryValidate(
                    withCave, out CastleCaveBuildReadinessIssue decorationIssue));
            Assert.AreEqual(
                CastleCaveBuildReadinessIssue.MissingCaveDecorationPlan,
                decorationIssue);

            CastleSpatialPlan completed = CastleSpatialPlanCompletion.AttachCaveDecoration(
                in castle, withCave);
            Assert.IsTrue(
                CastleCaveBuildReadiness.TryValidate(
                    completed, out CastleCaveBuildReadinessIssue completedIssue),
                completedIssue.ToString());
            Assert.NotNull(completed.CaveDecoration);
        }

        [Test]
        public void RuntimePreflightRequiresNaturalCaveDecorationAfterOtherPlanningIsComplete()
        {
            CastlePlan castle = CastlePlanner.Create(new int3(512, 220, 512), 1u);
            CastleSpatialPlan spatial = CentralSpatial(in castle);
            spatial = CastleSpatialPlanCompletion.AttachTowerVariation(in castle, spatial);
            spatial = CastleSpatialPlanCompletion.AttachKeepFloors(in castle, spatial);
            spatial = CastleSpatialPlanCompletion.AttachKeepCirculation(in castle, spatial);
            spatial = CastleSpatialPlanCompletion.AttachCourtyardBuildings(in castle, spatial);
            spatial = CastleSpatialPlanCompletion.AttachDungeon(in castle, spatial);

            CastleBuildPreflightResult missingCave = CastleBuildPreflight.EvaluateRuntimeReady(
                in castle, spatial, long.MaxValue);
            Assert.AreEqual(CastleBuildPreflightIssue.IncompleteSpatialPlan, missingCave.Issue);
            Assert.AreEqual(CastleSpatialBuildReadinessIssue.MissingCavePlan, missingCave.ReadinessIssue);

            CastleSpatialPlan withCave = CastleSpatialPlanCompletion.AttachCave(in castle, spatial);
            CastleBuildPreflightResult missingDecoration = CastleBuildPreflight.EvaluateRuntimeReady(
                in castle, withCave, long.MaxValue);
            Assert.AreEqual(CastleBuildPreflightIssue.IncompleteSpatialPlan, missingDecoration.Issue);
            Assert.AreEqual(
                CastleSpatialBuildReadinessIssue.MissingCaveDecorationPlan,
                missingDecoration.ReadinessIssue);

            CastleSpatialPlan completed = CastleSpatialPlanCompletion.AttachCaveDecoration(
                in castle, withCave);
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
        public void DungeonWithoutThresholdRequiresNoNaturalCaveOrDecoration()
        {
            CastlePlan castle = CastlePlanner.Create(new int3(512, 220, 512), 18u);
            CastleSpatialPlan spatial = CentralSpatial(in castle);
            CastleSpatialPlan withDungeon = CastleSpatialPlanCompletion.AttachDungeon(
                in castle, spatial);

            Assert.IsFalse(withDungeon.Dungeon.HasCaveExit,
                "Seed 18 is expected to exercise the no-cave readiness path.");
            Assert.IsNull(withDungeon.Cave);
            Assert.IsNull(withDungeon.CaveDecoration);
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
